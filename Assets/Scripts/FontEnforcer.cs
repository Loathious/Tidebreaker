using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies fonts to all TMP text in every scene.
///
/// Priority order:
///   1. Text with a <see cref="FontTag"/> component — FontTag handles itself, skip.
///   2. Text in FontConfig.Instance with a matching role — uses that font.
///   3. Falls back to PressStart2P SDF (the original hardcoded font).
///
/// To customise fonts: create Assets/Resources/FontConfig.asset via
///   Assets → Create → Game → Font Config, then assign per-role fonts.
/// To pin a single text element: attach <see cref="FontTag"/> and pick a role.
///
/// Public helper: <see cref="ApplyTo(TMP_Text)"/> skips FontTag-tagged elements.
/// </summary>
public static class FontEnforcer
{
    private const string FontResourcePath = "Fonts & Materials/PressStart2P-Regular SDF";
    private static TMP_FontAsset _cachedFont;
    private static GameObject    _runnerGO;

    /// <summary>The default PressStart2P font, loaded lazily.</summary>
    public static TMP_FontAsset Font
    {
        get
        {
            if (_cachedFont != null) return _cachedFont;
            _cachedFont = Resources.Load<TMP_FontAsset>(FontResourcePath)
                       ?? Resources.Load<TMP_FontAsset>("PressStart2P-Regular SDF");
            return _cachedFont;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded += (_, _) => SpawnRunner();
        SpawnRunner();
    }

    private static void SpawnRunner()
    {
        if (_runnerGO != null) return;
        _runnerGO = new GameObject("__FontEnforcer");
        Object.DontDestroyOnLoad(_runnerGO);
        _runnerGO.hideFlags = HideFlags.HideAndDontSave;
        _runnerGO.AddComponent<FontEnforcerRunner>();
    }

    /// <summary>
    /// Force a specific text component to use the appropriate font.
    /// Respects FontTag if present; otherwise applies the default font.
    /// </summary>
    public static void ApplyTo(TMP_Text t)
    {
        if (t == null) return;

        // FontTag manages itself — don't override it
        if (t.GetComponent<FontTag>() != null) return;

        TMP_FontAsset font = FontConfig.Instance?.defaultFont ?? Font;
        if (font != null && t.font != font) t.font = font;
    }

    internal static void ApplyToAll()
    {
        FontConfig cfg = FontConfig.Instance;
        TMP_FontAsset fallback = Font;

        foreach (var t in Object.FindObjectsByType<TMP_Text>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;

            // FontTag components handle themselves — don't fight them
            FontTag tag = t.GetComponent<FontTag>();
            if (tag != null) { tag.Apply(); continue; }

            // Apply default font from config, or PressStart2P
            TMP_FontAsset target = cfg?.defaultFont ?? fallback;
            if (target != null && t.font != target) t.font = target;
        }
    }
}

/// <summary>Hidden runner that polls every frame for ~5s after each scene load.</summary>
internal class FontEnforcerRunner : MonoBehaviour
{
    private float _timer;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        _timer = 5f;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode) => _timer = 5f;

    void Update()
    {
        if (_timer <= 0f) return;
        _timer -= Time.unscaledDeltaTime;
        FontEnforcer.ApplyToAll();
    }
}
