using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton that adds smooth black cinematic bars (top + bottom) during combat.
/// Bars slide in when combat starts and slide out when combat ends.
/// Auto-creates the bar GameObjects if they don't exist in the scene.
/// </summary>
public class CinematicBars : MonoBehaviour
{
    public static CinematicBars Instance { get; private set; }

    [Header("Bar Settings")]
    [SerializeField] private float barHeight    = 120f;  // thicker, more cinematic
    [SerializeField] private float slideInTime  = 0.55f;
    [SerializeField] private float slideOutTime = 0.75f;

    private RectTransform _topBar;
    private RectTransform _bottomBar;
    private bool          _barsVisible;
    private Coroutine     _animCoroutine;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        EnsureBars();
        // Start hidden
        SetBarPositions(0f);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowBars()
    {
        if (_barsVisible) return;
        _barsVisible = true;
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateBars(0f, 1f, slideInTime));
    }

    public void HideBars()
    {
        if (!_barsVisible) return;
        _barsVisible = false;
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateBars(1f, 0f, slideOutTime));
    }

    // ── Internals ─────────────────────────────────────────────────────────────
    IEnumerator AnimateBars(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(from, to, elapsed / duration);
            SetBarPositions(t);
            yield return null;
        }
        SetBarPositions(to);
    }

    /// <param name="t">0 = fully hidden, 1 = fully visible</param>
    void SetBarPositions(float t)
    {
        // When t = 0 the bars sit completely offscreen; when t = 1 they rest flush
        // against the top / bottom edge of the screen, occupying barHeight pixels
        // of the canvas vertically.
        if (_topBar != null)
        {
            // pivot is top-center; anchor is top of canvas.
            // Hidden → anchoredPosition.y = +barHeight (top of bar floats above screen).
            // Shown  → anchoredPosition.y =  0         (top of bar at top of screen, extending down).
            _topBar.anchoredPosition = new Vector2(0f, barHeight * (1f - t));
        }
        if (_bottomBar != null)
        {
            // pivot is bottom-center; anchor is bottom of canvas.
            // Hidden → anchoredPosition.y = -barHeight (bottom of bar floats below screen).
            // Shown  → anchoredPosition.y =  0         (bottom of bar at bottom of screen, extending up).
            _bottomBar.anchoredPosition = new Vector2(0f, -barHeight * (1f - t));
        }
    }

    void EnsureBars()
    {
        // Find a SCREEN-SPACE canvas — never attach to world-space canvases
        // (e.g. enemy floating health-bar canvases) which would render the bars
        // in-world above NPCs instead of as a screen overlay.
        Canvas canvas = null;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay ||
                c.renderMode == RenderMode.ScreenSpaceCamera)
            {
                // Prefer the main game canvas if present
                if (c.name == "GameCanvas") { canvas = c; break; }
                if (canvas == null) canvas = c;
            }
        }
        if (canvas == null) return;

        Transform root = canvas.transform;

        if (_topBar == null)
            _topBar    = CreateBar("CinematicBarTop",    root, true);
        if (_bottomBar == null)
            _bottomBar = CreateBar("CinematicBarBottom", root, false);
    }

    RectTransform CreateBar(string goName, Transform parent, bool isTop)
    {
        // Reuse existing if already in scene
        Transform existing = FindDeep(parent, goName);
        if (existing != null) return existing.GetComponent<RectTransform>();

        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling(); // render on top

        Image img = go.AddComponent<Image>();
        img.color  = Color.black;
        img.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();

        if (isTop)
        {
            // Anchor stretched along the top edge; pivot at TOP so the bar
            // extends DOWNWARD from its anchored position into the screen.
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
        }
        else
        {
            // Anchor stretched along the bottom edge; pivot at BOTTOM so the
            // bar extends UPWARD from its anchored position into the screen.
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
        }

        rt.sizeDelta = new Vector2(0f, barHeight);

        return rt;
    }

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
