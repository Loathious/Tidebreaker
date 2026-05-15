using System.Collections;
using UnityEngine;

/// <summary>
/// Villager NPC (Villager 1) — triggers story dialogue when the player enters the trigger zone.
/// Dialogue matches the spelmanus dokument.
/// After dialogue: gives rusty sword, shows objective, and forces zombies to chase.
/// </summary>
public class VillagerNPC : MonoBehaviour
{
    [SerializeField] private ItemData rustySwordItem;
    [SerializeField] private DialogUI dialogUI;
    [SerializeField] private GameObject tutorialText;
    [SerializeField] private Sprite villagerPortrait;

    private bool _hasTriggered = false;

    // Dialogue from spelmanus dokument
    private static readonly string[] StoryLines = new[]
    {
        "Hello adventurer! The monsters have kidnapped the other villagers. Please save them!",
        "Take this rusty old sword to fight the monsters. You must defeat all 6 of them.",
        "Be careful — the sword will break after 20 strikes. Left click to attack. Good luck!"
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

    void Awake()
    {
        if (dialogUI == null) dialogUI = FindFirstObjectByType<DialogUI>();

        if (tutorialText == null)
        {
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    var t = FindDeep(c.transform, "TutorialText");
                    if (t != null) { tutorialText = t.gameObject; break; }
                }
            }
        }
    }

    void Start()
    {
        if (tutorialText != null) tutorialText.SetActive(false);
        // Objective is hidden until dialogue completes
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !_hasTriggered)
        {
            _hasTriggered = true;
            ShowDialog();
        }
    }

    private void ShowDialog()
    {
        dialogUI.ShowDialog(
            "Villager",
            StoryLines,
            OnDialogComplete,
            villagerPortrait,
            transform   // ← camera locks onto this transform during dialogue
        );
    }

    private void OnDialogComplete()
    {
        GiveItemToPlayer();

        // Objective + combat music kick in IMMEDIATELY when dialogue ends.
        int targetCount = GameManager.Instance != null ? GameManager.Instance.enemiesToDefeat : 6;
        ObjectiveManager.Instance?.ShowObjective($"Defeat all {targetCount} monsters");
        GameManager.Instance?.NotifyCombatStarted();   // music now

        // Tutorial text shows alongside the objective (non-blocking)
        StartCoroutine(ShowTutorialBriefly());
        ForceChaseAllZombies();
    }

    private IEnumerator ShowTutorialBriefly()
    {
        if (tutorialText == null) yield break;
        tutorialText.SetActive(true);
        yield return new WaitForSeconds(4f);
        tutorialText.SetActive(false);
    }

    private void GiveItemToPlayer()
    {
        if (Inventory.Instance == null || rustySwordItem == null) return;
        Inventory.Instance.AddItem(rustySwordItem);
        Inventory.Instance.ToggleEquip(0);
    }

    private void ForceChaseAllZombies()
    {
        StartCoroutine(StaggeredForceChase());
    }

    private IEnumerator StaggeredForceChase()
    {
        ZombieAI[] zombies = FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
        foreach (ZombieAI zombie in zombies)
        {
            if (zombie != null)
            {
                zombie.ForceChase();
                yield return new WaitForSeconds(0.3f);
            }
        }
    }
}
