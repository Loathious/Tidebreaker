using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main menu + cinematic intro sequence.
/// Story content matches "Journey of Adventures" spelmanus dokument.
///
/// Play button flow (New Game):
///   1. DeleteSave → Fade to black → story panels → Load Village
///
/// Load Game button (shown only when a save exists):
///   Calls SaveManager.LoadGame() which restores scene/health/inventory.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private GameObject      storyPanel;
    [SerializeField] private TextMeshProUGUI storyText;
    [SerializeField] private Image           screenFade;

    private Button _playButton;
    private Button _loadGameButton;

    // Story content based on spelmanus dokument
    // Shown one paragraph at a time before Village loads
    private static readonly string[] StoryPages = new[]
    {
        "The once-peaceful village of Eldenmoor was a place of laughter and light.\n\nChildren played in the meadows. Blacksmiths forged tools at the anvil. Birds filled the morning air with song.",
        "Then, without warning — a roar from the forest.\n\nMonsters poured from the treeline, seizing the villagers in the dead of night.\n\nBy dawn, the village had fallen.",
        "You are the last hope.\n\nTake up the rusty sword. Defeat the monsters. Free the people of Eldenmoor.\n\nYour journey begins now."
    };

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

    void Start()
    {
        // Unload any stray game scenes
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.name != "MainMenu" && scene.isLoaded)
                SceneManager.UnloadSceneAsync(scene);
        }

        Time.timeScale = 1f;
        AutoWireFields();
        SetupMenuButtons();

        // Initial state
        if (screenFade != null)
        {
            Color c = screenFade.color;
            screenFade.color = new Color(c.r, c.g, c.b, 1f); // start fully black
            // CRITICAL: a fullscreen transparent Image with raycastTarget=true silently
            // eats every mouse click and breaks all buttons underneath. Force it off.
            screenFade.raycastTarget = false;
        }
        if (storyPanel != null) storyPanel.SetActive(false);

        StartCoroutine(IntroFadeIn());
    }

    private void AutoWireFields()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay &&
                canvas.renderMode != RenderMode.ScreenSpaceCamera) continue;

            Transform root = canvas.transform;

            if (titleText   == null) { var t = FindDeep(root, "TitleText");   if (t) titleText   = t.GetComponent<TextMeshProUGUI>(); }
            if (subtitleText == null) { var t = FindDeep(root, "SubtitleText"); if (t) subtitleText = t.GetComponent<TextMeshProUGUI>(); }
            if (storyPanel   == null) { var t = FindDeep(root, "StoryPanel");   if (t) storyPanel   = t.gameObject; }
            if (storyText    == null) { var t = FindDeep(root, "StoryText");    if (t) storyText    = t.GetComponent<TextMeshProUGUI>(); }
            if (screenFade   == null)
            {
                var t = FindDeep(root, "ScreenFade") ?? FindDeep(root, "Fade");
                if (t) screenFade = t.GetComponent<Image>();
            }
        }
    }

    // ── Menu entrance ─────────────────────────────────────────────────────────
    IEnumerator IntroFadeIn()
    {
        // Start with black screen, fade in over 1 second
        yield return StartCoroutine(FadeScreen(1f, 0f, 1.0f));

        // Fade + scale in the title
        if (titleText != null)
        {
            titleText.color = new Color(titleText.color.r, titleText.color.g, titleText.color.b, 0f);
            titleText.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

            float elapsed = 0f, dur = 0.9f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t  = Mathf.SmoothStep(0f, 1f, elapsed / dur);
                titleText.color = new Color(titleText.color.r, titleText.color.g, titleText.color.b, t);
                titleText.transform.localScale = Vector3.Lerp(new Vector3(0.85f, 0.85f, 1f), Vector3.one, t);
                yield return null;
            }
            titleText.color = new Color(titleText.color.r, titleText.color.g, titleText.color.b, 1f);
            titleText.transform.localScale = Vector3.one;

            // Subtle continuous pulse so the title feels alive
            StartCoroutine(TitlePulse());
        }

        // Fade in subtitle
        if (subtitleText != null)
        {
            subtitleText.color = new Color(subtitleText.color.r, subtitleText.color.g, subtitleText.color.b, 0f);
            float elapsed = 0f, dur = 0.6f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t  = elapsed / dur;
                subtitleText.color = new Color(subtitleText.color.r, subtitleText.color.g, subtitleText.color.b, t);
                yield return null;
            }
            subtitleText.color = new Color(subtitleText.color.r, subtitleText.color.g, subtitleText.color.b, 1f);
        }
    }

    // ── Button setup ─────────────────────────────────────────────────────────
    private void SetupMenuButtons()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            Transform root = canvas.transform;

            // Find the Play / New Game button
            Transform btnT = FindDeep(root, "PlayButton")
                          ?? FindDeep(root, "StartButton")
                          ?? FindDeep(root, "NewGameButton");
            if (btnT != null)
            {
                _playButton = btnT.GetComponent<Button>();
                if (_playButton != null)
                {
                    // Relabel to "New Game" so intent is clear
                    TextMeshProUGUI lbl = btnT.GetComponentInChildren<TextMeshProUGUI>();
                    if (lbl != null && (lbl.text == "Play" || lbl.text == "Start"))
                        lbl.text = "New Game";
                }
            }

            // Inject Load Game button only when a save exists
            bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSave;
            if (hasSave && _playButton != null && _loadGameButton == null)
            {
                _loadGameButton = CreateLoadGameButton(_playButton.GetComponent<RectTransform>());
            }
        }
    }

    private Button CreateLoadGameButton(RectTransform anchor)
    {
        if (anchor == null) return null;

        var go = new GameObject("LoadGameButton");
        go.transform.SetParent(anchor.transform.parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta        = anchor.sizeDelta;
        rt.anchorMin        = anchor.anchorMin;
        rt.anchorMax        = anchor.anchorMax;
        rt.pivot            = anchor.pivot;
        // Place it directly below the Play button
        rt.anchoredPosition = anchor.anchoredPosition + new Vector2(0f, -anchor.sizeDelta.y - 10f);

        // Background image
        Image img = go.AddComponent<Image>();
        Image srcImg = anchor.GetComponent<Image>();
        if (srcImg != null)
        {
            img.sprite = srcImg.sprite;
            img.type   = srcImg.type;
            img.color  = srcImg.color;
        }
        else
        {
            img.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        }

        // Button component
        Button btn = go.AddComponent<Button>();
        ColorBlock cb = anchor.GetComponent<Button>()?.colors ?? ColorBlock.defaultColorBlock;
        btn.targetGraphic = img;
        btn.colors        = cb;
        btn.onClick.AddListener(LoadGame);

        // Label
        var labelGo  = new GameObject("Text");
        labelGo.transform.SetParent(go.transform, false);
        var labelRT  = labelGo.AddComponent<RectTransform>();
        labelRT.anchorMin        = Vector2.zero;
        labelRT.anchorMax        = Vector2.one;
        labelRT.offsetMin        = Vector2.zero;
        labelRT.offsetMax        = Vector2.zero;

        // Copy font style from anchor's label
        TextMeshProUGUI srcTmp = anchor.GetComponentInChildren<TextMeshProUGUI>();
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        if (srcTmp != null)
        {
            tmp.font       = srcTmp.font;
            tmp.fontSize   = srcTmp.fontSize;
            tmp.color      = srcTmp.color;
            tmp.alignment  = srcTmp.alignment;
            tmp.fontStyle  = srcTmp.fontStyle;
        }
        else
        {
            tmp.fontSize  = 14f;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }
        tmp.text = "Load Game";
        FontEnforcer.ApplyTo(tmp);

        go.SetActive(true);
        return btn;
    }

    // ── Menu button handlers ──────────────────────────────────────────────────
    /// <summary>Clears any existing save and starts a fresh run from Village.</summary>
    public void NewGame()
    {
        SaveManager.Instance?.DeleteSave();
        StartCoroutine(StartGameSequence());
    }

    /// <summary>Resumes from the last saved scene.</summary>
    public void LoadGame()
    {
        if (SaveManager.Instance != null && SaveManager.Instance.HasSave)
            SaveManager.Instance.LoadGame();
    }

    // ── Start Game button handler ─────────────────────────────────────────────
    public void StartGame()
    {
        SaveManager.Instance?.DeleteSave();
        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        // Fade to black
        yield return StartCoroutine(FadeScreen(0f, 1f, 0.6f));

        // Show story pages one by one
        if (storyPanel != null) storyPanel.SetActive(true);
        if (storyText  != null) storyText.text = "";

        foreach (string page in StoryPages)
        {
            if (storyText != null)
            {
                // Fade in
                yield return StartCoroutine(FadeText(storyText, 0f, 1f, 0.3f));

                // Typewrite the page
                yield return StartCoroutine(TypewriterEffect(storyText, page, 0.032f));

                // Hold
                yield return new WaitForSecondsRealtime(2.2f);

                // Fade out before next page
                yield return StartCoroutine(FadeText(storyText, 1f, 0f, 0.4f));
                storyText.text = "";
            }
            else
            {
                yield return new WaitForSecondsRealtime(3f);
            }
        }

        // Brief pause then load Village
        yield return new WaitForSecondsRealtime(0.4f);
        SceneManager.LoadScene("Village");
    }

    // ── Utility coroutines ────────────────────────────────────────────────────
    IEnumerator FadeScreen(float from, float to, float duration)
    {
        if (screenFade == null) yield break;
        float elapsed = 0f;
        Color c = screenFade.color;
        screenFade.color = new Color(c.r, c.g, c.b, from);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            screenFade.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        screenFade.color = new Color(c.r, c.g, c.b, to);
    }

    IEnumerator FadeText(TextMeshProUGUI tmp, float from, float to, float duration)
    {
        if (tmp == null) yield break;
        float elapsed = 0f;
        Color c = tmp.color;
        tmp.color = new Color(c.r, c.g, c.b, from);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            tmp.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        tmp.color = new Color(c.r, c.g, c.b, to);
    }

    IEnumerator TypewriterEffect(TextMeshProUGUI target, string text, float charDelay)
    {
        target.text = "";
        foreach (char ch in text)
        {
            target.text += ch;
            yield return new WaitForSecondsRealtime(charDelay);
        }
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>
    /// Subtle continuous breathing effect on the title — never stops while the
    /// menu is up. Stops naturally when the scene unloads and StopAllCoroutines fires.
    /// </summary>
    IEnumerator TitlePulse()
    {
        if (titleText == null) yield break;
        Vector3 baseScale = titleText.transform.localScale;
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * 1.4f) + 1f) * 0.5f;   // 0..1
            float k = Mathf.Lerp(0.985f, 1.025f, t);
            titleText.transform.localScale = baseScale * k;
            yield return null;
        }
    }
}
