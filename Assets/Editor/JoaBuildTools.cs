using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One-shot build tooling for Journey of Adventures. Imports the new sprites
/// with the required settings and constructs the remaining levels
/// (Jungle / Desert / Ocean) plus the End Credits scene by transforming a copy
/// of the existing Cave scene (so the player, camera and full UI canvas are
/// reused). Run "JoA/BUILD EVERYTHING".
/// </summary>
public static class JoaBuildTools
{
    const string ScenesDir = "Assets/Scenes/";
    const string CaveScene = ScenesDir + "Cave.unity";

    const string L3 = "Assets/Sprites/Level 3 JUNGLE TEMPLE/";
    const string L4 = "Assets/Sprites/Level 4 DESERT PYRAMID/";
    const string L5 = "Assets/Sprites/Level 5 THE OCEAN/";

    const string Sfx = "Assets/Sounds/Sfx/";
    const string Mus = "Assets/Sounds/Music/";

    const int EnemyLayer = 6;
    const int GroundLayer = 3;

    // ─────────────────────────────────────────────────────────────────────────
    [MenuItem("JoA/BUILD EVERYTHING (Levels 3-5 + Credits)", false, 0)]
    public static void BuildEverything()
    {
        try
        {
            EditorUtility.DisplayProgressBar("JoA Build", "Importing sprites...", 0.05f);
            FixSpriteImports();

            EditorUtility.DisplayProgressBar("JoA Build", "Patching Cave exit...", 0.15f);
            PatchCaveScene();

            EditorUtility.DisplayProgressBar("JoA Build", "Building Level 3 — Jungle...", 0.25f);
            BuildJungle();

            EditorUtility.DisplayProgressBar("JoA Build", "Building Level 4 — Desert...", 0.45f);
            BuildDesert();

            EditorUtility.DisplayProgressBar("JoA Build", "Building Level 5 — Ocean...", 0.65f);
            BuildOcean();

            EditorUtility.DisplayProgressBar("JoA Build", "Building End Credits...", 0.85f);
            BuildEndCredits();

            EditorUtility.DisplayProgressBar("JoA Build", "Configuring build settings...", 0.95f);
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[JoA] BUILD EVERYTHING complete — Levels 3, 4, 5 and End Credits are ready.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("JoA/Fix Sprite Imports Only", false, 20)]
    public static void FixSpriteImports()
    {
        AssetDatabase.Refresh();
        string[] folders = { L3.TrimEnd('/'), L4.TrimEnd('/'), L5.TrimEnd('/') };
        var existing = folders.Where(AssetDatabase.IsValidFolder).ToArray();
        if (existing.Length == 0) return;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", existing);
        int fixedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;

            imp.textureType         = TextureImporterType.Sprite;
            imp.spriteImportMode    = SpriteImportMode.Single;
            imp.filterMode          = FilterMode.Point;
            imp.spritePixelsPerUnit = 16;
            imp.textureCompression  = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled       = false;
            imp.alphaIsTransparency = true;

            TextureImporterPlatformSettings ps = imp.GetDefaultPlatformTextureSettings();
            ps.textureCompression = TextureImporterCompression.Uncompressed;
            ps.maxTextureSize     = 4096;
            imp.SetPlatformTextureSettings(ps);

            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();
            fixedCount++;
        }
        AssetDatabase.Refresh();
        Debug.Log($"[JoA] Sprite imports fixed: {fixedCount} textures (Single, PPU 16, Point, no compression).");
    }

    // ── Cave patch ────────────────────────────────────────────────────────────
    [MenuItem("JoA/Patch Cave Exit Only", false, 21)]
    public static void PatchCaveScene()
    {
        Scene scene = EditorSceneManager.OpenScene(CaveScene, OpenSceneMode.Single);
        foreach (CraftingShrine cs in Object.FindObjectsByType<CraftingShrine>(FindObjectsSortMode.None))
        {
            var so = new SerializedObject(cs);
            var prop = so.FindProperty("nextSceneName");
            if (prop != null) { prop.stringValue = "Jungle"; so.ApplyModifiedProperties(); }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[JoA] Cave crafting shrine now leads to the Jungle.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  LEVEL 3 — JUNGLE TEMPLE
    // ═════════════════════════════════════════════════════════════════════════
    [MenuItem("JoA/Build Level 3 — Jungle", false, 40)]
    public static void BuildJungle()
    {
        Scene scene = CopyAndOpen(CaveScene, ScenesDir + "Jungle.unity");
        StripCaveObjects(scene);
        SetGlobalLight(new Color(1f, 0.99f, 0.92f), 1.15f);

        // Background
        Sprite bg = LoadSprite(L3 + "Level_3_Map.png");
        float w = bg != null ? bg.bounds.size.x : 75f;
        MakeBackground("JungleBackground", bg, new Vector3(w / 2f, 0f, 0f), 1f, -100);

        // Ground + jungle platforms
        MakeGround("Ground", w / 2f, -8f, w + 20f, 8f);          // top at y = -4
        Color leaf = new Color(0.30f, 0.45f, 0.18f);
        float[] px = { 18f, 26f, 34f, 30f, 42f, 50f, 46f };
        float[] py = { -1.2f, 0.6f, 2.2f, -0.4f, 1.4f, -0.8f, 3f };
        for (int i = 0; i < px.Length; i++)
            MakePlatform($"JunglePlatform_{i}", new Vector3(px[i], py[i], 0f),
                         new Vector2(4.5f, 0.7f), leaf);

        MovePlayer(scene, new Vector3(4f, -3f, 0f));
        FixCamera(scene, 0f, w, -6f, 9f);

        // Manager
        JungleManager mgr = SwapManager<JungleManager>(scene);
        mgr.ambientMusic    = LoadAudio(Mus + "village music.mp3");
        mgr.combatMusic     = LoadAudio(Mus + "Village fight musik.mp3");
        mgr.templeOpenClip  = LoadAudio(Sfx + "Banor/Djungel/" + Diacritic("Oppnar djungel dorr efter boss fight.mp3"));
        mgr.defaultWeapon   = LoadItem("Assets/DiamondSword.asset");
        SetMusicManagerClips(scene, mgr.ambientMusic, mgr.combatMusic);

        // ── Enemies — monkeys + vine snakes spread toward the temple ──────────
        float[] vineX  = { 12f, 22f, 31f, 39f, 48f, 56f };
        foreach (float x in vineX) SpawnVineSnake(new Vector3(x, -2.5f, 0f));
        float[] monkeyX = { 16f, 27f, 36f, 44f, 52f, 60f };
        float[] monkeyY = { -2.5f, 0.8f, 2.4f, -2.5f, 1.6f, -2.5f };
        for (int i = 0; i < monkeyX.Length; i++)
            SpawnMonkey(new Vector3(monkeyX[i], monkeyY[i], 0f));

        // ── Jungle Guardian mini-boss ─────────────────────────────────────────
        SpawnJungleGuardian(new Vector3(64f, -2f, 0f));

        // ── Temple inscription portal (sealed until the Guardian dies) ────────
        StoryPortal temple = MakeStoryPortal("TempleInscription", new Vector3(71.5f, -2.5f, 0f),
            new Vector2(4f, 6f));
        temple.unlocked        = false;
        temple.requireKeyPress = true;
        temple.speakerName     = "Temple Inscription";
        temple.lines = new[]
        {
            "The source of darkness lies beyond the endless desert.",
            "Only the power of the ancient pyramid can weaken the ocean beast."
        };
        temple.nextScene         = "Desert";
        temple.objectiveOnUnlock = "Read the temple inscription";
        temple.mysticClip = LoadAudio(Sfx + "Banor/Djungel/" + Diacritic("Gar in i djungel temple.mp3"));

        AddAmbientSfx(scene, new[]
        {
            LoadAudio(Sfx + "Monster/Vine snake/Vine snake ljud 1.mp3"),
            LoadAudio(Sfx + "Monster/Apa (inte mini bossen)/Monkey attack.mp3"),
        }, 7f, 16f);

        SaveScene(scene);
        Debug.Log("[JoA] Level 3 — Jungle built.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  LEVEL 4 — DESERT PYRAMID
    // ═════════════════════════════════════════════════════════════════════════
    [MenuItem("JoA/Build Level 4 — Desert", false, 41)]
    public static void BuildDesert()
    {
        Scene scene = CopyAndOpen(CaveScene, ScenesDir + "Desert.unity");
        StripCaveObjects(scene);
        SetGlobalLight(new Color(1f, 0.96f, 0.85f), 1.2f);

        Sprite bg = LoadSprite(L4 + "Desert level.png");
        float w = bg != null ? bg.bounds.size.x : 125f;
        MakeBackground("DesertBackground", bg, new Vector3(w / 2f, 0f, 0f), 1f, -100);

        MakeGround("Ground", w / 2f, -8f, w + 20f, 8f);          // top at y = -4

        MovePlayer(scene, new Vector3(4f, -3f, 0f));
        FixCamera(scene, 0f, w, -6f, 8f);

        DesertManager mgr = SwapManager<DesertManager>(scene);
        mgr.ambientMusic  = LoadAudio(Mus + "Lobby musik.mp3");
        mgr.combatMusic   = LoadAudio(Mus + "Village fight musik.mp3");
        mgr.defaultWeapon = LoadItem("Assets/DiamondSword.asset");
        SetMusicManagerClips(scene, mgr.ambientMusic, mgr.combatMusic);

        AudioClip obeliskClip = LoadAudio(Sfx + "Banor/" + Diacritic("Oken/Obelisk aktivering.mp3"));

        // ── Obelisk 1 trial — the great Sandworm ──────────────────────────────
        SpawnSandworm(new Vector3(26f, -2.5f, 0f), 150f, false, 2.4f);
        MakeObelisk(1, new Vector3(34f, -3.8f, 0f), obeliskClip);

        // ── Obelisk 2 trial — the platform puzzle ─────────────────────────────
        Color sand = new Color(0.78f, 0.62f, 0.36f);
        MakePlatform("DesertPlat_0", new Vector3(50f, -1.5f, 0f), new Vector2(4f, 0.7f), sand);
        MakeMovingPlatform("DesertMover_0", new Vector3(56f, 0f, 0f),
                           Vector2.zero, new Vector2(0f, 3.5f));
        MakePlatform("DesertPlat_1", new Vector3(61f, 1.5f, 0f), new Vector2(4f, 0.7f), sand);
        MakeMovingPlatform("DesertMover_1", new Vector3(65f, 2.5f, 0f),
                           new Vector2(-2f, 0f), new Vector2(2f, 0f));
        MakeObelisk(2, new Vector3(70f, 2.2f, 0f), obeliskClip);
        MakePlatform("DesertPlat_2", new Vector3(70f, 0.6f, 0f), new Vector2(5f, 0.7f), sand);

        // ── Obelisk 3 trial — the sand wave (6 creatures, hidden until reached) ─
        for (int i = 0; i < 6; i++)
        {
            float x = 90f + (i % 3) * 4f;
            float y = -2.5f;
            GameObject worm = SpawnSandworm(new Vector3(x, y, 0f), 55f, true, 1.2f);
            worm.SetActive(false);
        }
        MakeObelisk(3, new Vector3(100f, -3.8f, 0f), obeliskClip);

        // Roaming desert vine snakes between the trials
        foreach (float x in new[] { 14f, 44f, 80f, 108f })
            SpawnVineSnake(new Vector3(x, -2.5f, 0f));

        // ── Pyramid reward portal (grants Bow + Magical Armor) ────────────────
        StoryPortal pyramid = MakeStoryPortal("PyramidReward", new Vector3(118f, -2.5f, 0f),
            new Vector2(4f, 6f));
        pyramid.unlocked         = false;
        pyramid.requireKeyPress  = true;
        pyramid.speakerName      = "Ancient Voice";
        pyramid.grantBowAndArmor = true;
        pyramid.lines = new[]
        {
            "You have proven yourself, adventurer.",
            "Take the Magical Armor and the Bow.",
            "The armor will protect you from the sea beast. Now — sail to the Ocean."
        };
        pyramid.nextScene         = "Ocean";
        pyramid.objectiveOnUnlock = "Enter the pyramid";
        pyramid.mysticClip = LoadAudio(Sfx + "Generella sfx/" + Diacritic("Mystiska texter kommer upp.mp3"));

        // Give the player the (dormant) bow + armor components now
        AddPlayerGear(scene);

        AddAmbientSfx(scene, new[]
        {
            LoadAudio(Sfx + "Monster/Sandworm/Sandworm " + Diacritic("ror sig.mp3")),
        }, 6f, 13f);

        SaveScene(scene);
        Debug.Log("[JoA] Level 4 — Desert built.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  LEVEL 5 — THE OCEAN / FINAL BOSS
    // ═════════════════════════════════════════════════════════════════════════
    [MenuItem("JoA/Build Level 5 — Ocean", false, 42)]
    public static void BuildOcean()
    {
        Scene scene = CopyAndOpen(CaveScene, ScenesDir + "Ocean.unity");
        StripCaveObjects(scene);
        SetGlobalLight(new Color(0.55f, 0.6f, 0.85f), 0.8f);

        // Animated storm background (80 frames)
        Sprite[] oceanFrames = LoadTileFrames(L5 + "Ocean map frames/ezgif-split/", 80);
        GameObject bgGO = MakeBackground("OceanBackground",
            oceanFrames.Length > 0 ? oceanFrames[0] : null,
            new Vector3(23f, 3f, 0f), 2.7f, -100);
        if (oceanFrames.Length > 1)
        {
            var sa = bgGO.AddComponent<SpriteAnimator>();
            AddClip(sa, "storm", oceanFrames, 14f, true);
            sa.defaultClip = "storm";
        }

        // ── Platforms over the ocean (wood) ───────────────────────────────────
        Sprite woodNormal = LoadSprite(L5 + "Trä platform frames/" + Diacritic("Tra platform ocean map vanlig.png"));
        Sprite woodRed    = LoadSprite(L5 + "Trä platform frames/" + Diacritic("Tra platform ocean map rod.png"));

        // start + solid platforms
        MakeWoodPlatform("StartPlatform", new Vector3(3f, -2f, 0f), woodNormal, false, null);
        Vector3[] solid =
        {
            new Vector3(10f, -1f, 0f), new Vector3(17f, 0.5f, 0f),
            new Vector3(31f, 0f, 0f),  new Vector3(44f, -1f, 0f),
        };
        int s = 0;
        foreach (Vector3 p in solid) MakeWoodPlatform($"Platform_{s++}", p, woodNormal, false, null);
        // falling platforms (Phase-2 flavour)
        Vector3[] falling =
        {
            new Vector3(24f, 1.5f, 0f), new Vector3(38f, 1f, 0f),
            new Vector3(31f, 3.2f, 0f),
        };
        int f = 0;
        foreach (Vector3 p in falling) MakeWoodPlatform($"FallingPlatform_{f++}", p, woodNormal, true, woodRed);

        MovePlayer(scene, new Vector3(3f, 0f, 0f));
        FixCamera(scene, -2f, 50f, -3f, 14f);

        // ── Water hazard (respawn) ────────────────────────────────────────────
        GameObject startPlat = GameObject.Find("StartPlatform");
        GameObject water = new GameObject("OceanWater");
        water.transform.position = new Vector3(23f, -8f, 0f);
        var wcol = water.AddComponent<BoxCollider2D>();
        wcol.isTrigger = true;
        wcol.size = new Vector2(80f, 8f);
        HazardZone hz = water.AddComponent<HazardZone>();
        SetPrivate(hz, "mode", 1);          // Respawn
        SetPrivate(hz, "damage", 14f);
        if (startPlat != null) hz.respawnPoint = startPlat.transform;

        // ── The Kraken + 3 tentacles ──────────────────────────────────────────
        KrakenBoss kraken = SpawnKraken(new Vector3(34f, 6.5f, 0f));
        KrakenTentacle t1 = SpawnTentacle(new Vector3(25f, 1.5f, 0f));
        KrakenTentacle t2 = SpawnTentacle(new Vector3(33f, 0.5f, 0f));
        KrakenTentacle t3 = SpawnTentacle(new Vector3(41f, 1.5f, 0f));
        kraken.tentacles = new List<KrakenTentacle> { t1, t2, t3 };

        OceanManager mgr = SwapManager<OceanManager>(scene);
        mgr.ambientMusic   = LoadAudio(Sfx + "Banor/Boss map/Boss map ambiance.mp3");
        mgr.combatMusic    = LoadAudio(Sfx + "Banor/Boss map/" + Diacritic("Musik innan boss fighten borjar.mp3"));
        mgr.defaultWeapon  = LoadItem("Assets/DiamondSword.asset");
        mgr.endCreditsScene = "EndCredits";
        SetMusicManagerClips(scene, mgr.ambientMusic, mgr.combatMusic);

        AddPlayerGear(scene);

        AddAmbientSfx(scene, new[]
        {
            LoadAudio(Sfx + "Banor/Boss map/Blixt 1.mp3"),
            LoadAudio(Sfx + "Banor/Boss map/Blixt 2.mp3"),
            LoadAudio(Sfx + "Banor/Boss map/Blixt 3.mp3"),
        }, 4f, 9f);

        SaveScene(scene);
        Debug.Log("[JoA] Level 5 — Ocean built.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  END CREDITS
    // ═════════════════════════════════════════════════════════════════════════
    [MenuItem("JoA/Build End Credits", false, 43)]
    public static void BuildEndCredits()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,
                                                  NewSceneMode.Single);

        // Configure the camera for 2D
        GameObject camGO = FindRoot(scene, "Main Camera");
        if (camGO != null)
        {
            Camera cam = camGO.GetComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 5f;
            cam.backgroundColor  = new Color(0.05f, 0.05f, 0.08f);
            camGO.transform.position = new Vector3(0f, 0f, -10f);
        }
        GameObject dirLight = FindRoot(scene, "Directional Light");
        if (dirLight != null) Object.DestroyImmediate(dirLight);

        // Village background behind the reunion
        Sprite village = LoadSprite("Assets/Sprites/Level1/Level_1_Background.png")
                       ?? LoadSprite("Assets/Sprites/Level1/Level_1_Finalmap.png");
        if (village != null)
        {
            GameObject bg = new GameObject("VillageBackground");
            var sr = bg.AddComponent<SpriteRenderer>();
            sr.sprite = village;
            sr.sortingOrder = -100;
            // scale to comfortably fill the view
            float targetH = 11f;
            float sc = village.bounds.size.y > 0.01f ? targetH / village.bounds.size.y : 1f;
            bg.transform.localScale = new Vector3(sc, sc, 1f);
        }

        GameObject mgrGO = new GameObject("EndCreditsManager");
        EndCreditsManager ecm = mgrGO.AddComponent<EndCreditsManager>();
        ecm.creditsMusic  = LoadAudio(Mus + "village music.mp3");
        ecm.mainMenuScene = "MainMenu";

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenesDir + "EndCredits.unity");
        Debug.Log("[JoA] End Credits built.");
    }

    // ── Build settings ────────────────────────────────────────────────────────
    [MenuItem("JoA/Configure Build Settings", false, 60)]
    public static void ConfigureBuildSettings()
    {
        string[] order =
        {
            ScenesDir + "SplashScene.unity",
            ScenesDir + "MainMenu.unity",
            ScenesDir + "Village.unity",
            ScenesDir + "Cave.unity",
            ScenesDir + "Jungle.unity",
            ScenesDir + "Desert.unity",
            ScenesDir + "Ocean.unity",
            ScenesDir + "EndCredits.unity",
        };
        var list = new List<EditorBuildSettingsScene>();
        foreach (string p in order)
            if (File.Exists(p))
                list.Add(new EditorBuildSettingsScene(p, true));
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[JoA] Build settings configured with {list.Count} scenes.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  SCENE TRANSFORM HELPERS
    // ═════════════════════════════════════════════════════════════════════════
    static Scene CopyAndOpen(string src, string dst)
    {
        if (File.Exists(dst)) AssetDatabase.DeleteAsset(dst);
        AssetDatabase.CopyAsset(src, dst);
        AssetDatabase.Refresh();
        return EditorSceneManager.OpenScene(dst, OpenSceneMode.Single);
    }

    static void SaveScene(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void StripCaveObjects(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            string n = go.name;
            if (n == "CAVE" || n.StartsWith("DiamondRock") || n.StartsWith("Spider_") ||
                n == "CraftingShrine" || n == "Light2D" || n.StartsWith("Light2D (") ||
                n == "Main Camera (1)" || n == "New Game Object" || n == "CaveEntrance")
                Object.DestroyImmediate(go);
        }
    }

    static T SwapManager<T>(Scene scene) where T : LevelManagerBase
    {
        GameObject gm = FindRoot(scene, "GameManager") ?? new GameObject("GameManager");

        var cave = gm.GetComponent<CaveManager>(); if (cave) Object.DestroyImmediate(cave);
        var gman = gm.GetComponent<GameManager>(); if (gman) Object.DestroyImmediate(gman);
        var dbg  = gm.GetComponent<DebugMenu>();   if (dbg)  Object.DestroyImmediate(dbg);

        T existing = gm.GetComponent<T>();
        return existing != null ? existing : gm.AddComponent<T>();
    }

    static void SetMusicManagerClips(Scene scene, AudioClip ambient, AudioClip combat)
    {
        GameObject gm = FindRoot(scene, "GameManager");
        MusicManager mm = gm != null ? gm.GetComponent<MusicManager>() : null;
        if (mm == null) return;
        var so = new SerializedObject(mm);
        var a = so.FindProperty("musicClip");
        var c = so.FindProperty("combatMusicClip");
        if (a != null) a.objectReferenceValue = ambient;
        if (c != null) c.objectReferenceValue = combat;
        so.ApplyModifiedProperties();
    }

    static void SetGlobalLight(Color color, float intensity)
    {
        foreach (Light2D l in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (l.lightType == Light2D.LightType.Global)
            {
                l.color = color;
                l.intensity = intensity;
            }
        }
    }

    static void MovePlayer(Scene scene, Vector3 pos)
    {
        GameObject player = FindRoot(scene, "Player");
        if (player != null) player.transform.position = pos;
    }

    static void FixCamera(Scene scene, float minX, float maxX, float minY, float maxY)
    {
        GameObject cam = FindRoot(scene, "Main Camera");
        if (cam == null) return;
        CameraFollow cf = cam.GetComponent<CameraFollow>() ?? cam.AddComponent<CameraFollow>();
        cf.useBounds = true;
        cf.minX = minX; cf.maxX = maxX; cf.minY = minY; cf.maxY = maxY;
        if (cam.GetComponent<CameraShake>() == null) cam.AddComponent<CameraShake>();
    }

    static void AddPlayerGear(Scene scene)
    {
        GameObject player = FindRoot(scene, "Player");
        if (player == null) return;

        PlayerRanged pr = player.GetComponent<PlayerRanged>() ?? player.AddComponent<PlayerRanged>();
        pr.arrowSprite = LoadSprite(L4 + "arrow .png");
        pr.bowFrames   = new[]
        {
            LoadSprite(L4 + "bow sprite 1.png"), LoadSprite(L4 + "bow sprite 2.png"),
            LoadSprite(L4 + "bow sprite 3.png"), LoadSprite(L4 + "bow sprite 4.png"),
        }.Where(x => x != null).ToArray();
        pr.drawClip  = LoadAudio(Sfx + "Karaktär/Vapen/Pilbåge/" + Diacritic("Pilbage load.mp3"));
        pr.shootClip = LoadAudio(Sfx + "Karaktär/Vapen/Pilbåge/" + Diacritic("Pilbage skjuter.mp3"));

        if (player.GetComponent<MagicalArmor>() == null) player.AddComponent<MagicalArmor>();
    }

    static void AddAmbientSfx(Scene scene, AudioClip[] clips, float min, float max)
    {
        clips = clips.Where(c => c != null).ToArray();
        if (clips.Length == 0) return;
        GameObject go = new GameObject("AmbientSfx");
        AmbientSfxPlayer p = go.AddComponent<AmbientSfxPlayer>();
        p.clips = clips;
        p.minInterval = min;
        p.maxInterval = max;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  GEOMETRY HELPERS
    // ═════════════════════════════════════════════════════════════════════════
    static GameObject MakeBackground(string name, Sprite sprite, Vector3 pos,
                                     float scale, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = sortingOrder;
        return go;
    }

    static void MakeGround(string name, float cx, float cy, float w, float h)
    {
        GameObject go = new GameObject(name);
        go.layer = GroundLayer;
        go.transform.position = new Vector3(cx, cy, 0f);
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(w, h);
    }

    static GameObject MakePlatform(string name, Vector3 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.layer = GroundLayer;
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = ProceduralSprite.Box(Mathf.RoundToInt(size.x * 16f),
                                               Mathf.RoundToInt(size.y * 16f), color);
        sr.sortingOrder = -10;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        return go;
    }

    static void MakeMovingPlatform(string name, Vector3 pos, Vector2 a, Vector2 b)
    {
        GameObject go = MakePlatform(name, pos, new Vector2(3.5f, 0.7f),
                                     new Color(0.7f, 0.55f, 0.3f));
        var mp = go.AddComponent<MovingPlatform>();
        mp.pointA = a; mp.pointB = b; mp.speed = 2.2f;
    }

    static void MakeWoodPlatform(string name, Vector3 pos, Sprite normal,
                                 bool falling, Sprite red)
    {
        GameObject go = new GameObject(name);
        go.layer = GroundLayer;
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = normal;
        sr.sortingOrder = -5;

        // wood platform sprite is 64x64 — scale to a wide, flatter platform
        const float sc = 1.1f;
        go.transform.localScale = new Vector3(sc, sc * 0.55f, 1f);

        var col = go.AddComponent<BoxCollider2D>();
        if (normal != null) col.size = normal.bounds.size;
        else col.size = new Vector2(4f, 4f);

        if (falling)
        {
            FallingPlatform fp = go.AddComponent<FallingPlatform>();
            fp.normalSprite  = normal;
            fp.warningSprite = red;
            fp.respawns      = true;
            fp.respawnTime   = 4f;
            fp.fallClip = LoadAudio(Sfx + "Bossar/Kraken final bossen/Platform faller ner.mp3");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  ENEMY / BOSS SPAWNERS
    // ═════════════════════════════════════════════════════════════════════════
    static GameObject NewEnemyBase(string name, Vector3 pos, Sprite first,
                                   float worldHeight, bool solidCollider)
    {
        GameObject go = new GameObject(name);
        go.layer = EnemyLayer;
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = first;
        sr.sortingOrder = 5;

        float scale = 1f;
        if (first != null && first.bounds.size.y > 0.01f)
            scale = worldHeight / first.bounds.size.y;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        return go;
    }

    static BoxCollider2D AddSizedCollider(GameObject go, Sprite s, float shrink, bool trigger)
    {
        var col = go.AddComponent<BoxCollider2D>();
        if (s != null) col.size = s.bounds.size * shrink;
        else col.size = new Vector2(1f, 1f);
        col.isTrigger = trigger;
        return col;
    }

    static void SpawnVineSnake(Vector3 pos)
    {
        Sprite[] frames =
        {
            LoadSprite(L4 + "vine snake 1.png"), LoadSprite(L4 + "vine snake 2.png"),
            LoadSprite(L4 + "vine snake 3.png"), LoadSprite(L4 + "vine snake 4.png"),
        };
        frames = frames.Where(x => x != null).ToArray();
        Sprite first = frames.Length > 0 ? frames[0] : ProceduralSprite.Circle(16, new Color(0.3f, 0.7f, 0.2f));

        GameObject go = NewEnemyBase("VineSnake", pos, first, 1.2f, true);
        var sa = go.AddComponent<SpriteAnimator>();
        if (frames.Length > 0)
        {
            AddClip(sa, "move", frames, 10f, true);
            AddClip(sa, "idle", new[] { frames[0] }, 1f, true);
            AddClip(sa, "attack", frames.Reverse().ToArray(), 14f, false);
        }
        sa.defaultClip = "move";

        go.AddComponent<Health>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        AddSizedCollider(go, first, 0.7f, false);

        VineSnakeAI ai = go.AddComponent<VineSnakeAI>();
        ai.ambientClip = LoadAudio(Sfx + "Monster/Vine snake/Vine snake ljud 1.mp3");
        ai.attackClip  = LoadAudio(Sfx + "Monster/Vine snake/Vine snake attack 1.mp3");
        ai.hurtClip    = LoadAudio(Sfx + "Monster/Vine snake/Vine snake tar skada 1.mp3");

        go.AddComponent<FloatingHealthBar>();
    }

    static void SpawnMonkey(Vector3 pos)
    {
        Sprite[] mb = MinibossSprites();
        Sprite first = mb.Length > 0 ? mb[0] : ProceduralSprite.Circle(20, new Color(0.5f, 0.32f, 0.15f));

        GameObject go = NewEnemyBase("Monkey", pos, first, 1.4f, true);
        var sa = go.AddComponent<SpriteAnimator>();
        if (mb.Length > 1)
        {
            AddClip(sa, "idle", mb, 6f, true);
            AddClip(sa, "attack", mb, 14f, false);
        }
        else
        {
            AddClip(sa, "idle", new[] { first }, 1f, true);
        }
        AddClip(sa, "hurt", new[] { first }, 1f, false);
        sa.defaultClip = "idle";

        go.AddComponent<Health>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        AddSizedCollider(go, first, 0.75f, false);

        MonkeyAI ai = go.AddComponent<MonkeyAI>();
        ai.attackClip = LoadAudio(Sfx + "Monster/Apa (inte mini bossen)/Monkey attack.mp3");
        ai.hurtClip   = LoadAudio(Sfx + "Monster/Apa (inte mini bossen)/" + Diacritic("Monkey tar skada 1.mp3"));
        ai.deathClip  = LoadAudio(Sfx + "Monster/Apa (inte mini bossen)/" + Diacritic("Monkey dor.mp3"));

        go.AddComponent<FloatingHealthBar>();
    }

    static GameObject SpawnSandworm(Vector3 pos, float hp, bool wave, float worldHeight)
    {
        Sprite[] frames = SandmaskFrames();
        Sprite first = frames.Length > 0 ? frames[0] : ProceduralSprite.Circle(24, new Color(0.85f, 0.7f, 0.45f));

        GameObject go = NewEnemyBase(wave ? "WaveSandworm" : "GreatSandworm",
                                     pos, first, worldHeight, true);
        var sa = go.AddComponent<SpriteAnimator>();
        if (frames.Length > 1)
        {
            AddClip(sa, "move", frames, 12f, true);
            AddClip(sa, "attack", frames, 18f, false);
        }
        else AddClip(sa, "move", new[] { first }, 1f, true);
        sa.defaultClip = "move";

        go.AddComponent<Health>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        AddSizedCollider(go, first, 0.75f, false);

        SandwormAI ai = go.AddComponent<SandwormAI>();
        ai.countsAsWaveEnemy = wave;
        ai.moveClip   = LoadAudio(Sfx + "Monster/Sandworm/Sandworm " + Diacritic("ror sig.mp3"));
        ai.attackClip = LoadAudio(Sfx + "Monster/Sandworm/Sandworm attack 1.mp3");
        ai.Configure(hp, wave);

        go.AddComponent<FloatingHealthBar>();
        return go;
    }

    static void SpawnJungleGuardian(Vector3 pos)
    {
        Sprite[] mb = MinibossSprites();
        Sprite first = mb.Length > 0 ? mb[0] : ProceduralSprite.Circle(48, new Color(0.45f, 0.28f, 0.12f));

        GameObject go = NewEnemyBase("JungleGuardian", pos, first, 3.2f, true);
        go.name = "JungleGuardian";
        var sa = go.AddComponent<SpriteAnimator>();
        if (mb.Length > 1)
        {
            AddClip(sa, "idle", mb, 5f, true);
            AddClip(sa, "move", mb, 9f, true);
            AddClip(sa, "attack", mb, 16f, false);
        }
        else
        {
            AddClip(sa, "idle", new[] { first }, 1f, true);
            AddClip(sa, "move", new[] { first }, 1f, true);
        }
        AddClip(sa, "hurt", new[] { first }, 1f, false);
        sa.defaultClip = "idle";

        go.AddComponent<Health>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        AddSizedCollider(go, first, 0.8f, false);

        JungleGuardian g = go.AddComponent<JungleGuardian>();
        g.attackClip = LoadAudio(Sfx + "Bossar/Apa mini bossen/Miniboss attack 1.mp3");
        g.hurtClip   = LoadAudio(Sfx + "Bossar/Apa mini bossen/Miniboss tar skada 2.mp3");
        g.deathClip  = LoadAudio(Sfx + "Bossar/Apa mini bossen/" + Diacritic("Miniboss dor.mp3"));
    }

    static KrakenBoss SpawnKraken(Vector3 pos)
    {
        Sprite[] body =
        {
            LoadSprite(L5 + "Kraken/kraken background 1.png"),
            LoadSprite(L5 + "Kraken/kraken background 2.png"),
        };
        body = body.Where(x => x != null).ToArray();
        Sprite first = body.Length > 0 ? body[0] : ProceduralSprite.Circle(120, new Color(0.4f, 0.2f, 0.5f));

        GameObject go = NewEnemyBase("Kraken", pos, first, 11f, false);
        go.name = "Kraken";
        var sa = go.AddComponent<SpriteAnimator>();
        if (body.Length > 1) AddClip(sa, "idle", body, 2.5f, true);
        else AddClip(sa, "idle", new[] { first }, 1f, true);
        sa.defaultClip = "idle";

        go.AddComponent<Health>();
        BoxCollider2D bodyCol = AddSizedCollider(go, first, 0.55f, true);

        KrakenBoss kb = go.AddComponent<KrakenBoss>();
        kb.bodyCollider = bodyCol;
        kb.talkClip          = LoadAudio(Sfx + "Bossar/Kraken final bossen/Boss pratar.mp3");
        kb.hurtClip          = LoadAudio(Sfx + "Bossar/Kraken final bossen/Boss tar skada.mp3");
        kb.energyClip        = LoadAudio(Sfx + "Bossar/Kraken final bossen/Boss energi attack.mp3");
        kb.waveClip          = LoadAudio(Sfx + "Bossar/Kraken final bossen/Boss wave attack.mp3");
        kb.loseTentacleClip  = LoadAudio(Sfx + "Bossar/Kraken final bossen/" + Diacritic("Boss forlorar tentaklar.mp3"));
        kb.deathClip         = LoadAudio(Sfx + "Bossar/Kraken final bossen/" + Diacritic("Boss dor.mp3"));
        return kb;
    }

    static KrakenTentacle SpawnTentacle(Vector3 pos)
    {
        Sprite[] slap = LoadTileFrames(L5 + "Kraken/Kraken slap animation/", 8);
        Sprite[] whip = LoadTileFrames(L5 + "Kraken/Kraken whip animation/", 11);
        Sprite first = slap.Length > 0 ? slap[0]
                     : (whip.Length > 0 ? whip[0]
                     : ProceduralSprite.Circle(48, new Color(0.4f, 0.2f, 0.5f)));

        GameObject go = NewEnemyBase("KrakenTentacle", pos, first, 6f, false);
        var sa = go.AddComponent<SpriteAnimator>();
        if (slap.Length > 0) AddClip(sa, "idle", new[] { slap[0] }, 1f, true);
        if (slap.Length > 1) AddClip(sa, "attack", slap, 12f, false);
        else if (whip.Length > 1) AddClip(sa, "attack", whip, 12f, false);
        sa.defaultClip = "idle";

        go.AddComponent<Health>();
        AddSizedCollider(go, first, 0.6f, true);

        KrakenTentacle kt = go.AddComponent<KrakenTentacle>();
        kt.attackClip = LoadAudio(Sfx + "Bossar/Kraken final bossen/Boss tantakel attack.mp3");
        kt.hurtClip   = LoadAudio(Sfx + "Bossar/Kraken final bossen/Boss tar skada.mp3");
        kt.deathClip  = LoadAudio(Sfx + "Bossar/Kraken final bossen/" + Diacritic("Boss forlorar tentaklar.mp3"));
        return kt;
    }

    static void MakeObelisk(int index, Vector3 pos, AudioClip activateClip)
    {
        GameObject go = new GameObject($"Obelisk_{index}");
        go.transform.position = pos;
        Obelisk ob = go.AddComponent<Obelisk>();
        ob.index = index;
        ob.activateClip = activateClip;
    }

    static StoryPortal MakeStoryPortal(string name, Vector3 pos, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = size;
        return go.AddComponent<StoryPortal>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  ASSET LOADING
    // ═════════════════════════════════════════════════════════════════════════
    static Sprite LoadSprite(string path)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null && File.Exists(path))
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite sp) return sp;
        }
        if (s == null) Debug.LogWarning($"[JoA] Sprite not found: {path}");
        return s;
    }

    static Sprite[] LoadTileFrames(string folder, int count)
    {
        var list = new List<Sprite>();
        for (int i = 0; i < count; i++)
        {
            string p = folder + "tile" + i.ToString("D3") + ".png";
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s != null) list.Add(s);
        }
        return list.ToArray();
    }

    static Sprite[] _minibossCache;
    static Sprite[] MinibossSprites()
    {
        if (_minibossCache != null) return _minibossCache;
        var list = new List<Sprite>();
        string path = L3 + "miniboss.ase";
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite sp) list.Add(sp);
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        _minibossCache = list.ToArray();
        if (_minibossCache.Length == 0)
            Debug.LogWarning("[JoA] miniboss.ase produced no sprites — using a placeholder.");
        return _minibossCache;
    }

    static Sprite[] SandmaskFrames()
    {
        var list = new List<Sprite>();
        string folder = L4 + "Sandmask/";
        Sprite f1 = AssetDatabase.LoadAssetAtPath<Sprite>(folder + "Sandmask1.png");
        if (f1 != null) list.Add(f1);
        for (int i = 2; i <= 12; i++)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(folder + "Sandmask " + i + ".png");
            if (s != null) list.Add(s);
        }
        return list.ToArray();
    }

    static AudioClip LoadAudio(string path)
    {
        AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (c == null) Debug.LogWarning($"[JoA] Audio not found: {path}");
        return c;
    }

    static ItemData LoadItem(string path) => AssetDatabase.LoadAssetAtPath<ItemData>(path);

    // ═════════════════════════════════════════════════════════════════════════
    //  SMALL UTILITIES
    // ═════════════════════════════════════════════════════════════════════════
    static void AddClip(SpriteAnimator sa, string name, Sprite[] frames, float fps, bool loop)
    {
        if (frames == null || frames.Length == 0) return;
        sa.clips.Add(new SpriteAnimator.Clip { name = name, frames = frames, fps = fps, loop = loop });
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static void SetPrivate(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) return;
        if (value is int i) p.intValue = i;
        else if (value is float f) p.floatValue = f;
        else if (value is bool b) p.boolValue = b;
        else if (value is string s) p.stringValue = s;
        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// Resolves a file name that contains Swedish characters. The build code is
    /// written with ASCII placeholders (o, a) and this swaps in the real glyphs
    /// (ö, ä, å) so AssetDatabase finds the on-disk file regardless of how the
    /// source file encodes this string.
    /// </summary>
    static string Diacritic(string asciiName)
    {
        return asciiName
            .Replace("Oppnar",  "Öppnar")
            .Replace("Oken",    "Öken")
            .Replace("Gar in",  "Går in")
            .Replace("ror sig", "rör sig")
            .Replace("Tra ",    "Trä ")
            .Replace("rod",     "röd")
            .Replace("Pilbage", "Pilbåge")
            .Replace("dor",     "dör")
            .Replace("borjar",  "börjar")
            .Replace("forlorar","förlorar")
            .Replace("skada",   "skada");
    }
}
