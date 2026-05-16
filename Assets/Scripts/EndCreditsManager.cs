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

    private Canvas          _canvas;
    private TextMeshProUGUI _line;
    private Image           _black;
    private TMP_FontAsset   _font;

    private static readonly string[] ReunionLines =
    {
        "Villager 1:\n\"Hello again adventurer — you defeated the evil Kraken!\"",
        "Villager 1:\n\"Hurray! You saved the world!\"",
        "The Whole Village:\n\"Hurray! Hurray! Our hero!!!\"",
        "Balance has been restored."
    };

    private static readonly string[] Credits =
    {
        "JOURNEY OF ADVENTURES",
        "",
        "Spelproduktion TE25i",
        "",
        "Programming & Project Lead",
        "Alfred",
        "",
        "Level Design & World Assets",
        "Alexander",
        "",
        "Story, Script & NPC Dialogue",
        "Axel",
        "",
        "Music & Sound Effects",
        "Jack",
        "",
        "Character Design & Sprites",
        "Albin",
        "",
        "Assets & Moodboard",
        "Ferhad",
        "",
        "",
        "Thank you for playing!",
        "",
        "THE END"
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

        // Centred story line
        GameObject lineGO = new GameObject("StoryLine");
        lineGO.transform.SetParent(_canvas.transform, false);
        var lRt = lineGO.AddComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0.1f, 0.35f);
        lRt.anchorMax = new Vector2(0.9f, 0.65f);
        lRt.offsetMin = Vector2.zero; lRt.offsetMax = Vector2.zero;
        _line = lineGO.AddComponent<TextMeshProUGUI>();
        _line.alignment = TextAlignmentOptions.Center;
        _line.fontSize  = 16f;
        _line.color     = new Color(1f, 1f, 1f, 0f);
        _line.textWrappingMode = TextWrappingModes.Normal;
        _line.outlineWidth = 0.25f;
        _line.outlineColor = new Color32(0, 0, 0, 255);
        if (_font != null) _line.font = _font;

        // Black fade overlay (starts transparent)
        GameObject blackGO = new GameObject("BlackOverlay");
        blackGO.transform.SetParent(_canvas.transform, false);
        var bRt = blackGO.AddComponent<RectTransform>();
        bRt.anchorMin = Vector2.zero; bRt.anchorMax = Vector2.one;
        bRt.offsetMin = Vector2.zero; bRt.offsetMax = Vector2.zero;
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

        // ── Village reunion ───────────────────────────────────────────────
        for (int i = 0; i < ReunionLines.Length; i++)
        {
            _line.text = ReunionLines[i];
            // The final reunion line is the big "Balance restored" beat
            _line.fontSize = i == ReunionLines.Length - 1 ? 22f : 15f;
            yield return Fade(_line, 0f, 1f, 0.7f);
            yield return new WaitForSeconds(i == ReunionLines.Length - 1 ? 2.6f : 2.1f);
            yield return Fade(_line, 1f, 0f, 0.6f);
            yield return new WaitForSeconds(0.3f);
        }

        // ── Fade to black ─────────────────────────────────────────────────
        yield return FadeImage(_black, 0f, 1f, 1.4f);

        // ── Scrolling credits ─────────────────────────────────────────────
        yield return ScrollCredits();

        // ── Back to the main menu (auto or on click) ──────────────────────
        float wait = 0f;
        while (wait < 8f && !Input.GetMouseButtonDown(0) && !Input.anyKeyDown)
        { wait += Time.deltaTime; yield return null; }

        SceneManager.LoadScene(mainMenuScene);
    }

    private IEnumerator ScrollCredits()
    {
        GameObject scrollGO = new GameObject("CreditsScroll");
        scrollGO.transform.SetParent(_canvas.transform, false);
        var sRt = scrollGO.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, 0f);
        sRt.anchorMax = new Vector2(0.5f, 0f);
        sRt.pivot     = new Vector2(0.5f, 0f);
        sRt.sizeDelta = new Vector2(900f, 1600f);
        sRt.anchoredPosition = new Vector2(0f, -1650f);

        var txt = scrollGO.AddComponent<TextMeshProUGUI>();
        txt.alignment = TextAlignmentOptions.Top;
        txt.fontSize  = 22f;
        txt.lineSpacing = 18f;
        txt.color     = Color.white;
        if (_font != null) txt.font = _font;
        txt.text = string.Join("\n", Credits);

        // Scroll up past the screen
        float t = 0f, duration = 16f;
        float startY = -1650f, endY = 1700f;
        while (t < duration)
        {
            t += Time.deltaTime;
            // Allow a click to speed the scroll
            if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space)) t += Time.deltaTime * 2f;
            sRt.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, t / duration));
            yield return null;
        }
    }

    // ── Fade helpers ──────────────────────────────────────────────────────────
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
