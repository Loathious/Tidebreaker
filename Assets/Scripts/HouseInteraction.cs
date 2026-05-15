using UnityEngine;
using TMPro;

/// <summary>
/// Press E near a house to open it and reveal the villager inside.
/// </summary>
public class HouseInteraction : MonoBehaviour
{
    [SerializeField] private int houseIndex;
    [SerializeField] private Sprite openDoorSprite;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private float promptOffset = 1.5f;
    [SerializeField] private AudioClip doorOpenClip;

    private SpriteRenderer doorRenderer;
    private AudioSource audioSource;
    private bool playerInRange;
    private bool isOpen;

    void Start()
    {
        doorRenderer = GetComponent<SpriteRenderer>();
        audioSource  = GetComponent<AudioSource>();
        if (promptText != null) promptText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (isOpen) return;
        if (playerInRange && Input.GetKeyDown(KeyCode.E)) OpenHouse();

        if (promptText != null && playerInRange)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * promptOffset);
            promptText.transform.position = screenPos;
        }
    }

    void OpenHouse()
    {
        isOpen = true;
        if (promptText != null) promptText.gameObject.SetActive(false);

        // Swap to open door sprite
        if (doorRenderer != null && openDoorSprite != null)
            doorRenderer.sprite = openDoorSprite;

        if (audioSource != null && doorOpenClip != null)
            audioSource.PlayOneShot(doorOpenClip);

        // house opened — no level manager tracking needed
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isOpen)
        {
            playerInRange = true;
            if (promptText != null)
            {
                promptText.text = "Press E to open";
                promptText.gameObject.SetActive(true);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (promptText != null) promptText.gameObject.SetActive(false);
        }
    }
}
