using System.Collections;
using UnityEngine;
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

    // ── Weapon ────────────────────────────────────────────────────────────────
    private IEnumerator EquipWeaponNextFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

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

        if (weapon != null)
        {
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

    private IEnumerator RestoreFromSave()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        if (SaveManager.Instance == null) yield break;
        SaveManager.Instance.ApplySavedState();
        if (Player != null)
        {
            Vector3 savedPos = SaveManager.Instance.GetSavedPosition();
            if (savedPos != Vector3.zero) Player.transform.position = savedPos;
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

        TMP_FontAsset font = FontEnforcer.Font;

        GameObject root = new GameObject("PauseMenu");
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var bg = new GameObject("BG").AddComponent<Image>();
        bg.transform.SetParent(root.transform, false);
        bg.color = new Color(0f, 0f, 0f, 0.82f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(root.transform, false);
        var tRt = titleGO.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.2f, 0.62f); tRt.anchorMax = new Vector2(0.8f, 0.78f);
        tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
        var tmp = titleGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "PAUSED";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 22f;
        tmp.color = Color.white;
        if (font != null) tmp.font = font;

        GameObject resumeBtn = BuildButton(root.transform, "ResumeBtn",
            new Vector2(0.5f, 0.47f), new Vector2(0.5f, 0.47f),
            "Resume", new Color(0.1f, 0.45f, 0.1f), font);
        resumeBtn.GetComponent<Button>().onClick.AddListener(ResumePause);

        GameObject menuBtn = BuildButton(root.transform, "MenuBtn",
            new Vector2(0.5f, 0.31f), new Vector2(0.5f, 0.31f),
            "Save & Return to Menu", new Color(0.15f, 0.15f, 0.4f), font);
        menuBtn.GetComponent<Button>().onClick.AddListener(SaveAndQuit);

        return root;
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

        Time.timeScale = 0f;
        MusicManager.Instance?.Stop();

        // Permanent strong red vignette overlay
        if (_damagePulseCo != null) { StopCoroutine(_damagePulseCo); _damagePulseCo = null; }
        if (_damageOverlay == null) _damageOverlay = BuildDamageOverlay();
        if (_damageOverlay != null) _damageOverlay.color = new Color(1f, 0.05f, 0.05f, 0.45f);

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
        Button existing = parent.GetComponentInChildren<Button>(true);
        if (existing != null && _restartButton == null)
        {
            existing.onClick.RemoveAllListeners();
            existing.onClick.AddListener(RestartLevel);
            _restartButton = existing;
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
        Canvas canvas = FindOverlayCanvas();
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
        bg.color = new Color(0.45f, 0f, 0f, 0.88f);
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

        GameObject r1 = BuildButton(root.transform, "RestartBtn",
            new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f),
            "Try Again", new Color(0.7f, 0.1f, 0.1f), font);
        r1.GetComponent<Button>().onClick.AddListener(RestartLevel);

        GameObject r2 = BuildButton(root.transform, "MenuBtn",
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
            if (c.name == "GameCanvas") return c;
            if (found == null) found = c;
        }
        return found;
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
