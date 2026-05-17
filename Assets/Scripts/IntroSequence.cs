using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cinematic opening for the Village scene.
///
/// Flow when the scene loads:
///   1. Player input is locked (no movement, no attack).
///   2. Camera zooms out and floats above the ruined village.
///   3. Story text from the spelmanus fades in/out over a slow pan.
///   4. Camera eases down to the player.
///   5. HUD fades in, control is returned, the villager dialogue plays as soon
///      as the player walks toward Villager1.
///
/// Auto-creates the text overlay UI on a found ScreenSpaceOverlay Canvas — no
/// scene wiring required.
/// </summary>
public class IntroSequence : MonoBehaviour
{
    [Header("Camera Pan / Zoom (all customizable in Inspector)")]
    [Tooltip("Camera orthographic size during the intro pan. Larger = wider view of the village.")]
    [SerializeField] private float introOrthoSize    = 9f;
    [Tooltip("Camera orthographic size restored at the end of the intro for normal gameplay.")]
    [SerializeField] private float gameplayOrthoSize = 5f;
    [Tooltip("World X position the camera starts at.")]
    [SerializeField] private float panStartX = -18f;
    [Tooltip("World X position the camera ends at.")]
    [SerializeField] private float panEndX   =  18f;
    [Tooltip("World Y (height) the camera floats at during the pan.")]
    [SerializeField] private float panHeight = -1f;
    [Tooltip("How long the slow horizontal pan takes, in seconds.")]
    [SerializeField] private float panDuration = 6.5f;
    [Tooltip("How long the camera takes to ease down to the player at the end.")]
    [SerializeField] private float zoomToPlayerTime = 1.6f;
    [Tooltip("Vertical offset added when easing onto the player at the end.")]
    [SerializeField] private float playerEaseOffsetY = 2f;

    [Header("Story text (from spelmanus)")]
    [SerializeField, TextArea(2, 4)] private string line1 =
        "Eldenmoor lay in ruins. The monsters had come at dusk and taken the villagers into the dark.";
    [SerializeField, TextArea(2, 4)] private string line2 =
        "You are the last who escaped. The last who remembers what this place was.";
    [SerializeField, TextArea(2, 4)] private string line3 =
        "Take up the sword. Save them. Reclaim Eldenmoor.";

    [Header("Story text style")]
    [Tooltip("Font asset for the intro story text. Auto-loads PressStart2P-Regular SDF if left empty.")]
    [SerializeField] private TMP_FontAsset introFont;
    [Tooltip("Font size in canvas reference units. Pixel-art games typically want this small (e.g. 10).")]
    [SerializeField] private float fontSize = 10f;

    [Header("Timings")]
    [SerializeField] private float lineFadeIn  = 0.6f;
    [SerializeField] private float lineHold    = 1.9f;
    [SerializeField] private float lineFadeOut = 0.5f;

    [Header("Overlay sizing")]
    [Tooltip("How wide the story-text rect spans the screen (0..1, anchor min/max). Lower = more centered/smaller.")]
    [Range(0.1f, 1f)] [SerializeField] private float textRectWidth = 0.5f;
    [Tooltip("Vertical center of the story-text band (0..1, where 0=bottom, 1=top). Default sits in the lower-third.")]
    [Range(0f, 1f)] [SerializeField] private float textRectCenterY = 0.28f;
    [Tooltip("Vertical height of the story-text rect (0..1).")]
    [Range(0.05f, 0.5f)] [SerializeField] private float textRectHeight = 0.18f;

    private Camera          _cam;
    private CameraFollow    _camFollow;
    private PlayerController _player;
    private TextMeshProUGUI _text;
    private CanvasGroup     _vignette;

    void Awake()
    {
        _cam       = Camera.main;
        _camFollow = _cam != null ? _cam.GetComponent<CameraFollow>() : null;
        _player    = FindFirstObjectByType<PlayerController>();

        // Stop music early in Awake so it can't race with MusicManager.Start()
        // (IntroSequence.Run() also calls Stop(), but Awake runs before any Start())
        MusicManager.Instance?.Stop();
    }

    void Start()
    {
        // Safety: only run the intro if this is actually a gameplay scene
        // (player + camera + camera-follow all present). This prevents the
        // sequence from running in MainMenu / settings scenes if the GO is
        // accidentally placed there.
        if (_player == null || _cam == null || _camFollow == null) return;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // Lock player from moving / attacking during the intro
        _player?.LockInput();

        // Silence music during the cinematic pan — it will resume when the player
        // gets control at the end of the sequence.
        MusicManager.Instance?.Stop();

        // Build the overlay UI (text + dimmer) procedurally so the scene needs no wiring
        BuildOverlayUI();

        // Disable normal follow so we can drive the camera by hand
        if (_camFollow != null) _camFollow.enabled = false;

        // Establishing shot — wide view above the village
        if (_cam != null)
        {
            _cam.orthographicSize     = introOrthoSize;
            _cam.transform.position   = new Vector3(panStartX, panHeight, -10f);
        }

        // Hold for a beat before the first line
        yield return new WaitForSecondsRealtime(0.4f);

        // Fade in vignette darken
        if (_vignette != null) yield return Fade(_vignette, 0f, 0.45f, 0.6f);

        // Slow pan + 3 story lines timed across the pan duration
        StartCoroutine(SlowPan(panStartX, panEndX, panDuration));

        yield return ShowLine(line1);
        yield return ShowLine(line2);
        yield return ShowLine(line3);

        // Wait for the pan to finish if it hasn't
        yield return new WaitForSecondsRealtime(0.2f);

        // Ease down to the player + zoom back to gameplay size
        if (_cam != null && _player != null)
            yield return EaseToPlayer(_cam.transform.position, _player.transform.position);

        // Fade out vignette + re-enable follow
        if (_vignette != null) yield return Fade(_vignette, _vignette.alpha, 0f, 0.5f);
        if (_camFollow != null) _camFollow.enabled = true;
        if (_cam != null) _cam.orthographicSize = gameplayOrthoSize;

        _player?.UnlockInput();

        // Start music now that the player has control
        MusicManager.Instance?.Resume();

        // Clean up the overlay (it served its purpose)
        if (_text != null && _text.transform.parent != null)
            Destroy(_text.transform.parent.gameObject);
    }

    // ── Coroutine helpers ─────────────────────────────────────────────────────
    IEnumerator SlowPan(float fromX, float toX, float duration)
    {
        if (_cam == null) yield break;
        float t = 0f;
        Vector3 a = new Vector3(fromX, panHeight, -10f);
        Vector3 b = new Vector3(toX,   panHeight, -10f);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            _cam.transform.position = Vector3.Lerp(a, b, k);
            yield return null;
        }
        _cam.transform.position = b;
    }

    IEnumerator EaseToPlayer(Vector3 from, Vector3 toWorld)
    {
        if (_cam == null) yield break;
        float t = 0f;
        Vector3 target = new Vector3(toWorld.x, toWorld.y + playerEaseOffsetY, -10f);
        float startSize = _cam.orthographicSize;
        while (t < zoomToPlayerTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / zoomToPlayerTime);
            _cam.transform.position    = Vector3.Lerp(from, target, k);
            _cam.orthographicSize      = Mathf.Lerp(startSize, gameplayOrthoSize, k);
            yield return null;
        }
        _cam.transform.position = target;
        _cam.orthographicSize   = gameplayOrthoSize;
    }

    IEnumerator ShowLine(string line)
    {
        if (_text == null) yield break;
        _text.text = line;
        yield return Fade(_text, 0f, 1f, lineFadeIn);
        yield return new WaitForSecondsRealtime(lineHold);
        yield return Fade(_text, 1f, 0f, lineFadeOut);
    }

    IEnumerator Fade(TMP_Text tmp, float from, float to, float duration)
    {
        if (tmp == null) yield break;
        float t = 0f;
        Color c = tmp.color;
        tmp.color = new Color(c.r, c.g, c.b, from);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            tmp.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        tmp.color = new Color(c.r, c.g, c.b, to);
    }

    IEnumerator Fade(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        float t = 0f;
        cg.alpha = from;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    // ── Procedural UI ─────────────────────────────────────────────────────────
    void BuildOverlayUI()
    {
        // Find or create a ScreenSpaceOverlay canvas to host the intro overlay
        Canvas canvas = null;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }
        }
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("IntroCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Container with a dim vignette
        GameObject root = new GameObject("IntroOverlay");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _vignette         = root.AddComponent<CanvasGroup>();
        _vignette.alpha   = 0f;

        // Dimmer image (full screen, dark)
        GameObject dim = new GameObject("Dim");
        dim.transform.SetParent(root.transform, false);
        RectTransform dimRt = dim.AddComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        Image dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.55f);
        dimImg.raycastTarget = false;

        // Story text — sits in a smaller, customizable rect band on the canvas.
        // Anchors are derived from the textRectWidth / textRectCenterY / textRectHeight
        // serialized fields so the overlay size can be tuned in the Inspector.
        GameObject txt = new GameObject("IntroText");
        txt.transform.SetParent(root.transform, false);
        RectTransform tRt = txt.AddComponent<RectTransform>();
        float halfW = Mathf.Clamp(textRectWidth, 0.1f, 1f) * 0.5f;
        float halfH = Mathf.Clamp(textRectHeight, 0.05f, 0.5f) * 0.5f;
        float cx    = 0.5f;
        float cy    = Mathf.Clamp01(textRectCenterY);
        tRt.anchorMin = new Vector2(cx - halfW, cy - halfH);
        tRt.anchorMax = new Vector2(cx + halfW, cy + halfH);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        _text               = txt.AddComponent<TextMeshProUGUI>();
        _text.alignment     = TextAlignmentOptions.Center;
        _text.fontStyle     = FontStyles.Normal;     // PressStart2P doesn't ship a bold variant
        _text.textWrappingMode = TMPro.TextWrappingModes.Normal;
        _text.color         = new Color(1f, 1f, 1f, 0f);
        _text.outlineWidth  = 0.22f;
        _text.outlineColor  = new Color32(0, 0, 0, 255);
        _text.text          = "";

        // Apply the user-configured font size (default 10, intentionally small for pixel-art)
        _text.fontSize = Mathf.Max(2f, fontSize);

        // Resolve the font: explicit Inspector field wins, otherwise auto-load PressStart2P-Regular SDF
        TMP_FontAsset font = introFont;
        if (font == null)
        {
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/PressStart2P-Regular SDF")
                ?? Resources.Load<TMP_FontAsset>("PressStart2P-Regular SDF");
        }
        if (font != null) _text.font = font;
    }
}