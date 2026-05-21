using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;   // Light2D
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Singleton manager for the Dark Cave level (Level 2).
/// Fully self-bootstrapping: removes Village leftovers, gives the starting weapon,
/// spawns spiders if none exist, sets the objective, and handles game-over.
/// No Inspector wiring required.
/// </summary>
public class CaveManager : MonoBehaviour
{
    public static CaveManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int      diamondsRequired  = 5;
    [SerializeField] private int      spidersToSpawn    = 5;
    [SerializeField] private ItemData fallbackWeapon;   // Rusty Sword — assign in Inspector

    // Auto-found at runtime
    private Health     _playerHealth;
    private GameObject _gameOverUI;
    private Button     _restartButton;
    private ItemData   _startingWeapon;

    private int  _diamondsCollected;
    private bool _shrineUnlocked;
    private bool _isGameOver;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        DisableVillageLeftovers();
    }

    void Start()
    {
        // ── Auto-find player ──────────────────────────────────────────────
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            _playerHealth = p.GetComponent<Health>() ?? p.GetComponentInChildren<Health>();
            // Safety: if player fell out of bounds during scene transition, snap to spawn
            if (p.transform.position.y < -8f)
            {
                GameObject spawn = GameObject.Find("PlayerSpawn") ?? GameObject.Find("SpawnPoint");
                p.transform.position = spawn != null
                    ? spawn.transform.position
                    : new Vector3(-6.5f, -5.5f, 0f);
            }
        }
        if (_playerHealth != null)
        {
            _playerHealth.OnDeath.AddListener(OnPlayerDeath);
            _playerHealth.OnDamageTaken.AddListener(_ => TriggerDamagePulse());
        }

        // ── Auto-find game-over UI ────────────────────────────────────────
        AutoFindGameOverUI();
        if (_gameOverUI != null) _gameOverUI.SetActive(false);
        if (_restartButton != null)
            _restartButton.onClick.AddListener(RestartLevel);

        // ── Hide any leftover dialog panel ────────────────────────────────
        DialogUI dialog = FindFirstObjectByType<DialogUI>(FindObjectsInactive.Include);
        if (dialog != null) dialog.Hide();

        // ── Apply save-game state only when explicitly loading from main menu.
        // During a normal new-game progression, always start Cave with full health.
        if (SaveManager.Instance != null && SaveManager.Instance.IsLoadingFromSave)
        {
            SaveManager.Instance.ApplySavedState();
            SaveManager.Instance.ConfirmLoadApplied();
        }
        else
        {
            // Fresh level start — reset health so cave doesn't inherit village damage
            Health hp = p?.GetComponent<Health>() ?? p?.GetComponentInChildren<Health>();
            hp?.ResetHealth();
            SaveManager.Instance?.SaveGame();
        }

        // ── Give the player a starting weapon if they have none ───────────
        StartCoroutine(EquipNextFrame());

        // ── Spawn spiders if the scene has none ───────────────────────────
        SpawnSpidersIfNeeded();

        // ── Force the correct objective ───────────────────────────────────
        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.ShowObjective($"Collect diamonds (0 / {diamondsRequired})");

        Time.timeScale = 1f;
    }

    void Update()
    {
        if (_isGameOver) return;
        if (_playerHealth != null && _playerHealth.IsDead) OnPlayerDeath();
    }

    // ── Diamonds & Shrine ─────────────────────────────────────────────────────
    public void OnDiamondCollected()
    {
        _diamondsCollected++;
        int remaining = diamondsRequired - _diamondsCollected;

        if (remaining > 0)
        {
            ObjectiveManager.Instance?.UpdateObjective(
                $"Collect diamonds ({_diamondsCollected} / {diamondsRequired})");
        }
        else if (!_shrineUnlocked)
        {
            _shrineUnlocked = true;
            ObjectiveManager.Instance?.UpdateObjective("Find the Crafting Shrine");

            foreach (CraftingShrine s in FindObjectsByType<CraftingShrine>(FindObjectsSortMode.None))
                s.Unlock();
        }
    }

    public int DiamondsCollected => _diamondsCollected;
    public int DiamondsRequired  => diamondsRequired;

    // ── Death / Game Over ─────────────────────────────────────────────────────
    private void OnPlayerDeath()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        Time.timeScale = 0f;
        MusicManager.Instance?.Stop();

        foreach (SpiderAI s in FindObjectsByType<SpiderAI>(FindObjectsSortMode.None))
            s.enabled = false;

        // Full-screen black tint on its own canvas (sortingOrder 998) covers all HUD
        BuildDeathTintCanvas();

        if (_gameOverUI != null)
        {
            _gameOverUI.SetActive(true);
            // Move game over UI canvas above the tint
            Canvas gc = _gameOverUI.GetComponentInParent<Canvas>();
            if (gc != null) gc.sortingOrder = 999;
            _gameOverUI.transform.SetAsLastSibling();
            EnsureDeathButtons(_gameOverUI.transform);
        }
        else
        {
            StartCoroutine(ShowFallbackGameOver());
        }
    }

    private void EnsureDeathButtons(Transform parent)
    {
        // If a Button already exists (wired in scene), wire it up and return
        Button existing = parent.GetComponentInChildren<Button>();
        if (existing != null && _restartButton == null)
        {
            existing.onClick.AddListener(RestartLevel);
            _restartButton = existing;
            return;
        }
        if (_restartButton != null) return;

        TMP_FontAsset font = FontEnforcer.Font;

        // Restart button
        GameObject restartGO = BuildButton(parent, "RestartBtn",
            new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.45f),
            "Try Again", new Color(0.7f, 0.1f, 0.1f), font);
        _restartButton = restartGO.GetComponent<Button>();
        _restartButton.onClick.AddListener(RestartLevel);

        // Main menu button
        GameObject menuGO = BuildButton(parent, "MainMenuBtn",
            new Vector2(0.5f, 0.14f), new Vector2(0.5f, 0.28f),
            "Main Menu", new Color(0.15f, 0.15f, 0.4f), font);
        menuGO.GetComponent<Button>().onClick.AddListener(GoToMainMenu);
    }

    private static GameObject BuildButton(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, string label, Color bg, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var tRt = txtGO.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 9f;
        tmp.color = Color.white;
        if (font != null) tmp.font = font;
        return go;
    }

    private void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    // ── Village cleanup ───────────────────────────────────────────────────────
    private void DisableVillageLeftovers()
    {
        foreach (var v in FindObjectsByType<VillagerNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            v.gameObject.SetActive(false);
        foreach (var v in FindObjectsByType<Villager2NPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            v.gameObject.SetActive(false);
        foreach (var sp in FindObjectsByType<SwordPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sp.gameObject.SetActive(false);
        foreach (var z in FindObjectsByType<ZombieAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            z.gameObject.SetActive(false);
        foreach (var gm in FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            gm.enabled = false;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            Transform t = FindDeep(c.transform, "TutorialText");
            if (t != null) t.gameObject.SetActive(false);
        }
    }

    // ── Starting weapon ───────────────────────────────────────────────────────
    private IEnumerator EquipNextFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        EquipStartingWeapon();
    }

    private void EquipStartingWeapon()
    {
        if (Inventory.Instance == null) return;

        // If the player already has a weapon, don't overwrite
        ItemData[] hotbar = Inventory.Instance.GetHotbarItems();
        foreach (var it in hotbar)
            if (it != null && it.itemType == ItemType.Weapon) return;

        // Find the fallback weapon (Rusty Sword)
        _startingWeapon = fallbackWeapon;
        if (_startingWeapon == null) _startingWeapon = Resources.Load<ItemData>("RustySword");
        if (_startingWeapon == null)
        {
            foreach (ItemData id in Resources.FindObjectsOfTypeAll<ItemData>())
            {
                if (id == null) continue;
                if (id.itemName == "Rusty Sword" || id.name == "RustySword")
                { _startingWeapon = id; break; }
            }
        }
        if (_startingWeapon == null) return;

        // If hotbar is full (e.g. Map in slot 0), clear non-weapon items to make room
        bool added = Inventory.Instance.AddItem(_startingWeapon);
        if (!added)
        {
            // Replace slot 0 with weapon
            Inventory.Instance.RemoveItem(0);
            Inventory.Instance.AddItem(_startingWeapon);
        }
        Inventory.Instance.ToggleEquip(0);
    }

    // ── Spider spawning ───────────────────────────────────────────────────────
    private void SpawnSpidersIfNeeded()
    {
        if (FindObjectsByType<SpiderAI>(FindObjectsSortMode.None).Length >= spidersToSpawn) return;

        Sprite spider = Resources.Load<Sprite>("Spider/spideranimation1")
                     ?? Resources.Load<Sprite>("spideranimation1");
        if (spider == null)
        {
            foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
                if (s != null && (s.name.StartsWith("spideranimation1") || s.name.StartsWith("Spider")))
                { spider = s; break; }
        }

        Vector2[] spots = {
            new Vector2(-4f,  3.5f),
            new Vector2( 2f,  3.5f),
            new Vector2( 8f,  3.5f),
            new Vector2(14f,  3.5f),
            new Vector2(20f,  3.5f),
        };

        int existing = FindObjectsByType<SpiderAI>(FindObjectsSortMode.None).Length;
        for (int i = existing; i < spidersToSpawn && i < spots.Length; i++)
            CreateSpider(spider, spots[i], i + 1);
    }

    private void CreateSpider(Sprite sprite, Vector2 pos, int index)
    {
        GameObject go = new GameObject($"Spider_{index}");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        if (sprite != null) sr.sprite = sprite;
        sr.sortingOrder = 3;

        var col = go.AddComponent<BoxCollider2D>();
        col.size   = new Vector2(1.2f, 0.8125f);
        col.offset = new Vector2(0f, 0.1f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        go.AddComponent<Health>();
        go.AddComponent<SpiderAI>();

        GameObject lightGO = new GameObject("Light2D");
        lightGO.transform.SetParent(go.transform, false);
        var l = lightGO.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = new Color(1f, 0.3f, 0.3f);
        l.intensity = 0.4f;
        l.pointLightOuterRadius = 1.5f;
        l.pointLightInnerRadius = 0.2f;
    }

    // ── Death tint canvas ─────────────────────────────────────────────────────
    private void BuildDeathTintCanvas()
    {
        var cvGO = new GameObject("__DeathTintCanvas");
        var cv = cvGO.AddComponent<Canvas>();
        cv.renderMode  = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 998;
        cvGO.AddComponent<UnityEngine.UI.CanvasScaler>();

        var tGO = new GameObject("Tint");
        tGO.transform.SetParent(cvGO.transform, false);
        var rt = tGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = tGO.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = false;
        StartCoroutine(FadeInTint(img));
    }

    private IEnumerator FadeInTint(UnityEngine.UI.Image img)
    {
        float t = 0f, dur = 0.6f;
        while (t < dur) { t += Time.unscaledDeltaTime; img.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.82f, t / dur)); yield return null; }
        img.color = new Color(0f, 0f, 0f, 0.82f);
    }

    // ── Fallback game-over overlay ────────────────────────────────────────────
    private IEnumerator ShowFallbackGameOver()
    {
        Canvas canvas = null;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }
        if (canvas == null) yield break;

        TMP_FontAsset font = FontEnforcer.Font;

        GameObject root = new GameObject("GameOverFallback");
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var bg = new GameObject("BG").AddComponent<Image>();
        bg.transform.SetParent(root.transform, false);
        bg.color = new Color(0, 0, 0, 0.8f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(root.transform, false);
        var tRt = txtGO.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.2f, 0.58f); tRt.anchorMax = new Vector2(0.8f, 0.75f);
        tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "YOU DIED";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 20f;
        tmp.color = new Color(1f, 0.2f, 0.2f);
        tmp.outlineWidth = 0.3f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        if (font != null) tmp.font = font;

        // Try Again
        GameObject r1 = BuildButton(root.transform, "RestartBtn",
            new Vector2(0.3f, 0.4f), new Vector2(0.7f, 0.54f),
            "Try Again", new Color(0.7f, 0.1f, 0.1f), font);
        r1.GetComponent<Button>().onClick.AddListener(RestartLevel);

        // Main Menu
        GameObject r2 = BuildButton(root.transform, "MenuBtn",
            new Vector2(0.3f, 0.24f), new Vector2(0.7f, 0.38f),
            "Main Menu", new Color(0.15f, 0.15f, 0.4f), font);
        r2.GetComponent<Button>().onClick.AddListener(GoToMainMenu);

        yield return null;
    }

    // ── Damage screen pulse ───────────────────────────────────────────────────
    private Image _damageOverlay;
    private Coroutine _damagePulseCo;

    private IEnumerator DamageScreenPulse()
    {
        if (_damageOverlay == null) _damageOverlay = BuildDamageOverlay();
        if (_damageOverlay == null) yield break;

        const float peakAlpha = 0.18f;
        float t = 0f;
        while (t < 0.08f)
        {
            t += Time.deltaTime;
            _damageOverlay.color = new Color(1f, 0.05f, 0.05f, Mathf.Lerp(0f, peakAlpha, t / 0.08f));
            yield return null;
        }
        t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            _damageOverlay.color = new Color(1f, 0.05f, 0.05f, Mathf.Lerp(peakAlpha, 0f, t / 0.25f));
            yield return null;
        }
        _damageOverlay.color = new Color(1f, 0.05f, 0.05f, 0f);
        _damagePulseCo = null;
    }

    private void TriggerDamagePulse()
    {
        if (_damagePulseCo != null) StopCoroutine(_damagePulseCo);
        _damagePulseCo = StartCoroutine(DamageScreenPulse());
    }

    private Image BuildDamageOverlay()
    {
        Canvas canvas = null;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }
        if (canvas == null) return null;

        Texture2D tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(63.5f, 63.5f);
        float maxDist = 63.5f;
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                d = Mathf.Clamp01(d);
                float a = Mathf.Pow(d, 2.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;

        GameObject go = new GameObject("CaveDamageVignette");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 16f);
        img.color = new Color(1f, 0.05f, 0.05f, 0f);
        img.raycastTarget = false;
        return img;
    }

    private void AutoFindGameOverUI()
    {
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            Transform t = FindDeep(c.transform, "GameOverUI")
                       ?? FindDeep(c.transform, "GameOver")
                       ?? FindDeep(c.transform, "GameOverPanel");
            if (t != null)
            {
                _gameOverUI = t.gameObject;
                _restartButton = t.GetComponentInChildren<Button>();
                return;
            }
        }
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var f = FindDeep(child, name);
            if (f != null) return f;
        }
        return null;
    }
}
