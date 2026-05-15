using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Editor tool that fully rebuilds the Cave scene from scratch using real textures.
/// Run via: Tools > Cave Scene Setup (Rebuild)
/// </summary>
public class CaveSceneSetup : Editor
{
    // ── Texture paths (using existing Level 1 textures, dark-tinted for cave) ──
    const string GROUND_PATH    = "Assets/Sprites/Level1/Level_1_Villageground.png";
    const string FOLIAGE_PATH   = "Assets/Sprites/Level1/Level_1_Foliage.png";
    const string TREE_PATH      = "Assets/Sprites/Level1/Level_1_Tree3.png";
    const string SPIDER_HANG    = "Assets/Sprites/Spider/Spider_hang1.png";
    const string SPIDER_WALK1   = "Assets/Sprites/Spider/Spider_walk1.png";
    const string SPIDER_WALK2   = "Assets/Sprites/Spider/Spider_walk2.png";
    const string CAVE_MUSIC     = "Assets/Audio/Music/Cave ambiance.mp3";

    // Cave color tones
    static readonly Color CAVE_WALL_TINT   = new Color(0.35f, 0.32f, 0.45f);   // dark purple-grey
    static readonly Color CAVE_FLOOR_TINT  = new Color(0.45f, 0.38f, 0.55f);   // slightly lighter floor
    static readonly Color DIAMOND_COLOR    = new Color(0.45f, 0.85f, 1f);
    static readonly Color SHRINE_LOCKED    = new Color(0.35f, 0.32f, 0.4f);
    static readonly Color TORCH_COLOR      = new Color(1f, 0.7f, 0.35f);

    [MenuItem("Tools/Cave Scene Setup (Rebuild)")]
    static void RebuildCave()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (!active.name.Equals("Cave"))
        {
            EditorUtility.DisplayDialog("Cave Setup",
                "Open Assets/Scenes/Cave.unity first, then run this command.", "OK");
            return;
        }

        // ── 1. Clean out anything from a previous build ────────────────────
        ClearLevelGeometry();

        // Set camera background near-black
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            // Update bounds for cave width
            var cf = cam.GetComponent<CameraFollow>();
            if (cf != null)
            {
                var so = new SerializedObject(cf);
                so.FindProperty("minX").floatValue = -18f;
                so.FindProperty("maxX").floatValue = 56f;
                so.FindProperty("minY").floatValue = -10f;
                so.FindProperty("maxY").floatValue = 2f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ── 2. Load textures ───────────────────────────────────────────────
        Sprite groundSprite  = AssetDatabase.LoadAssetAtPath<Sprite>(GROUND_PATH);
        Sprite foliageSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FOLIAGE_PATH);
        Sprite treeSprite    = AssetDatabase.LoadAssetAtPath<Sprite>(TREE_PATH);
        Sprite spiderHang    = AssetDatabase.LoadAssetAtPath<Sprite>(SPIDER_HANG);
        Sprite spiderW1      = AssetDatabase.LoadAssetAtPath<Sprite>(SPIDER_WALK1);
        Sprite spiderW2      = AssetDatabase.LoadAssetAtPath<Sprite>(SPIDER_WALK2);

        if (groundSprite == null) Debug.LogWarning($"Missing ground sprite at {GROUND_PATH}");
        if (spiderHang == null)   Debug.LogWarning($"Missing spider sprite at {SPIDER_HANG}");

        // Make sure spider sprites are point-filtered for crisp pixel art
        EnsurePixelArtImport(SPIDER_HANG);
        EnsurePixelArtImport(SPIDER_WALK1);
        EnsurePixelArtImport(SPIDER_WALK2);

        // ── 3. Build a parent for cave geometry ────────────────────────────
        GameObject geoRoot = new GameObject("CaveGeometry");

        // ── 4. Floor — tiled village ground across the cave ───────────────
        BuildFloor(geoRoot.transform, groundSprite);

        // ── 5. Ceiling — dark foliage above ────────────────────────────────
        BuildCeiling(geoRoot.transform, foliageSprite);

        // ── 6. Walls (entrance and exit) ───────────────────────────────────
        BuildWall(geoRoot.transform, new Vector3(-20f, -3f, 0f), new Vector3(2f, 12f, 1f), groundSprite);
        BuildWall(geoRoot.transform, new Vector3( 58f, -3f, 0f), new Vector3(2f, 12f, 1f), groundSprite);

        // ── 7. Pillars and elevated platforms ──────────────────────────────
        BuildPillar(geoRoot.transform, new Vector3( -3f, -6f, 0f), treeSprite);
        BuildPillar(geoRoot.transform, new Vector3( 17f, -6f, 0f), treeSprite);
        BuildPillar(geoRoot.transform, new Vector3( 32f, -6f, 0f), treeSprite);

        BuildPlatform(geoRoot.transform, new Vector3(  9f, -4.5f, 0f), 6f, groundSprite);
        BuildPlatform(geoRoot.transform, new Vector3( 24f, -3.5f, 0f), 6f, groundSprite);

        // ── 8. Stalactites (decorative tinted foliage hanging from ceiling) ─
        float[] stalX = { -14f, -7f, 2f, 11f, 21f, 30f, 41f, 50f };
        foreach (float x in stalX)
            BuildStalactite(geoRoot.transform, new Vector3(x, 0.5f, 0f), foliageSprite);

        // ── 9. Diamond rocks (5 spread out) ────────────────────────────────
        ItemData diamondItem      = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Diamond.asset");
        ItemData diamondSwordItem = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/DiamondSword.asset");

        Vector3[] diamondPositions = {
            new Vector3(-11f, -6.5f, 0f),
            new Vector3(  1f, -6.5f, 0f),
            new Vector3(  9f, -3.2f, 0f),  // on first elevated platform
            new Vector3( 24f, -2.2f, 0f),  // on second elevated platform
            new Vector3( 38f, -6.5f, 0f)
        };
        for (int i = 0; i < diamondPositions.Length; i++)
            CreateDiamondRock(diamondPositions[i], i + 1, diamondItem);

        // ── 10. Spiders (5, hanging from ceiling) ──────────────────────────
        Vector3[] spiderPositions = {
            new Vector3(-7f,  0.2f, 0f),
            new Vector3( 5f,  0.2f, 0f),
            new Vector3(15f,  0.2f, 0f),
            new Vector3(28f,  0.2f, 0f),
            new Vector3(42f,  0.2f, 0f)
        };
        for (int i = 0; i < spiderPositions.Length; i++)
            CreateSpider(spiderPositions[i], i + 1, spiderHang);

        // ── 11. Crafting shrine at the end ────────────────────────────────
        CreateCraftingShrine(new Vector3(52f, -6.4f, 0f), diamondSwordItem);

        // ── 12. Torches for atmosphere (visual point lights) ──────────────
        float[] torchX = { -14f, -4f, 6f, 16f, 28f, 40f, 50f };
        foreach (float x in torchX)
            CreateTorch(geoRoot.transform, new Vector3(x, -3f, 0f));

        // Also a strong torch beside the shrine to highlight it
        CreateTorch(geoRoot.transform, new Vector3(50f, -5f, 0f), strong: true);

        // ── 13. Global cave darkness — add a Global 2D Light if present ───
        AddGlobal2DLight();

        // ── 14. Player spawn position ──────────────────────────────────────
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            player.transform.position = new Vector3(-16f, -6f, 0f);

        // ── 15. Cave music on the GameManager MusicManager ────────────────
        AudioClip caveMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(CAVE_MUSIC);
        GameObject gmGO = GameObject.Find("GameManager");
        if (gmGO != null && caveMusic != null)
        {
            MusicManager mm = gmGO.GetComponent<MusicManager>();
            if (mm != null)
            {
                var so = new SerializedObject(mm);
                so.FindProperty("musicClip").objectReferenceValue = caveMusic;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorSceneManager.MarkSceneDirty(active);
        EditorSceneManager.SaveScene(active);
        Debug.Log("[CaveSetup] Rebuild complete! Cave scene saved.");
    }

    // ─────────────────────────────────────────────────────────────────────
    //  CLEAR PREVIOUS BUILD
    // ─────────────────────────────────────────────────────────────────────
    static void ClearLevelGeometry()
    {
        var allRoots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
        var toDelete = new List<GameObject>();
        foreach (GameObject go in allRoots)
        {
            string n = go.name.ToLower();
            // Old procedural geometry from previous setup
            if (go.name == "Ground" || go.name == "WallLeft" || go.name == "WallRight" ||
                go.name == "Ceiling" || go.name == "Pillar" || go.name == "Platform_Mid" ||
                go.name == "CaveGeometry")
            { toDelete.Add(go); continue; }

            // Diamond rocks / spiders / shrine from previous setup
            if (n.StartsWith("diamondrock_") || n.StartsWith("spider_") ||
                n == "craftingshrine" || n == "torch")
            { toDelete.Add(go); }

            // Village-leftover stuff
            if (n.Contains("zombie") || n.Contains("house") || n.Contains("villager") ||
                n.Contains("village") || n.Contains("tree") || n.Contains("foliage") ||
                n.Contains("bush") || n.Contains("well") || n == "introcontroller")
            { toDelete.Add(go); }
        }
        foreach (GameObject go in toDelete)
            if (go != null) Object.DestroyImmediate(go);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  TERRAIN BUILDERS
    // ─────────────────────────────────────────────────────────────────────
    static void BuildFloor(Transform parent, Sprite groundSprite)
    {
        // Tile floor sprites across the cave (x = -19 to 57)
        for (int x = -19; x <= 57; x += 4)
        {
            GameObject g = new GameObject("Floor");
            g.transform.SetParent(parent);
            g.transform.position = new Vector3(x, -7.8f, 0f);
            g.transform.localScale = new Vector3(4f, 1.8f, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = groundSprite;
            sr.color = CAVE_FLOOR_TINT;
            sr.sortingOrder = -2;
            var bc = g.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one;
        }

        // Solid floor collider beneath (catches anything falling through tiles)
        GameObject bigFloor = new GameObject("FloorCollider");
        bigFloor.transform.SetParent(parent);
        bigFloor.transform.position = new Vector3(19f, -9.5f, 0f);
        var bf = bigFloor.AddComponent<BoxCollider2D>();
        bf.size = new Vector2(80f, 1f);
    }

    static void BuildCeiling(Transform parent, Sprite tex)
    {
        for (int x = -19; x <= 57; x += 4)
        {
            GameObject g = new GameObject("Ceiling");
            g.transform.SetParent(parent);
            g.transform.position = new Vector3(x, 2.2f, 0f);
            g.transform.localScale = new Vector3(4f, 1.5f, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = tex;
            sr.color = CAVE_WALL_TINT;
            sr.sortingOrder = -1;
            sr.flipY = true;
            var bc = g.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one;
        }
    }

    static void BuildWall(Transform parent, Vector3 pos, Vector3 size, Sprite tex)
    {
        GameObject g = new GameObject("Wall");
        g.transform.SetParent(parent);
        g.transform.position   = pos;
        g.transform.localScale = size;
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = tex;
        sr.color = CAVE_WALL_TINT * 0.85f;
        sr.sortingOrder = -1;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(1f, 1f);
        var bc = g.AddComponent<BoxCollider2D>();
        bc.size = Vector2.one;
    }

    static void BuildPillar(Transform parent, Vector3 pos, Sprite tex)
    {
        GameObject g = new GameObject("Pillar");
        g.transform.SetParent(parent);
        g.transform.position   = pos;
        g.transform.localScale = new Vector3(1.2f, 4f, 1f);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = tex;
        sr.color  = CAVE_WALL_TINT * 0.7f;
        sr.sortingOrder = -1;
        var bc = g.AddComponent<BoxCollider2D>();
        bc.size = Vector2.one;
    }

    static void BuildPlatform(Transform parent, Vector3 pos, float width, Sprite groundSprite)
    {
        GameObject g = new GameObject("Platform");
        g.transform.SetParent(parent);
        g.transform.position   = pos;
        g.transform.localScale = new Vector3(width, 0.7f, 1f);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = groundSprite;
        sr.color  = CAVE_FLOOR_TINT * 0.95f;
        sr.sortingOrder = 0;
        var bc = g.AddComponent<BoxCollider2D>();
        bc.size = Vector2.one;
    }

    static void BuildStalactite(Transform parent, Vector3 pos, Sprite tex)
    {
        GameObject g = new GameObject("Stalactite");
        g.transform.SetParent(parent);
        g.transform.position   = pos;
        g.transform.localScale = new Vector3(0.8f, 1.6f, 1f);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = tex;
        sr.color  = CAVE_WALL_TINT * 0.6f;
        sr.flipY = true;
        sr.sortingOrder = 0;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  GAMEPLAY OBJECTS
    // ─────────────────────────────────────────────────────────────────────
    static void CreateDiamondRock(Vector3 pos, int idx, ItemData diamond)
    {
        GameObject go = new GameObject($"DiamondRock_{idx}");
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateGemSprite(DIAMOND_COLOR);
        sr.sortingOrder = 2;

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = Vector2.one;

        var rock = go.AddComponent<DiamondRock>();
        var so = new SerializedObject(rock);
        so.FindProperty("diamondItem").objectReferenceValue = diamond;
        so.FindProperty("intactColor").colorValue   = DIAMOND_COLOR;
        so.FindProperty("crackedColor").colorValue  = new Color(0.3f, 0.65f, 0.95f);
        so.FindProperty("breakingColor").colorValue = new Color(0.2f, 0.45f, 0.8f);
        so.ApplyModifiedPropertiesWithoutUndo();

        // Add a small URP 2D light to make diamonds glow
        AddPointLight2D(go.transform, Color.cyan, 2f, 0.5f);
    }

    static void CreateSpider(Vector3 pos, int idx, Sprite hangSprite)
    {
        GameObject go = new GameObject($"Spider_{idx}");
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(0.4f, 0.4f, 1f);   // sprite is 60x43 px @ PPU 16

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = hangSprite;
        sr.color  = Color.white;
        sr.sortingOrder = 3;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(0.6f, 0.4f);

        var hp = go.AddComponent<Health>();
        var hpSo = new SerializedObject(hp);
        hpSo.FindProperty("maxHealth").floatValue     = 30f;
        hpSo.FindProperty("currentHealth").floatValue = 30f;
        hpSo.ApplyModifiedPropertiesWithoutUndo();

        var ai = go.AddComponent<SpiderAI>();
        var aiSo = new SerializedObject(ai);
        aiSo.FindProperty("spriteRenderer").objectReferenceValue = sr;
        aiSo.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateCraftingShrine(Vector3 pos, ItemData diamondSword)
    {
        GameObject go = new GameObject("CraftingShrine");
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(1.6f, 2.4f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite(SHRINE_LOCKED);
        sr.sortingOrder = 2;

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size      = Vector2.one;
        bc.isTrigger = true;

        var sh = go.AddComponent<CraftingShrine>();
        var so = new SerializedObject(sh);
        so.FindProperty("diamondSwordItem").objectReferenceValue = diamondSword;
        so.FindProperty("spriteRenderer").objectReferenceValue   = sr;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Glowing light on top of shrine
        AddPointLight2D(go.transform, new Color(0.4f, 0.9f, 1f), 4f, 0.8f);
    }

    static void CreateTorch(Transform parent, Vector3 pos, bool strong = false)
    {
        GameObject g = new GameObject("Torch");
        g.transform.SetParent(parent);
        g.transform.position = pos;
        AddPointLight2D(g.transform, TORCH_COLOR, strong ? 6f : 3.5f, strong ? 1.1f : 0.85f);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  LIGHTING
    // ─────────────────────────────────────────────────────────────────────
    static void AddPointLight2D(Transform parent, Color color, float radius, float intensity)
    {
        GameObject go = new GameObject("Light2D");
        go.transform.SetParent(parent, false);
        var l = go.AddComponent<Light2D>();
        l.lightType         = Light2D.LightType.Point;
        l.color             = color;
        l.intensity         = intensity;
        l.pointLightOuterRadius = radius;
        l.pointLightInnerRadius = radius * 0.2f;
    }

    static void AddGlobal2DLight()
    {
        // Check if a global light already exists
        foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global)
            {
                l.color     = new Color(0.4f, 0.4f, 0.55f);
                l.intensity = 0.35f;
                return;
            }

        GameObject g = new GameObject("GlobalLight2D");
        var gl = g.AddComponent<Light2D>();
        gl.lightType = Light2D.LightType.Global;
        gl.color     = new Color(0.4f, 0.4f, 0.55f);
        gl.intensity = 0.35f;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  SPRITE HELPERS
    // ─────────────────────────────────────────────────────────────────────
    static Sprite CreateSquareSprite(Color color)
    {
        Texture2D tex = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    static Sprite CreateGemSprite(Color color)
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Diamond shape (rotated square)
                float dx = Mathf.Abs(x - center.x);
                float dy = Mathf.Abs(y - center.y);
                if (dx + dy <= size * 0.4f)
                {
                    // Add some brightness variation
                    float bright = 1f - ((dx + dy) / (size * 0.4f));
                    pixels[y * size + x] = color * (0.7f + bright * 0.5f);
                }
                else pixels[y * size + x] = new Color(0,0,0,0);
            }
        }
        tex.SetPixels(pixels);
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static void EnsurePixelArtImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        bool changed = false;
        if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
        if (importer.spritePixelsPerUnit != 16f) { importer.spritePixelsPerUnit = 16f; changed = true; }
        if (changed) importer.SaveAndReimport();
    }
}
