using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Triggers level transition to the Dark Cave scene when the player enters,
/// but only after Level 1 is complete.
/// </summary>
public class CaveEntrance : MonoBehaviour
{
    [SerializeField] private string caveSceneName = "Cave";
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private float promptOffset = 1.5f;

    private bool playerInRange;

    void Start()
    {
        if (promptText != null) promptText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!playerInRange) return;

        if (promptText != null)
        {
            Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * promptOffset);
            promptText.transform.position = sp;
        }

        if (Input.GetKeyDown(KeyCode.E))
            SceneManager.LoadScene(caveSceneName);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (GameManager.Instance == null || !GameManager.Instance.LevelComplete) return;

        playerInRange = true;
        if (promptText != null)
        {
            promptText.text = "Press E to enter the Dark Cave";
            promptText.gameObject.SetActive(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        if (promptText != null) promptText.gameObject.SetActive(false);
    }
}
