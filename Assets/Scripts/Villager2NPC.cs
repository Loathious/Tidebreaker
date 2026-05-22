using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Villager 2 — appears after all zombies are defeated.
/// Dialogue from spelmanus dokument. Camera locks onto this NPC during dialogue.
/// After the dialog ends, transitions to the next level (Cave) if it exists,
/// otherwise shows a "Level 1 Complete" end-of-level screen.
/// </summary>
public class Villager2NPC : MonoBehaviour
{
    [SerializeField] private ItemData mapItem;
    [SerializeField] private DialogUI dialogUI;
    [SerializeField] private Sprite   villagerPortrait;
    [Tooltip("Distance from the player at which Villager2 should appear when victory triggers.")]
    [SerializeField] private float    appearOffsetFromPlayer = 3f;

    private bool _hasAppeared;

    // From spelmanus dokument
    private static readonly string[] DialogLines = new[]
    {
        "Thanks for defeating the monsters and saving us! Here is a map that will lead you to the temple.",
        "The temple contains information about the monsters. But you will need a new sword — the old one broke.",
        "Go to the dark cave located behind the village. There are diamonds down there. Use them to craft a new sword.",
        "Here, take these sticks with you. Come back when you have defeated the source of the monsters!"
    };

    void Awake()
    {
        if (dialogUI == null) dialogUI = FindFirstObjectByType<DialogUI>(FindObjectsInactive.Include);
    }

    void Start()
    {
        // Only hide if Appear() hasn't been called yet — prevents re-deactivating
        // after SetActive(true) when the object starts inactive in the scene.
        if (!_hasAppeared)
            gameObject.SetActive(false);
    }

    /// <summary>Makes the villager visible and immediately starts the dialog sequence.</summary>
    public void Appear()
    {
        _hasAppeared = true;
        // Teleport next to the player so they don't have to walk across the level
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float sideOffset = appearOffsetFromPlayer;
            // Place to the right of the player at the same Y
            transform.position = new Vector3(player.transform.position.x + sideOffset,
                                             player.transform.position.y,
                                             player.transform.position.z);
        }

        gameObject.SetActive(true);

        // Brief pause before speaking so the player has a moment to see them appear
        StartCoroutine(AppearAndSpeak());
    }

    private IEnumerator AppearAndSpeak()
    {
        yield return new WaitForSeconds(0.8f);

        // Update objective while speaking
        ObjectiveManager.Instance?.UpdateObjective("Enter the Dark Cave");

        if (dialogUI == null) dialogUI = FindFirstObjectByType<DialogUI>(FindObjectsInactive.Include);

        if (dialogUI != null)
        {
            dialogUI.ShowDialog(
                "Villager",
                DialogLines,
                OnDialogComplete,
                villagerPortrait,
                transform
            );
        }
        else
        {
            // Failsafe — go straight to "level complete" if no dialog UI is available
            OnDialogComplete();
        }
    }

    private void OnDialogComplete()
    {
        // Auto-save progress before leaving Level 1
        SaveManager.Instance?.SaveGame();

        // Map goes to inventory — but don't let it block the weapon slot
        // (Inventory is 1-slot; weapon takes priority in Cave)
        // We store the mapItem flag via PlayerPrefs so Cave knows the player has it
        if (mapItem != null)
            PlayerPrefs.SetInt("PlayerHasMap", 1);

        // Load Cave scene (build index 2). Fall back to name-based load if needed.
        int caveIndex = UnityEngine.SceneManagement.SceneUtility
            .GetBuildIndexByScenePath("Assets/Scenes/Cave.unity");

        if (caveIndex >= 0)
            SceneManager.LoadScene(caveIndex);
        else
            StartCoroutine(ShowLevelCompleteScreen());
    }

    // ── Fallback: in-game "Level 1 Complete" overlay ──────────────────────────
    private IEnumerator ShowLevelCompleteScreen()
    {
        ObjectiveManager.Instance?.HideObjective();

        // Find a screen-space canvas to host the overlay
        Canvas canvas = null;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode != RenderMode.ScreenSpaceOverlay &&
                c.renderMode != RenderMode.ScreenSpaceCamera) continue;
            if (c.name == "GameCanvas") { canvas = c; break; }
            if (canvas == null) canvas = c;
        }
        if (canvas == null) yield break;

        GameObject root = new GameObject("LevelCompleteOverlay");
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();
        RectTransform rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Full-screen dimmer
        GameObject dim = new GameObject("Dim");
        dim.transform.SetParent(root.transform, false);
        RectTransform dRt = dim.AddComponent<RectTransform>();
        dRt.anchorMin = Vector2.zero;
        dRt.anchorMax = Vector2.one;
        dRt.offsetMin = Vector2.zero;
        dRt.offsetMax = Vector2.zero;
        Image dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0f);
        dimImg.raycastTarget = true;        // blocks gameplay clicks

        // "LEVEL 1 COMPLETE" header
        GameObject hdr = new GameObject("HeaderText");
        hdr.transform.SetParent(root.transform, false);
        RectTransform hRt = hdr.AddComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0.1f, 0.55f);
        hRt.anchorMax = new Vector2(0.9f, 0.75f);
        hRt.offsetMin = Vector2.zero;
        hRt.offsetMax = Vector2.zero;
        TextMeshProUGUI header = hdr.AddComponent<TextMeshProUGUI>();
        header.text = "LEVEL 1 COMPLETE";
        header.alignment = TextAlignmentOptions.Center;
        header.fontSize = 14f;
        header.color = new Color(1f, 0.92f, 0.45f, 0f);
        header.outlineWidth = 0.22f;
        header.outlineColor = new Color32(0, 0, 0, 255);
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/PressStart2P-Regular SDF")
                          ?? Resources.Load<TMP_FontAsset>("PressStart2P-Regular SDF");
        if (font != null) header.font = font;

        // Subtitle
        GameObject sub = new GameObject("SubText");
        sub.transform.SetParent(root.transform, false);
        RectTransform sRt = sub.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.1f, 0.4f);
        sRt.anchorMax = new Vector2(0.9f, 0.55f);
        sRt.offsetMin = Vector2.zero;
        sRt.offsetMax = Vector2.zero;
        TextMeshProUGUI subText = sub.AddComponent<TextMeshProUGUI>();
        subText.text = "Sweetwater is safe. The dark cave awaits...";
        subText.alignment = TextAlignmentOptions.Center;
        subText.fontSize = 8f;
        subText.color = new Color(1f, 1f, 1f, 0f);
        subText.outlineWidth = 0.15f;
        subText.outlineColor = new Color32(0, 0, 0, 255);
        if (font != null) subText.font = font;

        // Fade in dim background + texts
        float t = 0f;
        while (t < 1.2f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 1.2f);
            dimImg.color = new Color(0f, 0f, 0f, k * 0.75f);
            header.color = new Color(1f, 0.92f, 0.45f, k);
            subText.color = new Color(1f, 1f, 1f, k);
            yield return null;
        }
    }
}
