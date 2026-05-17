using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Singleton objective display.
/// Hidden on startup. Call ShowObjective() after villager dialogue to slide in.
/// Two-color display using TMP rich text: label in gold, content in white.
/// </summary>
public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private RectTransform panelRect;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI objectiveText;  // single TMP, rich-text

    [Header("Animation")]
    [SerializeField] private float slideInDuration  = 0.5f;
    [SerializeField] private float slideOutDuration = 0.35f;
    [SerializeField] private float slideOffsetY     = 80f;  // pixels to slide in from above

    // Current content (the non-label part)
    private string      _currentContent = "";
    private Vector2     _shownAnchoredPos;
    private Vector2     _hiddenAnchoredPos;
    private Coroutine   _animCoroutine;
    private Coroutine   _autoHideCoroutine;
    private bool        _visible;

    [Header("Auto-Hide")]
    [SerializeField] private float autoHideDelay = 4f;   // seconds before sliding out

    // Rich-text colours
    private const string LabelHex   = "FFD933"; // gold
    private const string ContentHex = "FFFFFF"; // white

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // Auto-wire panel
        if (panelRect == null)
            panelRect = GetComponent<RectTransform>();

        // Auto-wire text — look for "ObjectiveContent", then "ObjectiveText", then any TMP child
        if (objectiveText == null)
            objectiveText = FindDeepTMP("ObjectiveContent")
                         ?? FindDeepTMP("ObjectiveText")
                         ?? GetComponentInChildren<TextMeshProUGUI>(true);

        // Apply thick outline so it reads on any background (the user wants a chunky stroke)
        if (objectiveText != null)
        {
            objectiveText.richText       = true;
            objectiveText.fontStyle      = FontStyles.Normal;   // PressStart2P has no bold variant
            objectiveText.outlineWidth   = 0.32f;                       // ← thicker stroke
            objectiveText.outlineColor   = new Color32(0, 0, 0, 255);   // pure black
        }

        // Start hidden (panel slides up off screen)
        if (panelRect != null)
        {
            _shownAnchoredPos  = panelRect.anchoredPosition;
            _hiddenAnchoredPos = _shownAnchoredPos + new Vector2(0f, slideOffsetY);
            panelRect.anchoredPosition = _hiddenAnchoredPos;
        }
        SetAlpha(0f);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Shows the panel with the given objective content, then auto-hides.</summary>
    public void ShowObjective(string content)
    {
        _currentContent = content;
        SetText(content);
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateIn());
        RestartAutoHide();
    }

    /// <summary>Updates objective text; shows the panel if hidden, then auto-hides.</summary>
    public void UpdateObjective(string content)
    {
        _currentContent = content;
        SetText(content);
        if (!_visible)
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateIn());
        }
        else if (objectiveText != null)
        {
            // Already visible — punch the text to highlight the change
            StartCoroutine(PunchScale(objectiveText.rectTransform, 1.18f, 0.28f));
        }
        RestartAutoHide();
    }

    /// <summary>Slides the panel out and hides it immediately.</summary>
    public void HideObjective()
    {
        if (_autoHideCoroutine != null) StopCoroutine(_autoHideCoroutine);
        if (!_visible) return;
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateOut());
    }

    private void RestartAutoHide()
    {
        if (_autoHideCoroutine != null) StopCoroutine(_autoHideCoroutine);
        _autoHideCoroutine = StartCoroutine(AutoHideAfterDelay());
    }

    private IEnumerator AutoHideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(autoHideDelay);
        HideObjective();
        _autoHideCoroutine = null;
    }

    // ── Internals ─────────────────────────────────────────────────────────────
    void SetText(string content)
    {
        if (objectiveText == null) return;
        objectiveText.text = $"<color=#{LabelHex}>Objective:</color> <color=#{ContentHex}>{content}</color>";
    }

    IEnumerator AnimateIn()
    {
        _visible = true;
        float elapsed = 0f;
        while (elapsed < slideInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideInDuration);
            if (panelRect != null)
                panelRect.anchoredPosition = Vector2.Lerp(_hiddenAnchoredPos, _shownAnchoredPos, t);
            SetAlpha(t);
            yield return null;
        }
        if (panelRect != null) panelRect.anchoredPosition = _shownAnchoredPos;
        SetAlpha(1f);

        // Punch scale on text once panel is in place — draws the eye to the new objective
        if (objectiveText != null)
            yield return StartCoroutine(PunchScale(objectiveText.rectTransform, 1.25f, 0.35f));
    }

    /// <summary>One-shot scale punch — used on appear and on every UpdateObjective().</summary>
    IEnumerator PunchScale(RectTransform rt, float peak, float duration)
    {
        if (rt == null) yield break;
        float half  = duration * 0.5f;
        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(1f, peak, t / half);
            rt.localScale = new Vector3(k, k, 1f);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(peak, 1f, t / half);
            rt.localScale = new Vector3(k, k, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    IEnumerator AnimateOut()
    {
        _visible = false;
        float elapsed = 0f;
        Vector2 startPos = panelRect != null ? panelRect.anchoredPosition : _shownAnchoredPos;
        while (elapsed < slideOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideOutDuration);
            if (panelRect != null)
                panelRect.anchoredPosition = Vector2.Lerp(startPos, _hiddenAnchoredPos, t);
            SetAlpha(1f - t);
            yield return null;
        }
        if (panelRect != null) panelRect.anchoredPosition = _hiddenAnchoredPos;
        SetAlpha(0f);
    }

    void SetAlpha(float a)
    {
        if (objectiveText == null) return;
        Color c = objectiveText.color;
        c.a = a;
        objectiveText.color = c;
    }

    // ── Auto-wiring helpers ───────────────────────────────────────────────────
    private TextMeshProUGUI FindDeepTMP(string goName)
    {
        var t = FindDeep(transform, goName);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
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
