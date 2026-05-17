using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// End Credits scene. Plays the village-reunion finale from the spelmanus, then
/// scrolls the team credits. Builds its whole UI procedurally so the scene only
/// needs a background and this component.
/// </summary>
public class EndCreditsManager : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip creditsMusic;

    [Header("Flow")]
    public string mainMenuScene = "MainMenu";

    private Canvas _canvas;
    private TextMeshProUGUI _line;
    private Image _black;
    private TMP_FontAsset _font;

    private TextMeshProUGUI _titleText;

    private static readonly string[] ReunionLines =
    {
        "Villager 1:\n\"Hello again adventurer — you defeated the evil Kraken!\"",
        "Villager 1:\n\"Hurray! You saved the world!\"",
        "The Whole Village:\n\"Hurray! Hurray! Our hero!!!\"",
        "Balance has been restored."
    };

    void Start()
    {
        Time.timeScale = 1f;
        _font = FontEnforcer.Font;
        SetupCanvas();
        SetupMusic();
        StartCoroutine(RunSequence());
    }

    private void SetupCanvas()
    {
        _canvas = FindFirstObjectByType<Canvas>();
        if (_canvas == null)
        {
            GameObject cgo = new GameObject("CreditsCanvas");
            _canvas = cgo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            cgo.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            cgo.AddComponent<GraphicRaycaster>();
        }

        GameObject lineGO = new GameObject("StoryLine");
        lineGO.transform.SetParent(_canvas.transform, false);

        var lRt = lineGO.AddComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0.1f, 0.35f);
        lRt.anchorMax = new Vector2(0.9f, 0.65f);
        lRt.offsetMin = Vector2.zero;
        lRt.offsetMax = Vector2.zero;

        _line = lineGO.AddComponent<TextMeshProUGUI>();
        _line.alignment = TextAlignmentOptions.Center;
        _line.fontSize = 10f;
        _line.color = new Color(1f, 1f, 1f, 0f);

        _line.outlineWidth = 0.25f;
        _line.outlineColor = new Color32(0, 0, 0, 255);

        if (_font != null)
            _line.font = _font;

        GameObject blackGO = new GameObject("BlackOverlay");
        blackGO.transform.SetParent(_canvas.transform, false);

        var bRt = blackGO.AddComponent<RectTransform>();
        bRt.anchorMin = Vector2.zero;
        bRt.anchorMax = Vector2.one;
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        _black = blackGO.AddComponent<Image>();
        _black.color = new Color(0, 0, 0, 0);
        _black.raycastTarget = false;
    }

    private void SetupMusic()
    {
        if (creditsMusic == null) return;

        AudioSource src = gameObject.GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();

        src.clip = creditsMusic;
        src.loop = true;
        src.volume = 0.5f;
        src.spatialBlend = 0f;
        src.Play();
    }

    private IEnumerator RunSequence()
    {
        yield return new WaitForSeconds(1f);

        for (int i = 0; i < ReunionLines.Length; i++)
        {
            _line.text = ReunionLines[i];
            _line.fontSize = (i == ReunionLines.Length - 1) ? 22f : 15f;

            yield return Fade(_line, 0f, 1f, 0.7f);
            yield return new WaitForSeconds(i == ReunionLines.Length - 1 ? 2.6f : 2.1f);
            yield return Fade(_line, 1f, 0f, 0.6f);
            yield return new WaitForSeconds(0.3f);
        }

        yield return FadeImage(_black, 0f, 1f, 1.4f);

        yield return ScrollCredits();

        float wait = 0f;
        while (wait < 8f && !Input.GetMouseButtonDown(0) && !Input.anyKeyDown)
        {
            wait += Time.deltaTime;
            yield return null;
        }

        SceneManager.LoadScene(mainMenuScene);
    }

    private IEnumerator ScrollCredits()
    {
        GameObject scrollGO = new GameObject("CreditsScroll");
        scrollGO.transform.SetParent(_canvas.transform, false);

        var sRt = scrollGO.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, 0f);
        sRt.anchorMax = new Vector2(0.5f, 0f);
        sRt.pivot = new Vector2(0.5f, 0f);
        sRt.sizeDelta = new Vector2(900f, 1600f);
        sRt.anchoredPosition = new Vector2(0f, -1650f);

        var creditsText = scrollGO.AddComponent<TextMeshProUGUI>();
        creditsText.alignment = TextAlignmentOptions.Top;
        creditsText.fontSize = 22f;
        creditsText.lineSpacing = 18f;
        creditsText.color = Color.white;

        if (_font != null)
            creditsText.font = _font;

        // Create title separately (ONLY this animates)
        CreateTitle(creditsText.transform);

        creditsText.text = BuildCredits();

        float t = 0f;
        float duration = 95f;

        float startY = -1650f;
        float endY = 1700f;

        while (t < duration)
        {
            t += Time.deltaTime;

            if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space))
                t += Time.deltaTime * 2f;

            sRt.anchoredPosition =
                new Vector2(0f, Mathf.Lerp(startY, endY, t / duration));

            yield return null;
        }
    }

    private void CreateTitle(Transform parent)
    {
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(parent, false);

        RectTransform rt = titleGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, 120f);

        _titleText = titleGO.AddComponent<TextMeshProUGUI>();
        _titleText.text = "JOURNEY OF ADVENTURES";
        _titleText.alignment = TextAlignmentOptions.Center;
        _titleText.fontSize = 34f;

        if (_font != null)
            _titleText.font = _font;

        StartCoroutine(AnimateTitleColors());
    }

    private IEnumerator AnimateTitleColors()
    {
        // smooth cycle: gold → aqua → purple → gold
        Color gold = new Color(1f, 0.84f, 0f);
        Color aqua = new Color(0f, 1f, 1f);
        Color purple = new Color(0.7f, 0.4f, 1f);

        float t = 0f;

        while (_titleText != null)
        {
            t += Time.deltaTime * 0.25f; // SLOW

            float cycle = Mathf.PingPong(t, 1f);

            Color c;
            if (cycle < 0.33f)
                c = Color.Lerp(gold, aqua, cycle / 0.33f);
            else if (cycle < 0.66f)
                c = Color.Lerp(aqua, purple, (cycle - 0.33f) / 0.33f);
            else
                c = Color.Lerp(purple, gold, (cycle - 0.66f) / 0.34f);

            _titleText.color = c;

            yield return null;
        }
    }

    private string BuildCredits()
    {
        return
            "\n\n<size=26><b>Spelproduktion TE25i</b></size>\n\n" +

            "<size=24><b>Programming, Assets & Project Lead</b></size>\n" +
            "Alfred\n\n" +

            "<size=24><b>Level Design & World Assets</b></size>\n" +
            "Alexander\n\n" +

            "<size=24><b>Story, Script & NPC Dialogue</b></size>\n" +
            "Axel\n\n" +

            "<size=24><b>Music & Sound Effects</b></size>\n" +
            "Jack\n\n" +

            "<size=24><b>Character Design & Sprites</b></size>\n" +
            "Albin\n\n" +

            "<size=24><b>Moodboard</b></size>\n" +
            "Ferhad\n\n\n" +

            "<size=28><b>Thank you for playing!</b></size>\n";
    }

    private IEnumerator Fade(TMP_Text tmp, float from, float to, float dur)
    {
        float t = 0f;
        Color c = tmp.color;

        while (t < dur)
        {
            t += Time.deltaTime;
            tmp.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, t / dur));
            yield return null;
        }

        tmp.color = new Color(c.r, c.g, c.b, to);
    }

    private IEnumerator FadeImage(Image img, float from, float to, float dur)
    {
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            img.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / dur));
            yield return null;
        }

        img.color = new Color(0, 0, 0, to);
    }
}