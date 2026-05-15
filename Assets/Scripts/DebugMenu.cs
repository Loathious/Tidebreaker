using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game developer testing menu.
/// Toggle: Ctrl + Shift + Alt + D
/// Only active in the Unity Editor or in Development Builds.
/// </summary>
public class DebugMenu : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private GameObject zombiePrefab;
    [SerializeField] private float      spawnOffset = 2f;

    [Header("Items")]
    [SerializeField] private ItemData   swordItemData;

    [Header("Audio")]
    [SerializeField] private AudioClip  swingClip;
    [SerializeField] private AudioClip  hitClip;
    [SerializeField] private AudioClip  musicClip;

    // ── UI state ────────────────────────────────────────────────────────────
    private bool      _visible;
    private Rect      _windowRect = new Rect(20, 20, 280, 480);
    private Vector2   _scroll;
    private GUIStyle  _headerStyle;
    private GUIStyle  _sectionStyle;
    private bool      _stylesInitialised;

    // ── cached refs ─────────────────────────────────────────────────────────
    private Health     _playerHealth;
    private Inventory  _inventory;
    private AudioSource _previewSource;

    // ── damage slider ────────────────────────────────────────────────────────
    private float _damageAmount = 10f;
    private float _musicVolume  = 0.5f;

    void Awake()
    {
        _previewSource             = gameObject.AddComponent<AudioSource>();
        _previewSource.playOnAwake = false;
        _previewSource.spatialBlend = 0f;
    }

    void Start()
    {
        _playerHealth = GameObject.FindGameObjectWithTag("Player")?.GetComponent<Health>();
        _inventory    = Inventory.Instance;
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool ctrl  = Input.GetKey(KeyCode.LeftControl)  || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift)    || Input.GetKey(KeyCode.RightShift);
        bool alt   = Input.GetKey(KeyCode.LeftAlt)      || Input.GetKey(KeyCode.RightAlt);

        if (ctrl && shift && alt && Input.GetKeyDown(KeyCode.D))
            _visible = !_visible;
#endif
    }

    void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!_visible) return;

        InitStyles();
        _windowRect = GUILayout.Window(9999, _windowRect, DrawWindow, "  Dev Menu  [Ctrl+Shift+Alt+D]");
#endif
    }

    void DrawWindow(int id)
    {
        _scroll = GUILayout.BeginScrollView(_scroll);

        // ── Player ──────────────────────────────────────────────────────────
        GUILayout.Label("PLAYER", _headerStyle);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Damage: {_damageAmount:F0}", GUILayout.Width(90));
        _damageAmount = GUILayout.HorizontalSlider(_damageAmount, 1f, 100f);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Deal Damage to Player"))
        {
            if (_playerHealth != null) _playerHealth.TakeDamage(_damageAmount);
            else Debug.LogWarning("[DebugMenu] Player Health not found.");
        }

        if (GUILayout.Button("Heal Player to Full"))
        {
            if (_playerHealth != null) _playerHealth.Heal(_playerHealth.MaxHealth);
            else Debug.LogWarning("[DebugMenu] Player Health not found.");
        }

        if (GUILayout.Button("Kill Player"))
        {
            if (_playerHealth != null) _playerHealth.TakeDamage(_playerHealth.MaxHealth * 10f);
            else Debug.LogWarning("[DebugMenu] Player Health not found.");
        }

        Separator();

        // ── Items ───────────────────────────────────────────────────────────
        GUILayout.Label("ITEMS", _headerStyle);

        if (GUILayout.Button("Give Sword"))
        {
            if (_inventory != null && swordItemData != null)
                _inventory.AddItem(swordItemData);
            else
                Debug.LogWarning("[DebugMenu] Inventory or SwordItemData not assigned.");
        }

        Separator();

        // ── Spawning ────────────────────────────────────────────────────────
        GUILayout.Label("SPAWNING", _headerStyle);

        if (GUILayout.Button("Spawn Zombie Next to Player"))
            SpawnZombie();

        if (GUILayout.Button("Kill All Zombies"))
            KillAllZombies();

        Separator();

        // ── Audio ───────────────────────────────────────────────────────────
        GUILayout.Label("AUDIO", _headerStyle);

        if (GUILayout.Button("Play Sword Swing"))
        {
            if (swingClip != null) _previewSource.PlayOneShot(swingClip);
            else Debug.LogWarning("[DebugMenu] Swing clip not assigned.");
        }

        if (GUILayout.Button("Play Sword Hit"))
        {
            if (hitClip != null) _previewSource.PlayOneShot(hitClip);
            else Debug.LogWarning("[DebugMenu] Hit clip not assigned.");
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Music Vol: {_musicVolume:F2}", GUILayout.Width(100));
        _musicVolume = GUILayout.HorizontalSlider(_musicVolume, 0f, 1f);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Apply Music Volume"))
            MusicManager.Instance?.SetVolume(_musicVolume);

        if (GUILayout.Button("Stop Music"))
            MusicManager.Instance?.Stop();

        if (GUILayout.Button("Resume Music"))
            MusicManager.Instance?.Resume();

        Separator();

        // ── Scene ───────────────────────────────────────────────────────────
        GUILayout.Label("SCENE", _headerStyle);

        if (GUILayout.Button("Restart Scene"))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void SpawnZombie()
    {
        if (zombiePrefab == null) { Debug.LogWarning("[DebugMenu] Zombie prefab not assigned."); return; }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 pos = player != null
            ? player.transform.position + Vector3.right * spawnOffset
            : Vector3.zero;

        Instantiate(zombiePrefab, pos, Quaternion.identity);
    }

    void KillAllZombies()
    {
        foreach (ZombieAI z in FindObjectsByType<ZombieAI>(FindObjectsSortMode.None))
        {
            Health h = z.GetComponent<Health>();
            if (h != null && !h.IsDead) h.TakeDamage(h.MaxHealth * 10f);
        }
    }

    void Separator() => GUILayout.Space(8);

    void InitStyles()
    {
        if (_stylesInitialised) return;
        _stylesInitialised = true;

        _headerStyle           = new GUIStyle(GUI.skin.label);
        _headerStyle.fontStyle = FontStyle.Bold;
        _headerStyle.fontSize  = 12;
        _headerStyle.normal.textColor = new Color(1f, 0.8f, 0.3f);
    }
}
