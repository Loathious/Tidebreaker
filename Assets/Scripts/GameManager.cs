using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Singleton game manager for Village level.
/// Handles: game-over, enemy defeated tracking, combat music, cinematic bars.
/// 16:9 aspect ratio enforced at startup.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Health       playerHealth;
    [SerializeField] private Transform    playerTransform;
    [SerializeField] private GameObject   gameOverUI;
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private Button       restartButton;
    [SerializeField] private Image        screenTint;
    [SerializeField] private HealthBar[]  healthBarsToHide;
    [SerializeField] private Villager2NPC villager2;
    [SerializeField] private AudioClip    deathSoundClip;
    [SerializeField] [Range(0f,1f)] private float deathSoundVolume = 1f;

    [Header("Death Settings")]
    [SerializeField] private float restartButtonDelay = 1f;
    [SerializeField] private Color deathTintColor = new Color(0f, 0f, 0f, 0.82f);
    [SerializeField] private float tintDuration   = 0.6f;

    [Header("Enemy Count")]
    public int enemiesToDefeat = 6;

    private bool  _isGameOver;
    private int   _enemiesDefeated;
    private bool  _combatStarted;
    private bool  _allEnemiesDefeated;
    private float _victoryCheckTimer = 3f;   // first check after 3s grace period
    private float _sceneTimer;
    private int   _initialZombieCount;

    public bool LevelComplete => _enemiesDefeated >= enemiesToDefeat;

    // â”€â”€ Lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Enforce 16:9 aspect ratio
        Enforce16by9();
    }

    void Start()
    {
        if (gameOverUI  != null) gameOverUI.SetActive(false);
        if (screenTint  != null) screenTint.color = new Color(1f, 0f, 0f, 0f);

        // Auto-find player health (also searches children, in case Health is on a child GO)
        if (playerHealth == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerHealth = player.GetComponent<Health>() ?? player.GetComponentInChildren<Health>();
        }

        if (playerHealth != null)
            playerHealth.OnDeath.AddListener(OnPlayerDeath);

        // Auto-find game over UI now (in case it's inactive in scene and doesn't show in tag searches later)
        if (gameOverUI == null) AutoFindGameOverUI();
        if (gameOverUI != null) gameOverUI.SetActive(false);

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);

        Time.timeScale = 1f;

        // Auto-find Villager2 if not assigned. INCLUDE INACTIVE: Villager2NPC disables
        // itself in Start() so it must be found via the include-inactive variant.
        if (villager2 == null)
            villager2 = FindFirstObjectByType<Villager2NPC>(FindObjectsInactive.Include);

        // Dynamically count zombies in scene so the "all defeated" trigger always matches
        ZombieAI[] zombies = FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
        if (zombies.Length > 0)
        {
            _initialZombieCount = zombies.Length;
            enemiesToDefeat     = zombies.Length;
        }
    }

    /// <summary>
    /// Health watchdog + fallback victory check. The victory check catches cases where
    /// a zombie's OnDeath event chain missed calling OnEnemyDefeated (e.g., off-screen
    /// zombie destroyed by physics). Scans every 2 s once combat has started.
    /// </summary>
    void Update()
    {
        if (_isGameOver) return;
        _sceneTimer += Time.deltaTime;
        if (playerHealth != null && playerHealth.IsDead) OnPlayerDeath();

        if (!_allEnemiesDefeated)
        {
            _victoryCheckTimer -= Time.deltaTime;
            if (_victoryCheckTimer <= 0f)
            {
                _victoryCheckTimer = 1f;
                FallbackVictoryCheck();
            }
        }
    }

    private void FallbackVictoryCheck()
    {
        if (_initialZombieCount == 0) return; // no enemy level

        ZombieAI[] alive = FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);

        // Count enemies still fighting
        int living = 0;
        foreach (ZombieAI z in alive)
            if (z != null && !z.IsDefeated) living++;

        if (living > 0) return; // at least one zombie still alive

        // All visible zombies are dead (or all are Destroyed). Require either:
        //   â€¢ at least one kill was tracked via OnEnemyDefeated, OR
        //   â€¢ enough scene time has passed that any Destroyed zombies would have been tracked
        bool someProgress = _enemiesDefeated > 0
                         || (alive.Length == 0 && _sceneTimer > 8f);
        if (!someProgress) return;

        _allEnemiesDefeated = true;
        _enemiesDefeated    = enemiesToDefeat;
        StartCoroutine(VictorySequence());
    }

    private void AutoFindGameOverUI()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            Transform t = FindDeep(c.transform, "GameOverUI")
                       ?? FindDeep(c.transform, "GameOver")
                       ?? FindDeep(c.transform, "GameOverPanel");
            if (t != null) { gameOverUI = t.gameObject; return; }
        }
    }

    // â”€â”€ 16:9 enforcement â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    static void Enforce16by9()
    {
        float target = 16f / 9f;
        float current = (float)Screen.width / Screen.height;
        if (Mathf.Abs(current - target) > 0.01f)
        {
            // Letterbox: set the camera's rect
            Camera cam = Camera.main;
            if (cam == null) return;

            float scaleH = current / target;
            if (scaleH < 1f)
            {
                // Pillarbox (screen too narrow)
                float inset = (1f - scaleH) / 2f;
                cam.rect = new Rect(inset, 0f, scaleH, 1f);
            }
            else
            {
                // Letterbox (screen too wide)
                float scaleV = 1f / scaleH;
                float inset  = (1f - scaleV) / 2f;
                cam.rect = new Rect(0f, inset, 1f, scaleV);
            }
        }
    }

    // â”€â”€ Combat â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public void NotifyCombatStarted()
    {
        if (_combatStarted) return;
        _combatStarted = true;
        MusicManager.Instance?.SwitchToCombat();
    }

    public void OnEnemyDefeated()
    {
        _enemiesDefeated++;

        // Update objective counter
        int remaining = enemiesToDefeat - _enemiesDefeated;
        if (remaining > 0)
            ObjectiveManager.Instance?.UpdateObjective($"Defeat all {enemiesToDefeat} monsters ({remaining} remaining)");

        if (_enemiesDefeated >= enemiesToDefeat && !_allEnemiesDefeated)
        {
            _allEnemiesDefeated = true;
            StartCoroutine(VictorySequence());
        }
    }

    /// <summary>
    /// Played once when the player kills the final zombie.
    /// â€” Brief slow-motion + camera shake hit-stop on the killing blow
    /// â€” Big "VICTORY" banner fades in / holds / fades out
    /// â€” Sun-glow flash overlay
    /// â€” Then the second villager appears with the next dialogue
    /// </summary>
    IEnumerator VictorySequence()
    {
        MusicManager.Instance?.SwitchToAmbient();
        ObjectiveManager.Instance?.HideObjective();

        // Build the victory UI procedurally so no scene wiring is required
        Canvas canvas = null;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode != RenderMode.ScreenSpaceOverlay &&
                c.renderMode != RenderMode.ScreenSpaceCamera) continue;
            if (c.name == "GameCanvas") { canvas = c; break; }
            if (canvas == null) canvas = c;
        }

        GameObject root = null;
        TMPro.TextMeshProUGUI banner = null;
        UnityEngine.UI.Image flash = null;

        if (canvas != null)
        {
            root = new GameObject("VictoryOverlay");
            root.transform.SetParent(canvas.transform, false);
            root.transform.SetAsLastSibling();
            RectTransform rt = root.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // Soft white flash (fades quickly)
            GameObject flashGO = new GameObject("Flash");
            flashGO.transform.SetParent(root.transform, false);
            RectTransform fRt = flashGO.AddComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero; fRt.offsetMax = Vector2.zero;
            flash = flashGO.AddComponent<UnityEngine.UI.Image>();
            flash.color = new Color(1f, 0.95f, 0.7f, 0f);
            flash.raycastTarget = false;

            // Banner text
            GameObject txtGO = new GameObject("VictoryText");
            txtGO.transform.SetParent(root.transform, false);
            RectTransform tRt = txtGO.AddComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 0.42f);
            tRt.anchorMax = new Vector2(1f, 0.62f);
            tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;

            banner = txtGO.AddComponent<TMPro.TextMeshProUGUI>();
            banner.text          = "VICTORY";
            banner.alignment     = TMPro.TextAlignmentOptions.Center;
            banner.fontSize      = 72f;
            banner.fontStyle     = TMPro.FontStyles.Normal;   // PressStart2P has no bold variant
            banner.color         = new Color(1f, 0.92f, 0.45f, 0f);
            banner.outlineWidth  = 0.32f;
            banner.outlineColor  = new Color32(0, 0, 0, 255);
        }

        // â”€â”€ Slow-motion hit-stop on the killing blow â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        Time.timeScale = 0.25f;
        yield return new WaitForSecondsRealtime(0.55f);
        // Ease back to normal speed
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(0.25f, 1f, t / 0.4f);
            yield return null;
        }
        Time.timeScale = 1f;

        // â”€â”€ Flash + banner fade-in â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (flash != null)
        {
            float ft = 0f;
            while (ft < 0.18f)
            {
                ft += Time.unscaledDeltaTime;
                flash.color = new Color(1f, 0.95f, 0.7f, Mathf.Lerp(0f, 0.55f, ft / 0.18f));
                yield return null;
            }
            // Decay back to transparent
            ft = 0f;
            while (ft < 0.7f)
            {
                ft += Time.unscaledDeltaTime;
                flash.color = new Color(1f, 0.95f, 0.7f, Mathf.Lerp(0.55f, 0f, ft / 0.7f));
                yield return null;
            }
        }

        if (banner != null)
        {
            // Punch scale + fade in
            banner.rectTransform.localScale = new Vector3(0.6f, 0.6f, 1f);
            float bt = 0f;
            while (bt < 0.55f)
            {
                bt += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, bt / 0.55f);
                Color c = banner.color; c.a = k; banner.color = c;
                float scale = Mathf.Lerp(0.6f, 1.15f, k);
                banner.rectTransform.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            // Settle to 1
            bt = 0f;
            while (bt < 0.18f)
            {
                bt += Time.unscaledDeltaTime;
                float k = bt / 0.18f;
                float scale = Mathf.Lerp(1.15f, 1f, k);
                banner.rectTransform.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            banner.rectTransform.localScale = Vector3.one;

            // Hold
            yield return new WaitForSecondsRealtime(1.4f);

            // Fade out
            bt = 0f;
            while (bt < 0.6f)
            {
                bt += Time.unscaledDeltaTime;
                Color c = banner.color; c.a = Mathf.Lerp(1f, 0f, bt / 0.6f); banner.color = c;
                yield return null;
            }
        }

        if (root != null) Destroy(root);

        // Bring out the second villager. Try finding them again in case wiring was missed.
        if (villager2 == null)
            villager2 = FindFirstObjectByType<Villager2NPC>(FindObjectsInactive.Include);

        if (villager2 != null)
        {
            villager2.Appear();
        }
        else
        {
            // No villager2 in scene â€” save progress and load Cave directly
            SaveManager.Instance?.SaveGame();
            yield return new WaitForSecondsRealtime(1.5f);
            int caveIndex = UnityEngine.SceneManagement.SceneUtility
                .GetBuildIndexByScenePath("Assets/Scenes/Cave.unity");
            if (caveIndex >= 0)
                SceneManager.LoadScene(caveIndex);
            else
                SceneManager.LoadScene("Cave");
        }
    }

    // â”€â”€ Death / Game Over â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void OnPlayerDeath()
    {
        if (_isGameOver) return;
        _isGameOver = true;

        if (deathSoundClip != null)
            SettingsManager.PlaySfxAt(deathSoundClip,
                Camera.main != null ? Camera.main.transform.position : Vector3.zero,
                deathSoundVolume);

        Time.timeScale = 0f;
        MusicManager.Instance?.Stop();

        foreach (ZombieAI zombie in FindObjectsByType<ZombieAI>(FindObjectsSortMode.None))
            zombie.enabled = false;

        foreach (HealthBar hb in healthBarsToHide)
            if (hb != null) hb.Hide();

        // Build a full-screen tint on a dedicated canvas (sortingOrder 998) so it
        // covers all HUD elements (health bars, objectives, etc.) without relying on
        // sibling ordering within a shared canvas.
        BuildAndFadeDeathTint();

        // Hide serialized screen tint if it exists (now superseded by the dynamic canvas)
        if (screenTint != null) screenTint.color = new Color(0f, 0f, 0f, 0f);

        // Auto-find gameOverUI if not wired
        if (gameOverUI == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (Canvas c in canvases)
            {
                Transform t = FindDeep(c.transform, "GameOverUI");
                if (t == null) t = FindDeep(c.transform, "GameOver");
                if (t != null) { gameOverUI = t.gameObject; break; }
            }
        }

        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
            // Boost the game over UI canvas above the tint canvas (sortingOrder 999)
            Canvas gc = gameOverUI.GetComponentInParent<Canvas>();
            if (gc != null) gc.sortingOrder = 999;
            gameOverUI.transform.SetAsLastSibling();
            CanvasGroup cg = gameOverUI.GetComponent<CanvasGroup>()
                          ?? gameOverUI.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInUI(cg));
        }

        if (gameOverText != null)
        {
            gameOverText.transform.localScale = Vector3.zero;
            StartCoroutine(ScaleInObj(gameOverText.gameObject, 0.2f));
        }

        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(false);
            StartCoroutine(ShowRestartButton());
        }
    }

    // â”€â”€ Coroutines â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // Creates a full-screen black overlay on a dedicated canvas (sortingOrder 998)
    // so it covers all normal HUD canvases regardless of scene hierarchy.
    private void BuildAndFadeDeathTint()
    {
        var cvGO = new GameObject("__DeathTintCanvas");
        var cv = cvGO.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 998;
        cvGO.AddComponent<CanvasScaler>();

        var tGO = new GameObject("Tint");
        tGO.transform.SetParent(cvGO.transform, false);
        var rt = tGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = tGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = false;
        StartCoroutine(FadeTintDynamic(img));
    }

    IEnumerator FadeTintDynamic(Image img)
    {
        float elapsed = 0f;
        while (elapsed < tintDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            img.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, deathTintColor.a, elapsed / tintDuration));
            yield return null;
        }
        img.color = new Color(0f, 0f, 0f, deathTintColor.a);
    }

    IEnumerator FadeTint()
    {
        float elapsed = 0f;
        while (elapsed < tintDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            Color c = deathTintColor;
            c.a = Mathf.Lerp(0f, deathTintColor.a, elapsed / tintDuration);
            screenTint.color = c;
            yield return null;
        }
        screenTint.color = deathTintColor;
    }

    IEnumerator FadeInUI(CanvasGroup cg)
    {
        float elapsed = 0f, duration = 0.5f;
        while (elapsed < duration)
        {
            elapsed  += Time.unscaledDeltaTime;
            cg.alpha  = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    IEnumerator ScaleInObj(GameObject obj, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        float elapsed = 0f, duration = 0.5f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = elapsed / duration;
            float s  = Mathf.Sin(t * Mathf.PI * 0.5f) * 1.2f;
            obj.transform.localScale = Vector3.one * s;
            yield return null;
        }
        obj.transform.localScale = Vector3.one;
    }

    IEnumerator ShowRestartButton()
    {
        yield return new WaitForSecondsRealtime(restartButtonDelay);
        restartButton.gameObject.SetActive(true);
        restartButton.transform.localScale = Vector3.zero;

        float elapsed = 0f, duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = elapsed / duration;
            float s  = Mathf.Sin(t * Mathf.PI * 0.5f) * 1.1f;
            restartButton.transform.localScale = Vector3.one * s;
            yield return null;
        }
        restartButton.transform.localScale = Vector3.one;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        // Full restart from the beginning â€” clear per-run persistent state
        PlayerPrefs.DeleteKey("PlayerHasArmor");
        PlayerPrefs.DeleteKey("PlayerHasBow");
        PlayerPrefs.Save();
        SaveManager.Instance?.DeleteSave();
        SceneManager.LoadScene("MainMenu");
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
