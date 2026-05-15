using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Self-contained splash/main-menu screen.
/// Builds all UI at runtime — no prefab needed. Attach to SplashCanvas in SplashScene.
/// Shows animated title, Play button, Settings (volume, fullscreen, quality).
/// </summary>
public class SplashScreen : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "SampleScene";

    // ── Theme colors matching the game ──────────────────────────────────────
    private static readonly Color BgColor       = new Color(0.05f, 0.05f, 0.09f, 1f);
    private static readonly Color TitleColor    = new Color(0.98f, 0.82f, 0.22f, 1f);
    private static readonly Color SubtitleColor = new Color(0.75f, 0.55f, 0.18f, 1f);
    private static readonly Color AccentColor   = new Color(0.85f, 0.18f, 0.12f, 1f);
    private static readonly Color ButtonNormal  = new Color(0.70f, 0.14f, 0.10f, 1f);
    private static readonly Color ButtonHover   = new Color(0.95f, 0.22f, 0.14f, 1f);
    private static readonly Color ButtonPress   = new Color(0.45f, 0.08f, 0.06f, 1f);
    private static readonly Color PanelBg      = new Color(0.07f, 0.07f, 0.11f, 0.97f);

    // ── Runtime refs ────────────────────────────────────────────────────────
    private CanvasGroup  _root;
    private GameObject   _menuPanel;
    private GameObject   _settingsPanel;
    private bool         _transitioning;

    private Slider       _volSlider;
    private Toggle       _fsToggle;
    private TMP_Dropdown _qualDropdown;

    void Awake()
    {
        SetupCanvas();
        BuildBackground();
        BuildTitle();
        _menuPanel     = BuildMenuPanel();
        _settingsPanel = BuildSettingsPanel();
        _settingsPanel.SetActive(false);
    }

    void Start()
    {
        LoadSettings();
        StartCoroutine(PlayIntro());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Canvas setup
    // ═══════════════════════════════════════════════════════════════════════
    void SetupCanvas()
    {
        Canvas c = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        c.renderMode  = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;

        CanvasScaler cs = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight  = 0.5f;

        if (!TryGetComponent<GraphicRaycaster>(out _))
            gameObject.AddComponent<GraphicRaycaster>();

        _root       = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _root.alpha = 0f;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Background + title
    // ═══════════════════════════════════════════════════════════════════════
    void BuildBackground()
    {
        FullscreenImage("BG",      BgColor,                         false);
        FullscreenImage("Vignette",new Color(0f,0f,0f,0.55f),      false);
        MakeBar("BarTop",    top:true);
        MakeBar("BarBottom", top:false);
        MakeAccentLine();
    }

    void MakeBar(string name, bool top)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<Image>().color = Color.black;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = top ? new Vector2(0,1) : Vector2.zero;
        rt.anchorMax = top ? Vector2.one      : new Vector2(1,0);
        rt.pivot     = top ? new Vector2(0.5f,1) : new Vector2(0.5f,0);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0, 55);
    }

    void MakeAccentLine()
    {
        GameObject go = new GameObject("AccentLine");
        go.transform.SetParent(transform, false);
        go.AddComponent<Image>().color = AccentColor;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.18f, 0.5f);
        rt.anchorMax = new Vector2(0.82f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 15);
        rt.sizeDelta        = new Vector2(0, 2);
    }

    void BuildTitle()
    {
        // Glow sprite behind text
        GameObject glowGo = new GameObject("TitleGlow");
        glowGo.transform.SetParent(transform, false);
        Image glow = glowGo.AddComponent<Image>();
        glow.color        = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.06f);
        glow.raycastTarget = false;
        RectTransform gr = glowGo.GetComponent<RectTransform>();
        gr.anchorMin = new Vector2(0,0.5f); gr.anchorMax = new Vector2(1,0.5f);
        gr.anchoredPosition = new Vector2(0, 130); gr.sizeDelta = new Vector2(0, 180);

        // Main title
        GameObject tGo = new GameObject("Title");
        tGo.transform.SetParent(transform, false);
        TextMeshProUGUI t = tGo.AddComponent<TextMeshProUGUI>();
        t.text = "JOURNEY OF ADVENTURES";
        t.fontSize = 76; t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = TitleColor;
        t.outlineWidth = 0.18f;
        t.outlineColor = new Color32(80,25,0,210);
        RectTransform tr = tGo.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0,0.5f); tr.anchorMax = new Vector2(1,0.5f);
        tr.anchoredPosition = new Vector2(0, 130); tr.sizeDelta = new Vector2(0, 110);

        // Subtitle
        GameObject sGo = new GameObject("Subtitle");
        sGo.transform.SetParent(transform, false);
        TextMeshProUGUI s = sGo.AddComponent<TextMeshProUGUI>();
        s.text = "AN EPIC PIXEL ADVENTURE";
        s.fontSize = 24; s.fontStyle = FontStyles.Italic;
        s.alignment = TextAlignmentOptions.Center;
        s.color = SubtitleColor; s.characterSpacing = 5;
        RectTransform sr = sGo.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0,0.5f); sr.anchorMax = new Vector2(1,0.5f);
        sr.anchoredPosition = new Vector2(0, 65); sr.sizeDelta = new Vector2(0, 45);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Menu panel
    // ═══════════════════════════════════════════════════════════════════════
    GameObject BuildMenuPanel()
    {
        GameObject panel = new GameObject("MenuPanel");
        panel.transform.SetParent(transform, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, -95);
        rt.sizeDelta = new Vector2(360, 10);

        VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 16; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddMenuBtn(panel.transform, "▶  PLAY",     () => StartCoroutine(TransitionToGame()));
        AddMenuBtn(panel.transform, "⚙  SETTINGS", ToggleSettings);
        AddMenuBtn(panel.transform, "✕  QUIT",     Application.Quit);

        return panel;
    }

    void AddMenuBtn(Transform parent, string label, System.Action action)
    {
        GameObject go = new GameObject(label + "_Btn");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(340, 56);

        Image bg = go.AddComponent<Image>();
        bg.color = ButtonNormal;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        ColorBlock cb = btn.colors;
        cb.normalColor = ButtonNormal; cb.highlightedColor = ButtonHover;
        cb.pressedColor = ButtonPress; cb.selectedColor = ButtonNormal;
        btn.colors = cb;
        btn.onClick.AddListener(() => action());

        GameObject lGo = new GameObject("Lbl");
        lGo.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = lGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 22; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        RectTransform lr = lGo.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.anchoredPosition = Vector2.zero; lr.sizeDelta = Vector2.zero;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Settings panel
    // ═══════════════════════════════════════════════════════════════════════
    GameObject BuildSettingsPanel()
    {
        GameObject panel = new GameObject("SettingsPanel");
        panel.transform.SetParent(transform, false);
        Image bg = panel.AddComponent<Image>(); bg.color = PanelBg;
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f,0.5f); rt.anchorMax = new Vector2(0.5f,0.5f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(540, 380);

        // Title
        MakeTMP(panel.transform, "SETTINGS", 28, FontStyles.Bold, TitleColor,
                new Vector2(0,1), new Vector2(1,1), new Vector2(0,-18), new Vector2(0,48));

        // Separator
        GameObject sep = new GameObject("Sep");
        sep.transform.SetParent(panel.transform, false);
        sep.AddComponent<Image>().color = AccentColor;
        RectTransform sepRt = sep.GetComponent<RectTransform>();
        sepRt.anchorMin = new Vector2(0.04f,1); sepRt.anchorMax = new Vector2(0.96f,1);
        sepRt.pivot = new Vector2(0.5f,1);
        sepRt.anchoredPosition = new Vector2(0,-68); sepRt.sizeDelta = new Vector2(0,2);

        // Rows container
        GameObject rows = new GameObject("Rows");
        rows.transform.SetParent(panel.transform, false);
        RectTransform rowsRt = rows.AddComponent<RectTransform>();
        rowsRt.anchorMin = new Vector2(0.05f,0.12f); rowsRt.anchorMax = new Vector2(0.95f,0.82f);
        rowsRt.anchoredPosition = Vector2.zero; rowsRt.sizeDelta = Vector2.zero;
        VerticalLayoutGroup vlg = rows.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 22; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperLeft;
        rows.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Volume
        _volSlider = MakeSliderRow(rows.transform, "Master Volume", 0f, 1f, AudioListener.volume);
        _volSlider.onValueChanged.AddListener(v => { AudioListener.volume = v; PlayerPrefs.SetFloat("MasterVolume", v); });

        // Fullscreen
        _fsToggle = MakeToggleRow(rows.transform, "Fullscreen", Screen.fullScreen);
        _fsToggle.onValueChanged.AddListener(v => { Screen.fullScreen = v; PlayerPrefs.SetInt("Fullscreen", v?1:0); });

        // Quality
        _qualDropdown = MakeDropdownRow(rows.transform, "Quality", QualitySettings.names, QualitySettings.GetQualityLevel());
        _qualDropdown.onValueChanged.AddListener(v => { QualitySettings.SetQualityLevel(v); PlayerPrefs.SetInt("Quality", v); });

        // Back button
        GameObject backGo = new GameObject("BackBtn");
        backGo.transform.SetParent(panel.transform, false);
        RectTransform backRt = backGo.AddComponent<RectTransform>();
        backRt.anchorMin = new Vector2(0.5f,0); backRt.anchorMax = new Vector2(0.5f,0);
        backRt.pivot = new Vector2(0.5f,0);
        backRt.anchoredPosition = new Vector2(0,14); backRt.sizeDelta = new Vector2(220,48);
        Image backBg = backGo.AddComponent<Image>(); backBg.color = ButtonNormal;
        Button backBtn = backGo.AddComponent<Button>(); backBtn.targetGraphic = backBg;
        ColorBlock cb2 = backBtn.colors;
        cb2.normalColor = ButtonNormal; cb2.highlightedColor = ButtonHover;
        cb2.pressedColor = ButtonPress; cb2.selectedColor = ButtonNormal;
        backBtn.colors = cb2;
        backBtn.onClick.AddListener(ToggleSettings);
        MakeTMP(backGo.transform, "← BACK", 20, FontStyles.Bold, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return panel;
    }

    Slider MakeSliderRow(Transform parent, string label, float min, float max, float val)
    {
        GameObject row = new GameObject(label + "Row");
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        row.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject lGo = new GameObject("Lbl");
        lGo.transform.SetParent(row.transform, false);
        TextMeshProUGUI lbl = lGo.AddComponent<TextMeshProUGUI>();
        lbl.text = label; lbl.fontSize = 19; lbl.color = Color.white;
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        LayoutElement le = lGo.AddComponent<LayoutElement>();
        le.preferredWidth = 175; le.preferredHeight = 34;

        GameObject sGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
        sGo.transform.SetParent(row.transform, false);
        Slider s = sGo.GetComponent<Slider>();
        s.minValue = min; s.maxValue = max; s.value = val;
        LayoutElement sle = sGo.AddComponent<LayoutElement>();
        sle.preferredWidth = 230; sle.preferredHeight = 34;
        return s;
    }

    Toggle MakeToggleRow(Transform parent, string label, bool val)
    {
        GameObject row = new GameObject(label + "Row");
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        row.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject lGo = new GameObject("Lbl");
        lGo.transform.SetParent(row.transform, false);
        TextMeshProUGUI lbl = lGo.AddComponent<TextMeshProUGUI>();
        lbl.text = label; lbl.fontSize = 19; lbl.color = Color.white;
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        LayoutElement le = lGo.AddComponent<LayoutElement>();
        le.preferredWidth = 175; le.preferredHeight = 34;

        GameObject tGo = DefaultControls.CreateToggle(new DefaultControls.Resources());
        tGo.transform.SetParent(row.transform, false);
        Toggle t = tGo.GetComponent<Toggle>();
        t.isOn = val;
        LayoutElement tle = tGo.AddComponent<LayoutElement>();
        tle.preferredWidth = 34; tle.preferredHeight = 34;
        return t;
    }

    TMP_Dropdown MakeDropdownRow(Transform parent, string label, string[] opts, int current)
    {
        GameObject row = new GameObject(label + "Row");
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        row.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject lGo = new GameObject("Lbl");
        lGo.transform.SetParent(row.transform, false);
        TextMeshProUGUI lbl = lGo.AddComponent<TextMeshProUGUI>();
        lbl.text = label; lbl.fontSize = 19; lbl.color = Color.white;
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        LayoutElement le = lGo.AddComponent<LayoutElement>();
        le.preferredWidth = 175; le.preferredHeight = 34;

        GameObject ddGo = new GameObject("DD");
        ddGo.transform.SetParent(row.transform, false);
        Image ddBg = ddGo.AddComponent<Image>(); ddBg.color = new Color(0.18f,0.18f,0.22f,1);
        TMP_Dropdown dd = ddGo.AddComponent<TMP_Dropdown>();
        dd.targetGraphic = ddBg;

        // Caption text child
        GameObject capGo = new GameObject("Caption");
        capGo.transform.SetParent(ddGo.transform, false);
        TextMeshProUGUI cap = capGo.AddComponent<TextMeshProUGUI>();
        cap.fontSize = 17; cap.color = Color.white; cap.alignment = TextAlignmentOptions.Center;
        RectTransform capRt = capGo.GetComponent<RectTransform>();
        capRt.anchorMin = Vector2.zero; capRt.anchorMax = Vector2.one;
        capRt.sizeDelta = Vector2.zero; capRt.anchoredPosition = Vector2.zero;
        dd.captionText = cap;

        foreach (string o in opts) dd.options.Add(new TMP_Dropdown.OptionData(o));
        dd.value = current; dd.RefreshShownValue();

        LayoutElement dle = ddGo.AddComponent<LayoutElement>();
        dle.preferredWidth = 230; dle.preferredHeight = 34;
        return dd;
    }

    void MakeTMP(Transform parent, string text, float fontSize, FontStyles style, Color color,
                 Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(text + "_TMP");
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.fontStyle = style;
        tmp.color = color; tmp.alignment = TextAlignmentOptions.Center;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f,1);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    Image FullscreenImage(string name, Color color, bool raycast)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        Image img = go.AddComponent<Image>();
        img.color = color; img.raycastTarget = raycast;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = Vector2.zero;
        return img;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Intro animation
    // ═══════════════════════════════════════════════════════════════════════
    IEnumerator PlayIntro()
    {
        yield return FadeCanvasGroup(_root, 0f, 1f, 0.65f);
        yield return PulseGlow(2, 0.30f);
        yield return new WaitForSeconds(0.25f);
        yield return SlideIn(_menuPanel.GetComponent<RectTransform>(), -100f, 0f, 0.45f);
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.SmoothStep(from, to, t / dur);
            yield return null;
        }
        cg.alpha = to;
    }

    IEnumerator SlideIn(RectTransform rt, float fromY, float toY, float dur)
    {
        CanvasGroup cg = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        Vector2 basePos = rt.anchoredPosition;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float ease = Mathf.SmoothStep(0f, 1f, t / dur);
            rt.anchoredPosition = basePos + new Vector2(0, Mathf.Lerp(fromY, toY, ease));
            cg.alpha = Mathf.Clamp01(ease * 2f);
            yield return null;
        }
        rt.anchoredPosition = basePos + new Vector2(0, toY);
        cg.alpha = 1f;
    }

    IEnumerator PulseGlow(int count, float halfPeriod)
    {
        Transform gt = transform.Find("TitleGlow");
        if (gt == null) yield break;
        Image glow = gt.GetComponent<Image>();
        for (int i = 0; i < count; i++)
        {
            float e = 0f;
            while (e < halfPeriod)
            {
                e += Time.unscaledDeltaTime;
                glow.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b,
                                       Mathf.Sin(e / halfPeriod * Mathf.PI) * 0.28f);
                yield return null;
            }
        }
        glow.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.06f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Button logic
    // ═══════════════════════════════════════════════════════════════════════
    void ToggleSettings()
    {
        bool open = !_settingsPanel.activeSelf;
        _settingsPanel.SetActive(open);
        _menuPanel.SetActive(!open);
        if (open) LoadSettings();
    }

    IEnumerator TransitionToGame()
    {
        if (_transitioning) yield break;
        _transitioning = true;

        GameObject flashGo = new GameObject("Flash");
        flashGo.transform.SetParent(transform, false);
        Image flash = flashGo.AddComponent<Image>();
        flash.color = new Color(1,1,1,0);
        RectTransform frt = flashGo.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.anchoredPosition = Vector2.zero; frt.sizeDelta = Vector2.zero;

        float t = 0f;
        while (t < 0.12f) { t += Time.unscaledDeltaTime; flash.color = new Color(1,1,1,t/0.12f); yield return null; }
        t = 0f;
        while (t < 0.35f) { t += Time.unscaledDeltaTime; flash.color = Color.Lerp(Color.white,Color.black,t/0.35f); yield return null; }

        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Settings persistence
    // ═══════════════════════════════════════════════════════════════════════
    void LoadSettings()
    {
        float vol = PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.volume = vol;
        _volSlider?.SetValueWithoutNotify(vol);

        bool fs = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        _fsToggle?.SetIsOnWithoutNotify(fs);

        int q = PlayerPrefs.GetInt("Quality", QualitySettings.GetQualityLevel());
        QualitySettings.SetQualityLevel(q);
        _qualDropdown?.SetValueWithoutNotify(q);
    }
}
