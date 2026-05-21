using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays dialog with a typewriter effect.
/// Supports single messages (auto-advance after typewriter) or
/// multi-line arrays where the player LEFT-CLICKS to advance.
/// Keyboard input (E / Space / Return) is intentionally disabled.
/// </summary>
public class DialogUI : MonoBehaviour
{
    [SerializeField] private HotbarUI hotbarUI;
    [SerializeField] private GameObject dialogRoot;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogBodyText;
    [SerializeField] private TextMeshProUGUI continuePromptText;
    [SerializeField] private float typewriterSpeed = 0.03f;

    [Header("Audio")]
    public AudioClip[] talkClips;

    // ── State ─────────────────────────────────────────────────────────────────
    private string[]  _lines;
    private int       _lineIndex;
    private Action    _onComplete;
    private bool      _isTyping;
    private bool      _waitingForInput;
    private Coroutine _typewriterCoroutine;
    private AudioSource _talkAudio;
    private int         _talkCharCount;
    private const int   TalkEveryNChars = 2;

    /// <summary>The speaker's Transform — used by CameraFollow to lock onto during dialogue.</summary>
    public Transform CurrentSpeakerTransform { get; private set; }

    void Awake()
    {
        _talkAudio = gameObject.AddComponent<AudioSource>();
        _talkAudio.playOnAwake  = false;
        _talkAudio.spatialBlend = 0f;

        if (hotbarUI == null)      hotbarUI      = FindFirstObjectByType<HotbarUI>();
        if (dialogRoot == null)    dialogRoot    = FindInCanvas("DialogPanel");
        if (portraitImage == null) portraitImage = FindInPanel<Image>("PortraitBox")
                                                ?? FindInPanel<Image>("PortraitImage");
        if (speakerNameText == null)    speakerNameText    = FindInPanel<TextMeshProUGUI>("SpeakerName");
        if (dialogBodyText == null)     dialogBodyText     = FindInPanel<TextMeshProUGUI>("DialogText")
                                                          ?? FindInPanel<TextMeshProUGUI>("DialogBody");
        if (continuePromptText == null) continuePromptText = FindInPanel<TextMeshProUGUI>("ContinuePrompt");

        if (dialogRoot != null) dialogRoot.SetActive(false);
    }

    // ── Scene helpers ─────────────────────────────────────────────────────────
    private GameObject FindInCanvas(string goName)
        => FindDeep(transform, goName)?.gameObject;

    private T FindInPanel<T>(string goName) where T : Component
    {
        var t = FindDeep(transform, goName);
        return t != null ? t.GetComponent<T>() : null;
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

    // ── Update — click-only advancement ──────────────────────────────────────
    void Update()
    {
        if (!IsOpen) return;

        // ONLY left mouse click advances dialogue — no keyboard
        bool advance = Input.GetMouseButtonDown(0);
        if (!advance) return;

        if (_isTyping)
            SkipTypewriter();
        else if (_waitingForInput)
            AdvanceLine();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Single-message dialog — auto-closes after the typewriter finishes.</summary>
    public void ShowDialog(string speakerName, string message, Sprite portrait, Action onComplete,
                           Transform speakerTransform = null)
    {
        ShowDialog(speakerName, new[] { message }, onComplete, portrait, speakerTransform);
    }

    /// <summary>Multi-line dialog — player clicks to advance through lines.</summary>
    public void ShowDialog(string speakerName, string[] lines, Action onComplete,
                           Sprite portrait = null, Transform speakerTransform = null)
    {
        if (lines == null || lines.Length == 0) { onComplete?.Invoke(); return; }

        _lines      = lines;
        _lineIndex  = 0;
        _onComplete = onComplete;

        CurrentSpeakerTransform = speakerTransform;

        // Lock camera onto speaker if CameraFollow is present
        CameraFollow cam = Camera.main?.GetComponent<CameraFollow>();
        cam?.LockToSpeaker(speakerTransform);

        // Lock player input during dialogue
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        pc?.LockInput();

        FixTextOverflow();
        if (hotbarUI != null) hotbarUI.SetVisible(false);
        if (dialogRoot != null) dialogRoot.SetActive(true);
        EnsureBlurBackdrop();   // ← darkened fake-blur dimmer behind dialogue
        if (speakerNameText != null) speakerNameText.text = speakerName;

        if (portraitImage != null)
        {
            portraitImage.gameObject.SetActive(portrait != null);
            if (portrait != null)
            {
                portraitImage.sprite           = portrait;
                portraitImage.color            = Color.white;
                portraitImage.type             = Image.Type.Simple;
                portraitImage.preserveAspect   = true;
            }
        }

        if (continuePromptText != null)
            continuePromptText.text = lines.Length > 1 ? "[ Click to continue ]" : "";

        StartLine(_lines[0]);
    }

    public void Hide()
    {
        StopAllCoroutines();
        if (dialogRoot != null) dialogRoot.SetActive(false);
        if (hotbarUI != null) hotbarUI.SetVisible(true);
        if (_blurBackdrop != null) _blurBackdrop.gameObject.SetActive(false);
        _isTyping        = false;
        _waitingForInput = false;
        CurrentSpeakerTransform = null;
    }

    public bool IsOpen => dialogRoot != null && dialogRoot.activeSelf;

    // ── Internals ─────────────────────────────────────────────────────────────
    void StartLine(string text)
    {
        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        _typewriterCoroutine = StartCoroutine(TypewriterEffect(text));
    }

    IEnumerator TypewriterEffect(string message)
    {
        _isTyping        = true;
        _waitingForInput = false;
        _talkCharCount   = 0;
        if (dialogBodyText != null) dialogBodyText.text = "";

        foreach (char c in message)
        {
            if (dialogBodyText != null) dialogBodyText.text += c;

            if (c != ' ' && talkClips != null && talkClips.Length > 0)
            {
                _talkCharCount++;
                if (_talkCharCount >= TalkEveryNChars)
                {
                    _talkCharCount = 0;
                    AudioClip clip = talkClips[UnityEngine.Random.Range(0, talkClips.Length)];
                    if (clip != null && _talkAudio != null)
                    {
                        _talkAudio.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
                        _talkAudio.PlayOneShot(clip, 0.45f * SettingsManager.SfxVol);
                    }
                }
            }

            yield return new WaitForSecondsRealtime(typewriterSpeed);
        }

        _isTyping = false;

        if (_lineIndex >= _lines.Length - 1)
        {
            // Last line — click to close, or auto-close after delay if it's a single-line call
            if (_lines.Length == 1)
            {
                yield return new WaitForSecondsRealtime(1.8f);
                CloseDialog();
            }
            else
            {
                // Wait for the player to click
                if (continuePromptText != null)
                    continuePromptText.text = "[ Click to close ]";
                _waitingForInput = true;
            }
        }
        else
        {
            _waitingForInput = true;
        }
    }

    void SkipTypewriter()
    {
        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        if (dialogBodyText != null && _lines != null && _lineIndex < _lines.Length)
            dialogBodyText.text = _lines[_lineIndex];
        _isTyping = false;

        if (_lineIndex >= _lines.Length - 1)
        {
            if (_lines.Length == 1)
                StartCoroutine(AutoCloseDelay());
            else
            {
                if (continuePromptText != null) continuePromptText.text = "[ Click to close ]";
                _waitingForInput = true;
            }
        }
        else
        {
            _waitingForInput = true;
        }
    }

    IEnumerator AutoCloseDelay()
    {
        yield return new WaitForSecondsRealtime(1.8f);
        CloseDialog();
    }

    void AdvanceLine()
    {
        _waitingForInput = false;
        _lineIndex++;
        if (_lineIndex < _lines.Length)
        {
            if (continuePromptText != null)
                continuePromptText.text = _lineIndex == _lines.Length - 1
                    ? "[ Click to close ]"
                    : "[ Click to continue ]";
            StartLine(_lines[_lineIndex]);
        }
        else
        {
            CloseDialog();
        }
    }

    void CloseDialog()
    {
        StopAllCoroutines();
        if (dialogRoot != null) dialogRoot.SetActive(false);
        if (hotbarUI != null) hotbarUI.SetVisible(true);
        if (_blurBackdrop != null) _blurBackdrop.gameObject.SetActive(false);
        _isTyping        = false;
        _waitingForInput = false;
        CurrentSpeakerTransform = null;

        // Release camera lock
        CameraFollow cam = Camera.main?.GetComponent<CameraFollow>();
        cam?.ReleaseSpeakerLock();

        // Unlock player movement
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        pc?.UnlockInput();

        _onComplete?.Invoke();
        _onComplete = null;
    }

    void FixTextOverflow()
    {
        if (dialogBodyText != null)
        {
            dialogBodyText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            dialogBodyText.overflowMode     = TextOverflowModes.Truncate;
        }
    }

    /// <summary>
    /// Adds a screen-darkening backdrop behind the dialog (a stand-in for true
    /// background blur — gives the same focus/separation feel without needing a
    /// custom URP renderer feature). Idempotent.
    /// </summary>
    private Image _blurBackdrop;
    void EnsureBlurBackdrop()
    {
        if (_blurBackdrop != null) { _blurBackdrop.gameObject.SetActive(true); return; }

        // Find the canvas the dialog lives on
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }
        if (canvas == null) return;

        GameObject go = new GameObject("DialogBlurBackdrop");
        go.transform.SetParent(canvas.transform, false);
        // Place behind the dialog panel (lowest sibling index so it draws first)
        go.transform.SetAsFirstSibling();

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _blurBackdrop = go.AddComponent<Image>();
        _blurBackdrop.color = new Color(0f, 0f, 0f, 0.45f);
        _blurBackdrop.raycastTarget = false;

        // Push the actual dialog panel above the backdrop
        if (dialogRoot != null) dialogRoot.transform.SetAsLastSibling();
    }

    void OnDisable()
    {
        // Clean up the backdrop when this dialog is disabled
        if (_blurBackdrop != null) _blurBackdrop.gameObject.SetActive(false);
    }
}
