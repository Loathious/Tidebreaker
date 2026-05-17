using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Large screen-space boss health bar shown at the top of the screen.
/// Built procedurally on the overlay canvas — call BossHealthBar.Create(...).
/// Bosses update it via SetHealth() and SetPhase().
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    private Image           _fill;
    private Image           _delayedFill;
    private TextMeshProUGUI _nameText;
    private TextMeshProUGUI _phaseText;
    private CanvasGroup     _group;
    private float           _target = 1f;

    /// <summary>Creates and shows a boss bar with the given name.</summary>
    public static BossHealthBar Create(string bossName)
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return null;

        GameObject root = new GameObject("BossHealthBar");
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -6f);
        rt.sizeDelta = new Vector2(260f, 22f);   // sized for 320x180 pixel-art canvas

        var bar = root.AddComponent<BossHealthBar>();
        bar._group = root.AddComponent<CanvasGroup>();
        bar.Build(root.transform, bossName);
        bar._group.alpha = 0f;
        bar.StartCoroutine(bar.FadeIn());
        return bar;
    }

    private void Build(Transform parent, string bossName)
    {
        TMP_FontAsset font = FontEnforcer.Font;

        // Name
        GameObject nameGO = new GameObject("Name");
        nameGO.transform.SetParent(parent, false);
        var nRt = nameGO.AddComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0f, 1f); nRt.anchorMax = new Vector2(1f, 1f);
        nRt.pivot = new Vector2(0.5f, 1f);
        nRt.anchoredPosition = new Vector2(0f, 0f);
        nRt.sizeDelta = new Vector2(0f, 10f);
        _nameText = nameGO.AddComponent<TextMeshProUGUI>();
        _nameText.text = bossName;
        _nameText.alignment = TextAlignmentOptions.Center;
        _nameText.fontSize = 6f;
        _nameText.color = new Color(1f, 0.85f, 0.5f);
        _nameText.outlineWidth = 0.25f;
        _nameText.outlineColor = new Color32(0, 0, 0, 255);
        if (font != null) _nameText.font = font;

        // Bar frame
        GameObject frame = new GameObject("Frame");
        frame.transform.SetParent(parent, false);
        var fRt = frame.AddComponent<RectTransform>();
        fRt.anchorMin = new Vector2(0f, 0f); fRt.anchorMax = new Vector2(1f, 0f);
        fRt.pivot = new Vector2(0.5f, 0f);
        fRt.anchoredPosition = new Vector2(0f, 2f);
        fRt.sizeDelta = new Vector2(-6f, 10f);
        var frameImg = frame.AddComponent<Image>();
        frameImg.color = new Color(0.05f, 0.05f, 0.05f, 0.9f);

        // Delayed (white trail) fill
        _delayedFill = MakeFill(frame.transform, new Color(1f, 1f, 1f, 0.5f));
        // Main fill
        _fill = MakeFill(frame.transform, new Color(0.85f, 0.15f, 0.15f));

        // Phase text
        GameObject phGO = new GameObject("Phase");
        phGO.transform.SetParent(parent, false);
        var pRt = phGO.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0f, 0f); pRt.anchorMax = new Vector2(1f, 0f);
        pRt.pivot = new Vector2(0.5f, 1f);
        pRt.anchoredPosition = new Vector2(0f, 1f);
        pRt.sizeDelta = new Vector2(0f, 8f);
        _phaseText = phGO.AddComponent<TextMeshProUGUI>();
        _phaseText.text = "";
        _phaseText.alignment = TextAlignmentOptions.Center;
        _phaseText.fontSize = 4f;
        _phaseText.color = new Color(1f, 1f, 1f, 0.85f);
        _phaseText.outlineWidth = 0.2f;
        _phaseText.outlineColor = new Color32(0, 0, 0, 255);
        if (font != null) _phaseText.font = font;
    }

    private static Image MakeFill(Transform parent, Color color)
    {
        GameObject go = new GameObject("Fill");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(2f, 2f); rt.offsetMax = new Vector2(-2f, -2f);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 1f;
        return img;
    }

    /// <summary>Sets the health ratio (0..1).</summary>
    public void SetHealth(float ratio)
    {
        _target = Mathf.Clamp01(ratio);
        if (_fill != null) _fill.fillAmount = _target;
    }

    /// <summary>Sets the small phase line under the bar.</summary>
    public void SetPhase(string phase)
    {
        if (_phaseText != null) _phaseText.text = phase;
    }

    void Update()
    {
        if (_delayedFill != null && _fill != null &&
            _delayedFill.fillAmount > _fill.fillAmount)
        {
            _delayedFill.fillAmount = Mathf.MoveTowards(
                _delayedFill.fillAmount, _fill.fillAmount, 0.4f * Time.deltaTime);
        }
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = t / 0.6f;
            yield return null;
        }
        _group.alpha = 1f;
    }

    /// <summary>Fades the bar out and destroys it.</summary>
    public void Dismiss()
    {
        StartCoroutine(FadeOutAndDie());
    }

    private IEnumerator FadeOutAndDie()
    {
        float t = 0f;
        float start = _group != null ? _group.alpha : 1f;
        while (t < 0.8f)
        {
            t += Time.unscaledDeltaTime;
            if (_group != null) _group.alpha = Mathf.Lerp(start, 0f, t / 0.8f);
            yield return null;
        }
        Destroy(gameObject);
    }

    private static Canvas FindOverlayCanvas()
    {
        Canvas found = null;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode != RenderMode.ScreenSpaceOverlay &&
                c.renderMode != RenderMode.ScreenSpaceCamera) continue;
            if (c.name == "GameCanvas") return c;
            if (found == null) found = c;
        }
        return found;
    }
}
