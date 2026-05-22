// ═══════════════════════════════════════════════════════════════════════════════
// HEALTH BAR — VERSION 2
//
// Drop-in replacement for HealthBar that requires zero Inspector wiring.
// A RuntimeInitializeOnLoadMethod bootstrapper auto-spawns one instance per
// scene-load and builds the entire UI in code:
//
//   • Green fill bar  (current HP)
//   • Red  decay bar  (lost HP, fades down after a brief delay)
//   • White HP text   (e.g. "85 / 100")
//
// The root canvas object is named "HealthBarV2_UI" so the JoA editor tools can
// locate it in play mode.  It is NOT DontDestroyOnLoad — each scene spawns its
// own instance so the bar always sits correctly in the active scene's UI stack.
// ═══════════════════════════════════════════════════════════════════════════════
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class HealthBarV2 : MonoBehaviour
{
    // ── Auto-spawn ─────────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // Only spawn if there isn't already one in the scene.
        if (FindFirstObjectByType<HealthBarV2>() != null) return;

        var go = new GameObject("HealthBarV2_Bootstrapper");
        go.AddComponent<HealthBarV2>();
    }

    // ── Tunables ───────────────────────────────────────────────────────────────
    [Header("Layout")]
    [SerializeField] private Vector2 barSize          = new Vector2(220f, 22f);
    [SerializeField] private Vector2 anchorPos        = new Vector2(16f, -16f); // top-left offset
    [SerializeField] private int     sortingOrder     = 10;

    [Header("Colours")]
    [SerializeField] private Color fillColor          = new Color(0.15f, 0.85f, 0.2f, 1f);  // green
    [SerializeField] private Color lostColor          = new Color(0.85f, 0.1f,  0.1f, 0.9f); // red
    [SerializeField] private Color bgColor            = new Color(0f,    0f,    0f,   0.55f); // dark bg

    [Header("Lost-health decay")]
    [SerializeField] private float decayDelay         = 0.75f;
    [SerializeField] private float decaySpeed         = 0.6f;

    // ── Internals ──────────────────────────────────────────────────────────────
    private Health    _playerHealth;
    private Image     _fillImg;
    private Image     _lostImg;
    private TextMeshProUGUI _text;
    private Canvas    _canvas;

    private float _lostFill;
    private float _delayTimer;
    private bool  _hidden;

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    private void Start()
    {
        BuildUI();
        HookPlayer();
    }

    private void Update()
    {
        if (_hidden || _lostImg == null || _fillImg == null) return;

        // Decay the red bar toward the green bar after a brief pause.
        if (_delayTimer > 0f)
        {
            _delayTimer -= Time.unscaledDeltaTime;
        }
        else if (_lostFill > _fillImg.fillAmount)
        {
            _lostFill = Mathf.MoveTowards(_lostFill, _fillImg.fillAmount,
                                           decaySpeed * Time.unscaledDeltaTime);
            _lostImg.fillAmount = _lostFill;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────────
    /// <summary>Hide the entire health bar (called by LevelManagerBase on death).</summary>
    public void Hide()
    {
        _hidden = true;
        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }

    /// <summary>Show the health bar again.</summary>
    public void Show()
    {
        _hidden = false;
        if (_canvas != null) _canvas.gameObject.SetActive(true);
    }

    // ── UI construction ────────────────────────────────────────────────────────
    private void BuildUI()
    {
        // Root canvas — named so JoaBuildTools can find it.
        var canvasGO = new GameObject("HealthBarV2_UI");
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = sortingOrder;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO); // keep across scene transitions; Hide() is called instead

        // ── Background strip ──────────────────────────────────────────────────
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRt  = bgGO.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 1f);
        bgRt.anchorMax = new Vector2(0f, 1f);
        bgRt.pivot     = new Vector2(0f, 1f);
        bgRt.anchoredPosition = anchorPos;
        bgRt.sizeDelta = barSize + new Vector2(4f, 4f); // 2px padding each side
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color         = bgColor;
        bgImg.raycastTarget = false;

        // ── Lost-health (red) bar — rendered first so it appears behind green ─
        var lostGO = new GameObject("LostHealth");
        lostGO.transform.SetParent(bgGO.transform, false);
        var lostRt = lostGO.AddComponent<RectTransform>();
        SetupBarRect(lostRt, barSize);
        _lostImg             = lostGO.AddComponent<Image>();
        _lostImg.color       = lostColor;
        _lostImg.type        = Image.Type.Filled;
        _lostImg.fillMethod  = Image.FillMethod.Horizontal;
        _lostImg.fillOrigin  = 0;
        _lostImg.fillAmount  = 1f;
        _lostImg.raycastTarget = false;
        _lostFill = 1f;

        // ── Current-health (green) bar — rendered on top ──────────────────────
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(bgGO.transform, false);
        var fillRt = fillGO.AddComponent<RectTransform>();
        SetupBarRect(fillRt, barSize);
        _fillImg             = fillGO.AddComponent<Image>();
        _fillImg.color       = fillColor;
        _fillImg.type        = Image.Type.Filled;
        _fillImg.fillMethod  = Image.FillMethod.Horizontal;
        _fillImg.fillOrigin  = 0;
        _fillImg.fillAmount  = 1f;
        _fillImg.raycastTarget = false;

        // ── HP text ───────────────────────────────────────────────────────────
        var textGO = new GameObject("HPText");
        textGO.transform.SetParent(bgGO.transform, false);
        var textRt = textGO.AddComponent<RectTransform>();
        textRt.anchorMin       = Vector2.zero;
        textRt.anchorMax       = Vector2.one;
        textRt.offsetMin       = new Vector2(4f, 0f);
        textRt.offsetMax       = new Vector2(-4f, 0f);
        _text                  = textGO.AddComponent<TextMeshProUGUI>();
        _text.fontSize         = 10f;
        _text.color            = Color.white;
        _text.alignment        = TextAlignmentOptions.Center;
        _text.raycastTarget    = false;
        _text.text             = "";
    }

    private static void SetupBarRect(RectTransform rt, Vector2 size)
    {
        rt.anchorMin       = new Vector2(0f, 0.5f);
        rt.anchorMax       = new Vector2(0f, 0.5f);
        rt.pivot           = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(2f, 0f); // 2px left padding from bg edge
        rt.sizeDelta       = size;
    }

    // ── Player hookup ──────────────────────────────────────────────────────────
    private void HookPlayer()
    {
        StartCoroutine(FindPlayerRoutine());
    }

    private IEnumerator FindPlayerRoutine()
    {
        // Wait a frame so all Start() calls finish before we search.
        yield return null;

        for (int attempts = 0; attempts < 60; attempts++)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                _playerHealth = playerGO.GetComponent<Health>()
                             ?? playerGO.GetComponentInChildren<Health>();
            }

            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged.AddListener(OnHealthChanged);
                _playerHealth.OnDeath.AddListener(OnPlayerDied);
                // Sync to current HP immediately.
                SetFill(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
                yield break;
            }

            yield return new WaitForSeconds(0.1f);
        }

        Debug.LogWarning("[HealthBarV2] Could not find a Player with a Health component.");
    }

    private void OnHealthChanged(float current, float max)
    {
        SetFill(current, max);
    }

    private void OnPlayerDied()
    {
        // Bar stays visible until LevelManagerBase calls Hide() as part of the
        // death sequence — no special handling needed here.
    }

    private void SetFill(float current, float max)
    {
        if (_fillImg == null) return;

        float fraction = max > 0f ? current / max : 0f;
        fraction = Mathf.Clamp01(fraction);

        if (fraction >= _fillImg.fillAmount)
        {
            // HP increased / reset — snap both bars up immediately (no phantom red gap).
            _fillImg.fillAmount = fraction;
            _lostFill           = fraction;
            if (_lostImg != null) _lostImg.fillAmount = fraction;
        }
        else
        {
            // HP decreased — green bar snaps down, red bar starts its decay timer.
            _fillImg.fillAmount = fraction;
            _delayTimer         = decayDelay;
            // _lostFill stays where it was; Update() will decay it.
        }

        if (_text != null)
            _text.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void OnDestroy()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged.RemoveListener(OnHealthChanged);
            _playerHealth.OnDeath.RemoveListener(OnPlayerDied);
        }
    }
}
