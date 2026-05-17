using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A versatile story trigger used for level transitions:
///  • Level 3 temple inscription → Desert
///  • Level 4 pyramid reward (grants Bow + Magical Armor) → Ocean
///  • plain "walk to the edge to continue" exits.
///
/// Shows its lines through the existing DialogUI, can grant the end-game gear,
/// fades to black and loads the next scene. Gated by <see cref="unlocked"/>.
/// </summary>
public class StoryPortal : MonoBehaviour
{
    [Header("Gate")]
    [Tooltip("If false the portal is sealed until UnlockPortal() is called.")]
    public bool unlocked = true;
    public bool requireKeyPress = true;

    [Header("Dialog")]
    public string speakerName = "Inscription";
    [TextArea(2, 4)] public string[] lines;

    [Header("Rewards")]
    public bool grantBowAndArmor = false;

    [Header("Transition")]
    public string nextScene = "";
    public string objectiveOnUnlock = "";

    [Header("Audio")]
    public AudioClip mysticClip;

    private bool _playerNearby;
    private bool _used;
    private TextMeshPro _prompt;

    void Awake()
    {
        BuildPrompt();
        // Ensure there is a trigger collider
        if (GetComponent<Collider2D>() == null)
        {
            var c = gameObject.AddComponent<BoxCollider2D>();
            c.isTrigger = true;
            c.size = new Vector2(3f, 5f);
        }
        else
        {
            foreach (var c in GetComponents<Collider2D>()) c.isTrigger = true;
        }
    }

    void Update()
    {
        if (_used || !unlocked) return;
        if (requireKeyPress && _playerNearby && Input.GetKeyDown(KeyCode.E))
            Activate();
    }

    /// <summary>Opens a previously sealed portal.</summary>
    public void UnlockPortal()
    {
        unlocked = true;
        if (!string.IsNullOrEmpty(objectiveOnUnlock))
            ObjectiveManager.Instance?.UpdateObjective(objectiveOnUnlock);
        if (_prompt != null && _playerNearby) ShowPrompt(true);
    }

    private void Activate()
    {
        if (_used) return;
        _used = true;
        if (_prompt != null) _prompt.gameObject.SetActive(false);

        if (mysticClip != null)
            AudioSource.PlayClipAtPoint(mysticClip, Camera.main.transform.position, 0.9f);

        if (grantBowAndArmor)
        {
            PlayerRanged.Grant();
            MagicalArmor.Grant();
            EnsurePlayerGear();
            ShowPickupBanner();
        }

        DialogUI dialog = FindFirstObjectByType<DialogUI>(FindObjectsInactive.Include);
        if (dialog != null && lines != null && lines.Length > 0)
            dialog.ShowDialog(speakerName, lines, OnDialogDone, null, transform);
        else
            OnDialogDone();
    }

    private void OnDialogDone()
    {
        SaveManager.Instance?.SaveGame();
        StartCoroutine(FadeAndLoad());
    }

    /// <summary>Adds the ranged + armor components to the player if missing.</summary>
    public static void EnsurePlayerGear()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;
        if (p.GetComponent<PlayerRanged>() == null) p.AddComponent<PlayerRanged>();
        if (p.GetComponent<MagicalArmor>() == null) p.AddComponent<MagicalArmor>();
    }

    private IEnumerator FadeAndLoad()
    {
        if (string.IsNullOrEmpty(nextScene)) yield break;

        Canvas canvas = FindOverlayCanvas();
        Image fade = null;
        if (canvas != null)
        {
            GameObject go = new GameObject("SceneFade");
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsLastSibling();
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            fade = go.AddComponent<Image>();
            fade.color = new Color(0, 0, 0, 0);
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 1.1f;
            if (fade != null) fade.color = new Color(0, 0, 0, Mathf.Clamp01(t));
            yield return null;
        }

        if (Application.CanStreamedLevelBeLoaded(nextScene))
            SceneManager.LoadScene(nextScene);
        else
            Debug.LogWarning($"[StoryPortal] Next scene '{nextScene}' is not in Build Settings.");
    }

    // ── Prompt ────────────────────────────────────────────────────────────────
    private void BuildPrompt()
    {
        GameObject go = new GameObject("PortalPrompt");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 2.2f;
        go.SetActive(false);

        _prompt = go.AddComponent<TextMeshPro>();
        _prompt.text      = "Press E";
        _prompt.fontSize  = 2.4f;
        _prompt.color     = new Color(1f, 0.95f, 0.7f);
        _prompt.alignment = TextAlignmentOptions.Center;
        _prompt.rectTransform.sizeDelta = new Vector2(14f, 3f);

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/PressStart2P-Regular SDF")
                          ?? Resources.Load<TMP_FontAsset>("PressStart2P-Regular SDF");
        if (font != null) _prompt.font = font;
    }

    private void ShowPrompt(bool show)
    {
        if (_prompt == null) return;
        _prompt.gameObject.SetActive(show);
        _prompt.text = unlocked ? "Press E to continue" : "Sealed";
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_used || !other.CompareTag("Player")) return;
        _playerNearby = true;

        if (!requireKeyPress && unlocked)
        {
            Activate();
            return;
        }
        ShowPrompt(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerNearby = false;
        if (_prompt != null) _prompt.gameObject.SetActive(false);
    }

    // ── Pickup notification ───────────────────────────────────────────────────
    private void ShowPickupBanner()
    {
        StartCoroutine(PickupBannerRoutine());
    }

    private IEnumerator PickupBannerRoutine()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) yield break;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/PressStart2P-Regular SDF")
                          ?? Resources.Load<TMP_FontAsset>("PressStart2P-Regular SDF");

        GameObject go = new GameObject("PickupBanner");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.62f);
        rt.anchorMax = new Vector2(0.9f, 0.78f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text         = "You received:\nMagical Armor  &  Bow!";
        tmp.alignment    = TextAlignmentOptions.Center;
        tmp.fontSize     = 14f;
        tmp.color        = new Color(1f, 0.9f, 0.3f, 0f);
        tmp.outlineWidth = 0.28f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        if (font != null) tmp.font = font;

        // Fade in
        float t = 0f;
        while (t < 0.5f) { t += Time.unscaledDeltaTime; tmp.color = new Color(1f, 0.9f, 0.3f, t / 0.5f); yield return null; }
        yield return new WaitForSecondsRealtime(2.5f);
        // Fade out
        t = 0f;
        while (t < 0.5f) { t += Time.unscaledDeltaTime; tmp.color = new Color(1f, 0.9f, 0.3f, 1f - t / 0.5f); yield return null; }
        Destroy(go);
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
