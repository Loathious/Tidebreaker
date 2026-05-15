using UnityEngine;
using TMPro;
using System.Collections;

public class SwordPickup : MonoBehaviour
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private GameObject promptUI;
    [SerializeField] private TextMeshProUGUI promptText;

    [Header("Pickup Animation")]
    [SerializeField] private float riseHeight        = 1.4f;
    [SerializeField] private float riseDuration      = 0.35f;
    [SerializeField] private float holdDuration      = 0.5f;
    [SerializeField] private float spinDuration      = 0.35f;
    [SerializeField] private float flyDuration       = 0.3f;
    [SerializeField] private float glowColorR        = 1f;
    [SerializeField] private float glowColorG        = 0.92f;
    [SerializeField] private float glowColorB        = 0.3f;

    private bool          _playerInRange;
    private bool          _isPickingUp;
    private Transform     _player;
    private Vector3       _startPosition;
    private Vector3       _startScale;
    private SpriteRenderer _sr;

    void Start()
    {
        _startPosition = transform.position;
        _startScale    = transform.localScale;
        _sr            = GetComponent<SpriteRenderer>();

        if (promptUI != null)
            promptUI.SetActive(false);

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
            promptText.text      = "Press E to pickup";
            promptText.fontSize  = 14;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color     = Color.white;

            promptUI.SetActive(false);
        }
    }

    void Update()
    {
        if (_isPickingUp) return;

        if (_playerInRange && Input.GetKeyDown(KeyCode.E))
            TryPickup();

        if (promptUI != null && _playerInRange)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.7f);
            promptUI.transform.position = screenPos;
        }
    }

    void TryPickup()
    {
        if (Inventory.Instance == null || itemData == null) return;
        if (!Inventory.Instance.AddItem(itemData)) return;

        _isPickingUp = true;
        if (promptUI != null) promptUI.SetActive(false);
        StartCoroutine(PlayPickupAnimation());
    }

    IEnumerator PlayPickupAnimation()
    {
        // Disable physics so the sword floats freely
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        Color baseColor = _sr != null ? _sr.color : Color.white;
        Color goldColor = new Color(glowColorR, glowColorG, glowColorB);
        Vector3 riseTarget = _startPosition + Vector3.up * riseHeight;

        // ── Phase 1: Rise + spin ────────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / riseDuration);
            transform.position   = Vector3.Lerp(_startPosition, riseTarget, t);
            transform.localScale = Vector3.Lerp(_startScale, _startScale * 1.35f, t);
            transform.Rotate(0f, 0f, 360f * Time.deltaTime / spinDuration);
            if (_sr != null) _sr.color = Color.Lerp(baseColor, goldColor, t);
            yield return null;
        }

        transform.position = riseTarget;

        // ── Phase 2: Hold + glow pulse ──────────────────────────────────────
        elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.PI * 6f);
            transform.Rotate(0f, 0f, 360f * Time.deltaTime / spinDuration);
            if (_sr != null) _sr.color = Color.Lerp(goldColor, Color.white, pulse * 0.35f);
            yield return null;
        }

        // ── Phase 3: Arc fly to player ──────────────────────────────────────
        Vector3 flyStart = transform.position;
        Vector3 flyScale = transform.localScale;
        elapsed = 0f;
        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / flyDuration);

            Vector3 target  = _player != null ? _player.position : transform.position;
            // Slight arc upward during flight
            Vector3 mid     = Vector3.Lerp(flyStart, target, 0.5f) + Vector3.up * 0.5f;
            Vector3 pos     = Vector3.Lerp(Vector3.Lerp(flyStart, mid, t), Vector3.Lerp(mid, target, t), t);

            transform.position   = pos;
            transform.localScale = Vector3.Lerp(flyScale, Vector3.zero, t);
            transform.Rotate(0f, 0f, 720f * Time.deltaTime / flyDuration);
            if (_sr != null) _sr.color = Color.Lerp(goldColor, Color.clear, t);
            yield return null;
        }

        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = true;
        _player        = other.transform;
        if (promptUI != null) promptUI.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = false;
        if (promptUI != null) promptUI.SetActive(false);
    }
}
