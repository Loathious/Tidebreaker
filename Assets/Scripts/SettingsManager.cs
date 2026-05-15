using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Singleton that manages game settings: music volume, SFX volume, and pause via Escape.
/// Persists across scenes using DontDestroyOnLoad. Saves values to PlayerPrefs.
/// Re-wires panel references automatically each time a new scene loads.
///
/// Pause flow:
///   Escape in Village        → TogglePause()  (opens / closes pausePanelRoot)
///   Pause panel Settings btn → OpenFromPause() (opens settingsPanelRoot)
///   Escape while settings open from pause → CloseSettingsReturnToPause()
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [SerializeField] private GameObject      settingsPanelRoot;
    [SerializeField] private GameObject      pausePanelRoot;
    [SerializeField] private Slider          musicVolumeSlider;
    [SerializeField] private Slider          sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI musicVolumeLabel;
    [SerializeField] private TextMeshProUGUI sfxVolumeLabel;

    private float _musicVolume            = 0.5f;
    private float _sfxVolume              = 0.5f;
    private bool  _isPaused               = false;
    private bool  _settingsOpenedFromPause = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        _musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        _sfxVolume   = PlayerPrefs.GetFloat("SFXVolume",   0.5f);
        ApplySettings();
        WireSliderListeners();
        HidePanels();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reset pause state so entering a new scene always starts unpaused
        _isPaused                = false;
        _settingsOpenedFromPause = false;
        Time.timeScale           = 1f;

        // Re-find panel references in the new scene
        AutoWirePanels();
        WireSliderListeners();
        HidePanels();
        ApplySettings();
    }

    // ── Auto-wiring ───────────────────────────────────────────────────────────

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

    private void AutoWirePanels()
    {
        settingsPanelRoot = null;
        pausePanelRoot    = null;
        musicVolumeSlider = null;
        sfxVolumeSlider   = null;
        musicVolumeLabel  = null;
        sfxVolumeLabel    = null;

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay &&
                canvas.renderMode != RenderMode.ScreenSpaceCamera) continue;

            Transform root = canvas.transform;

            if (settingsPanelRoot == null)
            {
                var t = FindDeep(root, "SettingsPanel");
                if (t != null) settingsPanelRoot = t.gameObject;
            }

            if (pausePanelRoot == null)
            {
                var t = FindDeep(root, "PausePanel");
                if (t != null) pausePanelRoot = t.gameObject;
            }

            if (musicVolumeSlider == null)
            {
                var t = FindDeep(root, "MusicSlider");
                if (t != null) musicVolumeSlider = t.GetComponent<Slider>();
            }

            if (sfxVolumeSlider == null)
            {
                var t = FindDeep(root, "SFXSlider");
                if (t != null) sfxVolumeSlider = t.GetComponent<Slider>();
            }

            if (musicVolumeLabel == null)
            {
                var t = FindDeep(root, "MusicLabel");
                if (t != null) musicVolumeLabel = t.GetComponent<TextMeshProUGUI>();
            }

            if (sfxVolumeLabel == null)
            {
                var t = FindDeep(root, "SFXLabel");
                if (t != null) sfxVolumeLabel = t.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    private void WireSliderListeners()
    {
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            musicVolumeSlider.value = _musicVolume;
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
            sfxVolumeSlider.value = _sfxVolume;
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        // Update labels to match persisted values
        if (musicVolumeLabel != null)
            musicVolumeLabel.text = Mathf.RoundToInt(_musicVolume * 100) + "%";
        if (sfxVolumeLabel != null)
            sfxVolumeLabel.text = Mathf.RoundToInt(_sfxVolume * 100) + "%";
    }

    private void HidePanels()
    {
        if (settingsPanelRoot != null) settingsPanelRoot.SetActive(false);
        if (pausePanelRoot    != null) pausePanelRoot.SetActive(false);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (SceneManager.GetActiveScene().name != "Village") return;

        // If settings are open and were opened from the pause menu, close back to pause
        if (settingsPanelRoot != null && settingsPanelRoot.activeSelf && _settingsOpenedFromPause)
        {
            CloseSettingsReturnToPause();
            return;
        }

        // If settings are open (opened standalone), just close them
        if (settingsPanelRoot != null && settingsPanelRoot.activeSelf)
        {
            CloseSettings();
            return;
        }

        // Otherwise toggle the pause panel
        TogglePause();
    }

    // ── Pause ─────────────────────────────────────────────────────────────────

    /// <summary>Toggles pause state. Shows / hides pausePanelRoot.</summary>
    public void TogglePause()
    {
        if (!_isPaused)
        {
            _isPaused      = true;
            Time.timeScale = 0f;
            if (pausePanelRoot != null) pausePanelRoot.SetActive(true);
        }
        else
        {
            _isPaused      = false;
            Time.timeScale = 1f;
            if (pausePanelRoot    != null) pausePanelRoot.SetActive(false);
            if (settingsPanelRoot != null) settingsPanelRoot.SetActive(false);
            _settingsOpenedFromPause = false;
        }
    }

    // ── Settings panel ────────────────────────────────────────────────────────

    /// <summary>Opens settingsPanelRoot from inside the pause menu.</summary>
    public void OpenFromPause()
    {
        _settingsOpenedFromPause = true;
        if (settingsPanelRoot != null) settingsPanelRoot.SetActive(true);
    }

    /// <summary>Closes settingsPanelRoot and ensures pausePanelRoot stays visible.</summary>
    public void CloseSettingsReturnToPause()
    {
        if (settingsPanelRoot != null) settingsPanelRoot.SetActive(false);
        if (pausePanelRoot    != null) pausePanelRoot.SetActive(true);
        _settingsOpenedFromPause = false;
    }

    /// <summary>Shows the settings panel (standalone, not from pause).</summary>
    public void OpenSettings()
    {
        _settingsOpenedFromPause = false;
        if (settingsPanelRoot != null) settingsPanelRoot.SetActive(true);
    }

    /// <summary>Hides the settings panel.</summary>
    public void CloseSettings()
    {
        if (settingsPanelRoot != null) settingsPanelRoot.SetActive(false);
        _settingsOpenedFromPause = false;
    }

    // ── Volume callbacks ──────────────────────────────────────────────────────

    public void OnMusicVolumeChanged(float value)
    {
        _musicVolume = value;
        if (musicVolumeLabel != null)
            musicVolumeLabel.text = Mathf.RoundToInt(value * 100) + "%";
        MusicManager.Instance?.SetVolume(value);
        PlayerPrefs.SetFloat("MusicVolume", value);
        PlayerPrefs.Save();
    }

    public void OnSFXVolumeChanged(float value)
    {
        _sfxVolume = value;
        if (sfxVolumeLabel != null)
            sfxVolumeLabel.text = Mathf.RoundToInt(value * 100) + "%";
        PlayerPrefs.SetFloat("SFXVolume", value);
        PlayerPrefs.Save();
    }

    public void ApplySettings()
    {
        MusicManager.Instance?.SetVolume(_musicVolume);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    public void QuitToMainMenu()
    {
        _isPaused                = false;
        _settingsOpenedFromPause = false;
        Time.timeScale           = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
