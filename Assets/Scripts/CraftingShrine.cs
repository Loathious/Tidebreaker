using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// The crafting shrine in the Dark Cave.
/// Locked until all 5 diamonds are collected. Then the player presses E to
/// craft the Diamond Sword and complete the level.
/// </summary>
public class CraftingShrine : MonoBehaviour
{
    [Header("Items")]
    [SerializeField] private ItemData diamondSwordItem;   // assign in inspector

    [Header("Next Scene")]
    [SerializeField] private string nextSceneName = "Jungle";

    [Header("Audio")]
    public AudioClip craftClip;
    public AudioClip unlockClip;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private bool       _unlocked;
    private bool       _crafted;
    private bool       _playerNearby;
    private TextMeshPro _hint;

    private static readonly Color ColorLocked   = new Color(0.25f, 0.25f, 0.35f);
    private static readonly Color ColorUnlocked = new Color(0.4f, 0.9f, 1f);

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>()
                          ?? GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        if (spriteRenderer != null) spriteRenderer.color = ColorLocked;
        BuildHint();
    }

    void Update()
    {
        if (!_unlocked || _crafted) return;

        if (_playerNearby && Input.GetKeyDown(KeyCode.E))
            Craft();
    }

    public void Unlock()
    {
        _unlocked = true;
        if (spriteRenderer != null) spriteRenderer.color = ColorUnlocked;
        if (_hint != null) _hint.text = "Press E to craft Diamond Sword";
        if (unlockClip != null) SettingsManager.PlaySfxAt(unlockClip, transform.position, 0.9f);

        // Boost the attached Light2D, if any, so the shrine visibly turns on
        var l = GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>();
        if (l != null)
        {
            l.intensity = 1.5f;
            l.color = new Color(0.5f, 0.95f, 1f);
            l.pointLightOuterRadius = Mathf.Max(l.pointLightOuterRadius, 6f);
        }

        StartCoroutine(UnlockPulse());
    }

    private IEnumerator UnlockPulse()
    {
        if (spriteRenderer == null) yield break;
        for (int i = 0; i < 3; i++)
        {
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = ColorUnlocked;
            yield return new WaitForSeconds(0.1f);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _playerNearby = true;
            if (_hint != null && _unlocked && !_crafted)
                _hint.gameObject.SetActive(true);
            else if (_hint != null && !_unlocked)
            {
                _hint.text = $"Need {CaveManager.Instance?.DiamondsRequired ?? 5} diamonds";
                _hint.gameObject.SetActive(true);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _playerNearby = false;
            if (_hint != null) _hint.gameObject.SetActive(false);
        }
    }

    private void Craft()
    {
        _crafted = true;
        if (_hint != null) _hint.gameObject.SetActive(false);
        if (craftClip != null) SettingsManager.PlaySfxAt(craftClip, transform.position, 1.0f);

        if (diamondSwordItem != null)
            Inventory.Instance?.AddItem(diamondSwordItem);

        // Remove diamonds from inventory (optional - just a nice touch)
        // Inventory.Instance?.RemoveItem(diamondItem, 5);

        StartCoroutine(CraftSequence());
    }

    private IEnumerator CraftSequence()
    {
        // Brief glow effect
        if (spriteRenderer != null)
        {
            for (int i = 0; i < 6; i++)
            {
                spriteRenderer.color = Color.white;
                yield return new WaitForSecondsRealtime(0.08f);
                spriteRenderer.color = ColorUnlocked;
                yield return new WaitForSecondsRealtime(0.08f);
            }
        }

        ObjectiveManager.Instance?.UpdateObjective("Exit the Cave");

        // Show "Diamond Sword Crafted!" overlay then transition
        yield return ShowCraftedOverlay();

        // Transition to next scene
        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
        else
            StartCoroutine(ShowLevelCompleteScreen());
    }

    private IEnumerator ShowCraftedOverlay()
    {
        Canvas canvas = FindCanvas();
        if (canvas == null) yield break;

        GameObject root = new GameObject("CraftedOverlay");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // Background dim
        GameObject dim = new GameObject("Dim");
        dim.transform.SetParent(root.transform, false);
        RectTransform dRt = dim.AddComponent<RectTransform>();
        dRt.anchorMin = Vector2.zero; dRt.anchorMax = Vector2.one;
        dRt.offsetMin = Vector2.zero; dRt.offsetMax = Vector2.zero;
        Image dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0);

        // "DIAMOND SWORD CRAFTED!" text
        GameObject txt = new GameObject("CraftText");
        txt.transform.SetParent(root.transform, false);
        RectTransform tRt = txt.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.1f, 0.42f);
        tRt.anchorMax = new Vector2(0.9f, 0.62f);
        tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
        TextMeshProUGUI header = txt.AddComponent<TextMeshProUGUI>();
        header.text      = "DIAMOND SWORD CRAFTED!";
        header.alignment = TextAlignmentOptions.Center;
        header.fontSize  = 12f;
        header.color     = new Color(0.4f, 0.9f, 1f, 0f);
        header.outlineWidth = 0.22f;
        header.outlineColor = new Color32(0, 0, 0, 255);
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/PressStart2P-Regular SDF")
                          ?? Resources.Load<TMP_FontAsset>("PressStart2P-Regular SDF");
        if (font != null) header.font = font;

        // Fade in
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.8f;
            float k = Mathf.SmoothStep(0f, 1f, t);
            dimImg.color = new Color(0, 0, 0, k * 0.7f);
            header.color = new Color(0.4f, 0.9f, 1f, k);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1.8f);

        // Fade out
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.6f;
            float k = 1f - Mathf.SmoothStep(0f, 1f, t);
            dimImg.color = new Color(0, 0, 0, k * 0.7f);
            header.color = new Color(0.4f, 0.9f, 1f, k);
            yield return null;
        }

        Destroy(root);
    }

    private IEnumerator ShowLevelCompleteScreen()
    {
        ObjectiveManager.Instance?.HideObjective();

        Canvas canvas = FindCanvas();
        if (canvas == null) yield break;

        GameObject root = new GameObject("LevelCompleteOverlay");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        GameObject dim = new GameObject("Dim");
        dim.transform.SetParent(root.transform, false);
        RectTransform dRt = dim.AddComponent<RectTransform>();
        dRt.anchorMin = Vector2.zero; dRt.anchorMax = Vector2.one;
        dRt.offsetMin = Vector2.zero; dRt.offsetMax = Vector2.zero;
        Image dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0);
        dimImg.raycastTarget = true;

        GameObject hdrGO = new GameObject("HeaderText");
        hdrGO.transform.SetParent(root.transform, false);
        RectTransform hRt = hdrGO.AddComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0.1f, 0.55f); hRt.anchorMax = new Vector2(0.9f, 0.75f);
        hRt.offsetMin = Vector2.zero; hRt.offsetMax = Vector2.zero;
        TextMeshProUGUI header = hdrGO.AddComponent<TextMeshProUGUI>();
        header.text      = "LEVEL 2 COMPLETE";
        header.alignment = TextAlignmentOptions.Center;
        header.fontSize  = 32f;
        header.color     = new Color(0.4f, 0.9f, 1f, 0f);
        FontEnforcer.ApplyTo(header);    // PressStart2P
        // No Bold â€” pixel fonts don't have a real bold variant; synthesized bold
        // produces garbled glyph overlap.

        GameObject subGO = new GameObject("SubText");
        subGO.transform.SetParent(root.transform, false);
        RectTransform sRt = subGO.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.1f, 0.4f); sRt.anchorMax = new Vector2(0.9f, 0.55f);
        sRt.offsetMin = Vector2.zero; sRt.offsetMax = Vector2.zero;
        TextMeshProUGUI sub = subGO.AddComponent<TextMeshProUGUI>();
        sub.text      = "The jungle temple awaits";
        sub.alignment = TextAlignmentOptions.Center;
        sub.fontSize  = 16f;
        sub.color     = new Color(1f, 1f, 1f, 0f);
        FontEnforcer.ApplyTo(sub);

        float t = 0f;
        while (t < 1.2f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 1.2f);
            dimImg.color = new Color(0, 0, 0, k * 0.75f);
            header.color = new Color(0.4f, 0.9f, 1f, k);
            sub.color    = new Color(1f, 1f, 1f, k);
            yield return null;
        }
    }

    private static Canvas FindCanvas()
    {
        Canvas found = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode != RenderMode.ScreenSpaceOverlay &&
                c.renderMode != RenderMode.ScreenSpaceCamera) continue;
            if (c.name == "GameCanvas") return c;
            if (found == null) found = c;
        }
        return found;
    }

    private void BuildHint()
    {
        GameObject hintGO = new GameObject("ShrineHint");
        hintGO.transform.SetParent(transform);
        hintGO.transform.localPosition = Vector3.up * 1.2f;
        hintGO.SetActive(false);

        _hint = hintGO.AddComponent<TextMeshPro>();
        _hint.text      = "Need 5 diamonds";
        _hint.fontSize  = 2f;
        _hint.color     = new Color(0.8f, 1f, 1f);
        _hint.alignment = TextAlignmentOptions.Center;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/PressStart2P-Regular SDF")
                          ?? Resources.Load<TMP_FontAsset>("PressStart2P-Regular SDF");
        if (font != null) _hint.font = font;
    }
}
