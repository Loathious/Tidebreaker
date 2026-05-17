using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shared base for the Jungle / Desert / Ocean level managers.
/// Bootstraps each level with no Inspector wiring required:
///  • finds the player and hooks death,
///  • restores / equips a weapon,
///  • configures level music,
///  • handles game-over UI + restart,
///  • shows a red damage pulse when the player is hit.
///
/// Modelled on the existing CaveManager so all new levels behave consistently.
/// </summary>
public abstract class LevelManagerBase : MonoBehaviour
{
    [Header("Music")]
    public AudioClip ambientMusic;
    public AudioClip combatMusic;

    [Header("Weapon")]
    [Tooltip("Fallback weapon if the player arrives without one (Diamond Sword).")]
    public ItemData defaultWeapon;

    [Header("Level Intro")]
    [Tooltip("Text shown on the level intro screen. Leave empty to skip intro.")]
    [SerializeField] protected string levelDisplayName = "";
    [SerializeField] private float introHoldTime = 1.8f;

    // Auto-found at runtime
    protected Health     PlayerHealth;
    protected GameObject Player;

    private GameObject _gameOverUI;
    private Button     _restartButton;
    private bool       _isGameOver;
    private bool       _combatStarted;
    private bool       _isPaused;
    private GameObject _pauseUI;

    public bool IsGameOver => _isGameOver;

    /// <summary>The active level manager — enemies use this to report combat / kills.</summary>
    public static LevelManagerBase Current { get; private set; }

    /// <summary>True during the level-intro banner — enemies freeze while the player keeps full control.</summary>
    public static bool MonstersFrozen { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    protected virtual void Awake()
    {
        Current = this;

        // Stop enemies (layer "Enemy") from colliding with / shoving each other
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);

        CleanupLeftovers();
    }

    protected virtual void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    /// <summary>Called by enemies when they die so levels can track objectives.</summary>
    public virtual void OnEnemyDefeated() { }

    protected virtual void Start()
    {
        EnsureEventSystem();

        // Player
        Player = GameObject.FindGameObjectWithTag("Player");
        if (Player != null)
        {
            PlayerHealth = Player.GetComponent<Health>() ?? Player.GetComponentInChildren<Health>();
            if (PlayerHealth != null)
            {
                PlayerHealth.OnDeath.AddListener(OnPlayerDeath);
                PlayerHealth.OnDamageTaken.AddListener(_ => TriggerDamagePulse());
            }

            // Player renders in front of all enemies (enemies use sortingOrder 5)
            SpriteRenderer playerSr = Player.GetComponent<SpriteRenderer>()
                                   ?? Player.GetComponentInChildren<SpriteRenderer>();
            if (playerSr != null && playerSr.sortingOrder < 10)
                playerSr.sortingOrder = 10;
        }

        // Game-over UI
        AutoFindGameOverUI();
        if (_gameOverUI != null) _gameOverUI.SetActive(false);
        if (_restartButton != null) _restartButton.onClick.AddListener(RestartLevel);

        // Hide any leftover dialog panel / tutorial text from the template scene
        DialogUI dialog = FindFirstObjectByType<DialogUI>(FindObjectsInactive.Include);
        if (dialog != null) dialog.Hide();

        // Weapon — if loading from a save, skip equipping and restore saved state instead
        bool loadingFromSave = SaveManager.Instance != null && SaveManager.Instance.IsLoadingFromSave;
        if (loadingFromSave)
            StartCoroutine(RestoreFromSave());
        else
        {
            StartCoroutine(EquipWeaponNextFrame());
            // Checkpoint save so "Continue" from the main menu resumes this level
            SaveManager.Instance?.SaveGame();
        }

        // Music — reconfigure the (persistent) MusicManager for this level
        if (MusicManager.Instance != null && ambientMusic != null)
            MusicManager.Instance.ConfigureAndPlay(ambientMusic, combatMusic);

        Time.timeScale = 1f;

        OnLevelStart();

        // Level intro banner — auto-detect name from scene when Inspector field is blank
        if (string.IsNullOrEmpty(levelDisplayName))
            levelDisplayName = GetLevelDisplayNameFromScene();
        if (!string.IsNullOrEmpty(levelDisplayName))
            StartCoroutine(ShowLevelIntroBanner(levelDisplayName));
    }

    protected virtual void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !_isGameOver) TogglePause();
        if (_isGameOver || _isPaused) return;
        if (PlayerHealth != null && PlayerHealth.IsDead) OnPlayerDeath();
    }

    /// <summary>Per-level setup (spawn enemies, set objective, etc.).</summary>
    protected abstract void OnLevelStart();

    // ── Combat music ──────────────────────────────────────────────────────────
    public void NotifyCombatStarted()
    {
        if (_combatStarted || combatMusic == null) return;
        _combatStarted = true;
        MusicManager.Instance?.SwitchToCombat();
    }

    public void NotifyCombatEnded()
    {
        if (!_combatStarted) return;
        _combatStarted = false;
        MusicManager.Instance?.SwitchToAmbient();
    }

    // ── Level intro banner ────────────────────────────────────────────────────
    private IEnumerator ShowLevelIntroBanner(string name)
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) yield break;

        MonstersFrozen = true;

        GameObject root = new GameObject("LevelIntroBanner");
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // Full-screen black
        var bg = new GameObject("BG").AddComponent<Image>();
        bg.transform.SetParent(root.transform, false);
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = false;
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

        // Level name text
        var txtGO = new GameObject("LevelName");
        txtGO.transform.SetParent(root.transform, false);
        var tRt = txtGO.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.1f, 0.42f); tRt.anchorMax = new Vector2(0.9f, 0.58f);
        tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text         = name;
        tmp.alignment    = TextAlignmentOptions.Center;
        tmp.fontSize     = 18f;
        tmp.color        = new Color(1f, 1f, 1f, 0f);
        tmp.outlineWidth = 0.28f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        FontEnforcer.ApplyTo(tmp);

        // Fade in
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.6f);
            bg.color  = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.92f, k));
            tmp.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 1f, k));
            yield return null;
        }

        yield return new WaitForSecondsRealtime(introHoldTime);

        // Fade out
        t = 0f;
        while (t < 0.7f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.7f);
            bg.color  = new Color(0f, 0f, 0f, Mathf.Lerp(0.92f, 0f, k));
            tmp.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, k));
            yield return null;
        }

        Destroy(root);
        MonstersFrozen = false;
    }

    private static string GetLevelDisplayNameFromScene()
    {
        string lower = SceneManager.GetActiveScene().name.ToLower();
        if (lower.Contains("jungle"))  return "Level 3\nJungle Temple";
        if (lower.Contains("desert"))  return "Level 4\nDesert Pyramid";
        if (lower.Contains("ocean"))   return "Level 5\nThe Ocean";
        if (lower.Contains("cave"))    return "Level 2\nDark Cave";
        if (lower.Contains("village")) return "Level 1\nThe Village";
        return "";
    }

    // ── Weapon ────────────────────────────────────────────────────────────────
    private IEnumerator EquipWeaponNextFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        // Always start a fresh level with full health and correct spawn position.
        if (Player != null)
        {
            Health hp = Player.GetComponent<Health>() ?? Player.GetComponentInChildren<Health>();
            hp?.ResetHealth();
            TeleportPlayerToSceneSpawn();
        }

        if (Inventory.Instance == null) yield break;

        // From Level 3 on the player canonically wields the Diamond Sword.
        ItemData weapon = defaultWeapon;
        if (weapon == null) weapon = Resources.Load<ItemData>("DiamondSword");

        // Broadest fallback: any weapon-type ItemData in memory named "Diamond Sword" or "DiamondSword"
        if (weapon == null)
        {
            foreach (ItemData id in Resources.FindObjectsOfTypeAll<ItemData>())
            {
                if (id != null && id.itemType == ItemType.Weapon
                    && (id.name.Contains("Diamond") || id.name.Contains("diamond")))
                {
                    weapon = id;
                    break;
                }
            }
        }

        // Final fallback: runtime ItemData so the player never starts weaponless
        if (weapon == null)
        {
            weapon           = ScriptableObject.CreateInstance<ItemData>();
            weapon.name      = "DiamondSword";
            weapon.itemName  = "Diamond Sword";
            weapon.damage    = 35;
            weapon.maxUses   = 0;
            weapon.itemType  = ItemType.Weapon;
            weapon.attackCooldown = 0.32f;
        }

        if (weapon != null)
        {
            // Ensure placeholder icon on diamond sword so it shows in hotbar
            EnsureDiamondSwordIcon(weapon);

            Inventory.Instance.ClearAll();
            Inventory.Instance.AddItem(weapon);
            Inventory.Instance.ToggleEquip(0);
            PlayerPrefs.DeleteKey("WeaponCurrentUses");
            yield break;
        }

        // No default — equip whatever weapon the player already carries (don't clear).
        ItemData[] hotbar = Inventory.Instance.GetHotbarItems();
        for (int i = 0; i < hotbar.Length; i++)
        {
            if (hotbar[i] != null && hotbar[i].itemType == ItemType.Weapon)
            {
                if (Inventory.Instance.GetEquippedSlot() != i)
                    Inventory.Instance.ToggleEquip(i);
                yield break;
            }
        }
    }

    private static void EnsureDiamondSwordIcon(ItemData weapon)
    {
        if (weapon == null || weapon.icon != null) return;
        // Create a procedural diamond-blue icon so the hotbar isn't blank
        Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color blade = new Color(0.3f, 0.7f, 1f);
        Color edge  = new Color(0.6f, 0.9f, 1f);
        Color clear = Color.clear;
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                // Simple diamond shape
                bool isDiamond = Mathf.Abs(x - 8) + Mathf.Abs(y - 8) < 6;
                bool isEdge    = Mathf.Abs(x - 8) + Mathf.Abs(y - 8) == 5;
                tex.SetPixel(x, y, isDiamond ? (isEdge ? edge : blade) : clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        weapon.icon = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
    }

    private void TeleportPlayerToSceneSpawn()
    {
        if (Player == null) return;

        // Priority 1: Scene object named "SpawnPoint" or "PlayerSpawn"
        // (avoid FindGameObjectWithTag since "SpawnPoint" may not be defined in every project)
        GameObject spawn = GameObject.Find("PlayerSpawn");
        if (spawn == null) spawn = GameObject.Find("SpawnPoint");

        Vector3 dest;
        if (spawn != null)
            dest = spawn.transform.position;
        else
            dest = transform.position; // fallback: place LevelManager object at spawn location

        Player.transform.position = dest;
        Rigidbody2D rb = Player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = new Vector2(dest.x, dest.y);
        }
    }

    private IEnumerator RestoreFromSave()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        if (SaveManager.Instance == null) yield break;

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string savedScene   = SaveManager.Instance.SavedScene;
        bool   sameScene    = string.Equals(currentScene, savedScene,
                                  System.StringComparison.OrdinalIgnoreCase);

        // Always restore equipped weapon so the player has the right gear.
        SaveManager.Instance.ApplySavedState();

        if (Player != null)
        {
            if (sameScene)
            {
                // Resuming the same level mid-run: restore exact position.
                Vector3 savedPos = SaveManager.Instance.GetSavedPosition();
                if (savedPos != Vector3.zero) Player.transform.position = savedPos;
            }
            else
            {
                // Advancing to a new level: always start at the scene-placed spawn and
                // reset health to full so the player doesn't carry over damage.
                Health hp = Player.GetComponent<Health>()
                         ?? Player.GetComponentInChildren<Health>();
                hp?.ResetHealth();
                TeleportPlayerToSceneSpawn();
            }
        }

        SaveManager.Instance.ConfirmLoadApplied();
    }

    // ── Pause ─────────────────────────────────────────────────────────────────
    private void TogglePause()
    {
        if (_isPaused) ResumePause();
        else           OpenPause();
    }

    private void OpenPause()
    {
        _isPaused = true;
        Time.timeScale = 0f;
        _pauseUI = CreatePauseUI();
    }

    private void ResumePause()
    {
        _isPaused = false;
        Time.timeScale = 1f;
        if (_pauseUI != null) { Destroy(_pauseUI); _pauseUI = null; }
    }

    private void SaveAndQuit()
    {
        SaveManager.Instance?.SaveGame();
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private GameObject CreatePauseUI()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return null;
        EnsureGraphicRaycaster(canvas);
        canvas.sortingOrder = 500;   // ensure pause is above all HUD canvases
        EnsureEventSystem();

        TMP_FontAsset font = FontEnforcer.Font;

        GameObject root = new GameObject("PauseMenu");
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // Full-screen darkener — raycastTarget=true so clicks outside the panel
        // are consumed by the dimmer and don't reach game objects beneath it.
        var dimmer = new GameObject("Dimmer").AddComponent<Image>();
        dimmer.transform.SetParent(root.transform, false);
        dimmer.color = new Color(0f, 0f, 0f, 0.72f);
        dimmer.raycastTarget = true;
        FullStretch(dimmer.GetComponent<RectTransform>());

        // Center panel
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        var panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.35f, 0.25f);
        panelRt.anchorMax = new Vector2(0.65f, 0.80f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.96f);

        // PAUSED title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panel.transform, false);
        var tRt = titleGO.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0.78f); tRt.anchorMax = new Vector2(1f, 1f);
        tRt.offsetMin = new Vector2(8f, 0f);    tRt.offsetMax = new Vector2(-8f, 0f);
        var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
        titleTmp.text         = "PAUSED";
        titleTmp.alignment    = TextAlignmentOptions.Center;
        titleTmp.fontSize     = 16f;
        titleTmp.color        = Color.white;
        titleTmp.outlineWidth = 0.25f;
        titleTmp.outlineColor = new Color32(0, 0, 0, 200);
        if (font != null) titleTmp.font = font;

        // Separator line
        var sepGO = new GameObject("Sep").AddComponent<Image>();
        sepGO.transform.SetParent(panel.transform, false);
        sepGO.color = new Color(1f, 1f, 1f, 0.18f);
        var sepRt = sepGO.GetComponent<RectTransform>();
        sepRt.anchorMin = new Vector2(0.05f, 0.755f); sepRt.anchorMax = new Vector2(0.95f, 0.762f);
        sepRt.offsetMin = Vector2.zero; sepRt.offsetMax = Vector2.zero;

        // Main buttons panel (shown initially)
        GameObject mainBtns = new GameObject("MainButtons");
        mainBtns.transform.SetParent(panel.transform, false);
        var mbRt = mainBtns.AddComponent<RectTransform>();
        mbRt.anchorMin = new Vector2(0.08f, 0.04f); mbRt.anchorMax = new Vector2(0.92f, 0.74f);
        mbRt.offsetMin = Vector2.zero; mbRt.offsetMax = Vector2.zero;
        mainBtns.AddComponent<Image>().color = Color.clear;

        // Settings panel (hidden initially)
        GameObject settingsPanel = new GameObject("SettingsPanel");
        settingsPanel.transform.SetParent(panel.transform, false);
        var spRt = settingsPanel.AddComponent<RectTransform>();
        spRt.anchorMin = new Vector2(0.08f, 0.04f); spRt.anchorMax = new Vector2(0.92f, 0.74f);
        spRt.offsetMin = Vector2.zero; spRt.offsetMax = Vector2.zero;
        settingsPanel.AddComponent<Image>().color = Color.clear;
        settingsPanel.SetActive(false);

        // ── Main buttons ────────────────────────────────────────────────────
        float btnH = 0.26f;
        float gap  = 0.06f;
        float y    = 1f - btnH;

        var resumeGO = BuildPanelButton(mainBtns.transform, "ResumeBtn",
            new Vector2(0f, y - gap), new Vector2(1f, y + btnH - gap),
            "Resume", new Color(0.1f, 0.38f, 0.1f), font);
        resumeGO.GetComponent<Button>().onClick.AddListener(ResumePause);
        y -= btnH + gap;

        var settingsGO = BuildPanelButton(mainBtns.transform, "SettingsBtn",
            new Vector2(0f, y - gap), new Vector2(1f, y + btnH - gap),
            "Settings", new Color(0.18f, 0.18f, 0.32f), font);
        y -= btnH + gap;

        var menuGO = BuildPanelButton(mainBtns.transform, "MenuBtn",
            new Vector2(0f, y - gap), new Vector2(1f, y + btnH - gap),
            "Save & Return to Menu", new Color(0.32f, 0.10f, 0.10f), font);
        menuGO.GetComponent<Button>().onClick.AddListener(SaveAndQuit);

        // ── Settings sub-panel ──────────────────────────────────────────────
        var stTitle = new GameObject("SettingsTitle");
        stTitle.transform.SetParent(settingsPanel.transform, false);
        var stRt = stTitle.AddComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0f, 0.78f); stRt.anchorMax = new Vector2(1f, 1f);
        stRt.offsetMin = Vector2.zero; stRt.offsetMax = Vector2.zero;
        var stTmp = stTitle.AddComponent<TextMeshProUGUI>();
        stTmp.text      = "SETTINGS";
        stTmp.alignment = TextAlignmentOptions.Center;
        stTmp.fontSize  = 10f;
        stTmp.color     = new Color(0.8f, 0.8f, 1f, 1f);
        if (font != null) stTmp.font = font;

        // Volume label
        var volLabel = new GameObject("VolumeLabel");
        volLabel.transform.SetParent(settingsPanel.transform, false);
        var vlRt = volLabel.AddComponent<RectTransform>();
        vlRt.anchorMin = new Vector2(0f, 0.56f); vlRt.anchorMax = new Vector2(1f, 0.72f);
        vlRt.offsetMin = Vector2.zero; vlRt.offsetMax = Vector2.zero;
        var vlTmp = volLabel.AddComponent<TextMeshProUGUI>();
        vlTmp.text      = "Music Volume";
        vlTmp.alignment = TextAlignmentOptions.Center;
        vlTmp.fontSize  = 7f;
        vlTmp.color     = Color.white;
        if (font != null) vlTmp.font = font;

        // Volume slider
        GameObject sliderGO = CreateVolumeSlider(settingsPanel.transform,
            new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.57f));

        // Back button
        var backGO = BuildPanelButton(settingsPanel.transform, "BackBtn",
            new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.30f),
            "← Back", new Color(0.18f, 0.18f, 0.32f), font);
        backGO.GetComponent<Button>().onClick.AddListener(() =>
        {
            mainBtns.SetActive(true);
            settingsPanel.SetActive(false);
        });

        // Wire settings button to swap panels
        settingsGO.GetComponent<Button>().onClick.AddListener(() =>
        {
            mainBtns.SetActive(false);
            settingsPanel.SetActive(true);
        });

        return root;
    }

    private GameObject CreateVolumeSlider(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject sliderGO = new GameObject("VolumeSlider");
        sliderGO.transform.SetParent(parent, false);
        var sRt = sliderGO.AddComponent<RectTransform>();
        sRt.anchorMin = anchorMin; sRt.anchorMax = anchorMax;
        sRt.offsetMin = Vector2.zero; sRt.offsetMax = Vector2.zero;

        // Background track
        var bg = new GameObject("Background").AddComponent<Image>();
        bg.transform.SetParent(sliderGO.transform, false);
        bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        FullStretch(bg.GetComponent<RectTransform>());

        // Fill area
        var fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(sliderGO.transform, false);
        var faRt = fillArea.AddComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.25f); faRt.anchorMax = new Vector2(1f, 0.75f);
        faRt.offsetMin = new Vector2(5f, 0f); faRt.offsetMax = new Vector2(-15f, 0f);

        var fill = new GameObject("Fill").AddComponent<Image>();
        fill.transform.SetParent(fillArea.transform, false);
        fill.color = new Color(0.3f, 0.6f, 1f, 1f);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;

        // Handle
        var handleArea = new GameObject("HandleArea");
        handleArea.transform.SetParent(sliderGO.transform, false);
        FullStretch(handleArea.AddComponent<RectTransform>());
        var handle = new GameObject("Handle").AddComponent<Image>();
        handle.transform.SetParent(handleArea.transform, false);
        handle.color = Color.white;
        var hRt = handle.GetComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(20f, 0f);
        hRt.anchorMin = new Vector2(0f, 0f);
        hRt.anchorMax = new Vector2(0f, 1f);

        var slider = sliderGO.AddComponent<Slider>();
        slider.fillRect        = fill.GetComponent<RectTransform>();
        slider.handleRect      = handle.GetComponent<RectTransform>();
        slider.targetGraphic   = handle;
        slider.direction       = Slider.Direction.LeftToRight;
        slider.minValue        = 0f;
        slider.maxValue        = 1f;
        slider.value           = MusicManager.Instance != null ? MusicManager.Instance.GetVolume() : 0.5f;
        slider.onValueChanged.AddListener(v => MusicManager.Instance?.SetVolume(v));

        return sliderGO;
    }

    private static void FullStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static GameObject BuildPanelButton(Transform parent, string name,
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

        // Hover tint
        ColorBlock cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        cb.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
        btn.colors = cb;

        GameObject txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        FullStretch(txtGO.AddComponent<RectTransform>());
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize  = 7.5f;
        tmp.color     = Color.white;
        if (font != null) tmp.font = font;
        return go;
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────
    private void CleanupLeftovers()
    {
        foreach (var v in FindObjectsByType<VillagerNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            v.gameObject.SetActive(false);
        foreach (var v in FindObjectsByType<Villager2NPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            v.gameObject.SetActive(false);
        foreach (var gm in FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            gm.enabled = false;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            Transform t = FindDeep(c.transform, "TutorialText");
            if (t != null) t.gameObject.SetActive(false);
        }
    }

    // ── Death / Game Over ─────────────────────────────────────────────────────
    protected virtual void OnPlayerDeath()
    {
        if (_isGameOver) return;
        _isGameOver = true;

        // Close pause menu if open
        if (_isPaused) { _isPaused = false; if (_pauseUI != null) { Destroy(_pauseUI); _pauseUI = null; } }

        MusicManager.Instance?.Stop();

        // Freeze the camera immediately so it doesn't follow the falling player.
        CameraFollow camFollow = Camera.main?.GetComponent<CameraFollow>();
        camFollow?.Freeze();

        // Stop any damage pulse so we can control the overlay ourselves.
        if (_damagePulseCo != null) { StopCoroutine(_damagePulseCo); _damagePulseCo = null; }

        // Hide all UI that should vanish on death: boss bars, hotbar, player health bars, objectives.
        foreach (BossHealthBar bar in FindObjectsByType<BossHealthBar>(FindObjectsSortMode.None))
            if (bar != null) bar.gameObject.SetActive(false);
        HotbarUI hotbar = FindFirstObjectByType<HotbarUI>(FindObjectsInactive.Include);
        if (hotbar != null) hotbar.SetVisible(false);
        foreach (HealthBar hb in FindObjectsByType<HealthBar>(FindObjectsSortMode.None))
            if (hb != null) hb.Hide();
        ObjectiveManager.Instance?.HideObjective();

        // Run the fall → red tint → pause → death screen sequence.
        StartCoroutine(DeathSequence());
    }

    private Image _deathOverlay; // solid full-screen red — separate from the vignette

    private IEnumerator DeathSequence()
    {
        // Solid full-screen red overlay (not the vignette, which has a transparent centre).
        if (_deathOverlay == null) _deathOverlay = BuildSolidDeathOverlay();

        // Phase 1 — player falls off the map while the screen turns deep red (real time).
        const float fallDuration = 1.6f;
        const float peakAlpha    = 0.78f;
        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            if (_deathOverlay != null)
                _deathOverlay.color = new Color(0.55f, 0f, 0f, Mathf.Lerp(0f, peakAlpha, t));
            yield return null;
        }
        if (_deathOverlay != null)
            _deathOverlay.color = new Color(0.55f, 0f, 0f, peakAlpha);

        // Phase 2 — pause everything, then show the death screen on top of the red.
        Time.timeScale = 0f;

        if (_gameOverUI != null)
        {
            _gameOverUI.SetActive(true);
            EnsureDeathButtons(_gameOverUI.transform);
        }
        else
        {
            StartCoroutine(ShowFallbackGameOver());
        }
    }

    private Image BuildSolidDeathOverlay()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return null;

        GameObject go = new GameObject("DeathRedOverlay");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.55f, 0f, 0f, 0f);
        img.raycastTarget = false;
        return img;
    }

    private void RestartLevel()
    {
        Time.timeScale = 1f;
        // Full restart: clear per-run PlayerPrefs so the next run starts clean
        PlayerPrefs.DeleteKey("PlayerHasArmor");
        PlayerPrefs.DeleteKey("PlayerHasBow");
        PlayerPrefs.Save();
        SceneManager.LoadScene("MainMenu");
    }

    private void GoToMainMenu()
    {
        Time.timeScale = 1f;
        PlayerPrefs.DeleteKey("PlayerHasArmor");
        PlayerPrefs.DeleteKey("PlayerHasBow");
        PlayerPrefs.Save();
        SceneManager.LoadScene("MainMenu");
    }

    private void EnsureDeathButtons(Transform parent)
    {
        // The canvas that contains the GameOverUI is what needs GraphicRaycaster —
        // it is often GameCanvas (a scene canvas), NOT the HUD overlay canvas.
        Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
        if (parentCanvas != null) { EnsureGraphicRaycaster(parentCanvas); parentCanvas.sortingOrder = 999; }
        Canvas overlayCanvas = FindOverlayCanvas();
        if (overlayCanvas != null && overlayCanvas != parentCanvas)
        { EnsureGraphicRaycaster(overlayCanvas); if (overlayCanvas.sortingOrder < 999) overlayCanvas.sortingOrder = 999; }
        EnsureEventSystem();

        Button existing = parent.GetComponentInChildren<Button>(true);
        if (existing != null && _restartButton == null)
        {
            existing.onClick.RemoveAllListeners();
            existing.onClick.AddListener(RestartLevel);
            _restartButton = existing;
            // Wire second scene button (if any) to GoToMainMenu
            Button[] allBtns = parent.GetComponentsInChildren<Button>(true);
            if (allBtns.Length > 1)
            {
                allBtns[1].onClick.RemoveAllListeners();
                allBtns[1].onClick.AddListener(GoToMainMenu);
            }
            return;
        }
        if (_restartButton != null) return;

        TMP_FontAsset font = FontEnforcer.Font;

        GameObject restartGO = BuildButton(parent, "RestartBtn",
            new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.45f),
            "Try Again", new Color(0.7f, 0.1f, 0.1f), font);
        _restartButton = restartGO.GetComponent<Button>();
        _restartButton.onClick.AddListener(RestartLevel);

        GameObject menuGO = BuildButton(parent, "MainMenuBtn",
            new Vector2(0.5f, 0.14f), new Vector2(0.5f, 0.28f),
            "Main Menu", new Color(0.15f, 0.15f, 0.4f), font);
        menuGO.GetComponent<Button>().onClick.AddListener(GoToMainMenu);
    }

    protected static GameObject BuildButton(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, string label, Color bg, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(-90f, -22f); rt.offsetMax = new Vector2(90f, 22f);
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

    private IEnumerator ShowFallbackGameOver()
    {
        EnsureEventSystem();

        // Dedicated top-level canvas — never blocked by any scene UI hierarchy
        var canvasGO = new GameObject("DeathScreenCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        TMP_FontAsset font = FontEnforcer.Font;

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(canvasGO.transform, false);
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

        GameObject r1 = BuildButton(canvasGO.transform, "RestartBtn",
            new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f),
            "Try Again", new Color(0.7f, 0.1f, 0.1f), font);
        r1.GetComponent<Button>().onClick.AddListener(RestartLevel);

        GameObject r2 = BuildButton(canvasGO.transform, "MenuBtn",
            new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f),
            "Main Menu", new Color(0.15f, 0.15f, 0.4f), font);
        r2.GetComponent<Button>().onClick.AddListener(GoToMainMenu);
        yield return null;
    }

    // ── Damage screen pulse ───────────────────────────────────────────────────
    private Image     _damageOverlay;
    private Coroutine _damagePulseCo;

    private void TriggerDamagePulse()
    {
        if (_damagePulseCo != null) StopCoroutine(_damagePulseCo);
        _damagePulseCo = StartCoroutine(DamageScreenPulse());
    }

    private IEnumerator DamageScreenPulse()
    {
        if (_damageOverlay == null) _damageOverlay = BuildDamageOverlay();
        if (_damageOverlay == null) yield break;

        const float peak = 0.2f;
        float t = 0f;
        while (t < 0.08f)
        {
            t += Time.deltaTime;
            _damageOverlay.color = new Color(1f, 0.05f, 0.05f, Mathf.Lerp(0f, peak, t / 0.08f));
            yield return null;
        }
        t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            _damageOverlay.color = new Color(1f, 0.05f, 0.05f, Mathf.Lerp(peak, 0f, t / 0.3f));
            yield return null;
        }
        _damageOverlay.color = new Color(1f, 0.05f, 0.05f, 0f);
        _damagePulseCo = null;
    }

    private Image BuildDamageOverlay()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return null;

        Texture2D tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(63.5f, 63.5f);
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), center) / 63.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(d, 2.5f)));
            }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;

        GameObject go = new GameObject("DamageVignette");
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

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void AutoFindGameOverUI()
    {
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            Transform t = FindDeep(c.transform, "GameOverUI")
                       ?? FindDeep(c.transform, "GameOver")
                       ?? FindDeep(c.transform, "GameOverPanel");
            if (t != null)
            {
                _gameOverUI    = t.gameObject;
                _restartButton = t.GetComponentInChildren<Button>(true);
                return;
            }
        }
    }

    protected static Canvas FindOverlayCanvas()
    {
        Canvas found = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode != RenderMode.ScreenSpaceOverlay &&
                c.renderMode != RenderMode.ScreenSpaceCamera) continue;
            if (c.name == "GameCanvas") { EnsureGraphicRaycaster(c); return c; }
            if (found == null) found = c;
        }
        if (found != null) EnsureGraphicRaycaster(found);
        return found;
    }

    private static void EnsureGraphicRaycaster(Canvas c)
    {
        if (c != null && c.GetComponent<GraphicRaycaster>() == null)
            c.gameObject.AddComponent<GraphicRaycaster>();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
            Object.DontDestroyOnLoad(esGO);
        }
    }

    protected static Transform FindDeep(Transform parent, string name)
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
