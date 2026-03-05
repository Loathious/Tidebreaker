using UnityEngine;
using TMPro;

public class SwordPickup : MonoBehaviour
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private GameObject promptUI;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private float pickupAnimDuration = 0.6f;
    [SerializeField] private float shrinkScale = 0.3f;
    
    private bool playerInRange;
    private bool isPickingUp;
    private Transform player;
    private Vector3 startPosition;
    private Vector3 startScale;
    private float pickupTimer;
    
    void Start()
    {
        startPosition = transform.position;
        startScale = transform.localScale;
        
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
        
        CreatePromptIfNeeded();
    }
    
    void CreatePromptIfNeeded()
    {
        if (promptUI == null)
        {
            GameObject canvasObj = GameObject.Find("GameCanvas");
            if (canvasObj == null) return;
            
            promptUI = new GameObject("PickupPrompt");
            promptUI.transform.SetParent(canvasObj.transform);
            
            RectTransform rect = promptUI.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 50);
            
            promptText = promptUI.AddComponent<TextMeshProUGUI>();
            promptText.text = "Press E to pickup";
            promptText.fontSize = 14;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = Color.white;
            
            promptUI.SetActive(false);
        }
    }
    
    void Update()
    {
        if (isPickingUp)
        {
            UpdatePickupAnimation();
        }
        else if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            TryPickup();
        }
        
        if (promptUI != null && playerInRange && !isPickingUp)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.7f);
            promptUI.transform.position = screenPos;
        }
    }
    
    void TryPickup()
    {
        if (Inventory.Instance != null && itemData != null)
        {
            if (Inventory.Instance.AddItem(itemData))
            {
                isPickingUp = true;
                pickupTimer = 0f;
                
                if (promptUI != null)
                {
                    promptUI.SetActive(false);
                }
            }
        }
    }
    
    void UpdatePickupAnimation()
    {
        if (player == null) return;
        
        pickupTimer += Time.deltaTime;
        float t = pickupTimer / pickupAnimDuration;
        
        transform.position = Vector3.Lerp(startPosition, player.position, t);
        transform.localScale = Vector3.Lerp(startScale, startScale * shrinkScale, t);
        
        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            player = other.transform;
            
            if (promptUI != null)
            {
                promptUI.SetActive(true);
            }
        }
    }
    
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            
            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }
        }
    }
}
