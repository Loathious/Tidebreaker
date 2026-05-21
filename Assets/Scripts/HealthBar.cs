// ═══════════════════════════════════════════════════════════════════════════════
// HEALTH BAR — VERSION 1  (superseded by HealthBarV2.cs)
//
// Kept for backward compatibility with existing scene setups (Village, Cave).
// DO NOT add this component to new scenes — HealthBarV2 auto-spawns instead.
// ═══════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Health targetHealth;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image lostHealthImage;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private bool showText = true;

    private float _lostHealthDecayDelay = 0.8f;
    private float _lostHealthDecaySpeed = 0.5f;
    private float _delayTimer           = 0f;

    void Awake()
    {
        // HealthBar is valid in all gameplay scenes (Village, Cave, etc.)
    }

    void Start()
    {
        // Auto-find fillImage: try exact name "Fill", then partial name match,
        // then any non-root Image child as a last resort.
        if (fillImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            Image selfImg  = GetComponent<Image>();
            foreach (Image img in images)
            {
                if (img == selfImg) continue;
                string n = img.gameObject.name.ToLower();
                if (n == "fill" || n.Contains("fill") || n == "healthimage" || n == "hpfill")
                { fillImage = img; break; }
            }
            // Fallback: first non-root Image child
            if (fillImage == null)
                foreach (Image img in images)
                    if (img != selfImg) { fillImage = img; break; }
        }

        // Auto-find targetHealth: first check parent hierarchy (works when the bar
        // is a child of the player), then fall back to the Player-tagged object
        // (works when the bar lives inside a Canvas unrelated to the player).
        if (targetHealth == null)
            targetHealth = GetComponentInParent<Health>();
        if (targetHealth == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                targetHealth = p.GetComponent<Health>() ?? p.GetComponentInChildren<Health>();
        }

        if (fillImage != null)
        {
            fillImage.sprite     = null;
            fillImage.material   = null;
            fillImage.type       = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 1f;
            fillImage.color      = new Color(0.15f, 0.85f, 0.2f, 1f); // always green
        }

        // Auto-create lostHealthImage if not assigned — builds it as a sibling
        // placed directly behind fillImage in the hierarchy.
        if (lostHealthImage == null && fillImage != null)
        {
            // Check if a sibling is already named "LostHealth"
            Transform parent = fillImage.transform.parent;
            if (parent != null)
            {
                Transform existing = parent.Find("LostHealth");
                if (existing != null)
                    lostHealthImage = existing.GetComponent<Image>();
            }

            if (lostHealthImage == null && parent != null)
            {
                GameObject lhGO = new GameObject("LostHealth");
                lhGO.transform.SetParent(parent, false);

                // Match the RectTransform exactly to fillImage
                RectTransform fillRt = fillImage.GetComponent<RectTransform>();
                RectTransform lhRt   = lhGO.AddComponent<RectTransform>();
                lhRt.anchorMin        = fillRt.anchorMin;
                lhRt.anchorMax        = fillRt.anchorMax;
                lhRt.offsetMin        = fillRt.offsetMin;
                lhRt.offsetMax        = fillRt.offsetMax;
                lhRt.pivot            = fillRt.pivot;

                lostHealthImage = lhGO.AddComponent<Image>();
            }
        }

        if (lostHealthImage != null)
        {
            lostHealthImage.sprite     = null;
            lostHealthImage.material   = null;
            lostHealthImage.type       = Image.Type.Filled;
            lostHealthImage.fillMethod = Image.FillMethod.Horizontal;
            lostHealthImage.fillOrigin = 0;
            lostHealthImage.fillAmount = 1f;
            lostHealthImage.color      = new Color(0.85f, 0.1f, 0.1f, 0.9f); // always red
        }

        // Lost-health (red) must render behind the current-health fill (green).
        // In Unity UI, lower sibling index = rendered first = appears behind.
        if (lostHealthImage != null && fillImage != null)
        {
            int fillIdx = fillImage.transform.GetSiblingIndex();
            int lostIdx = lostHealthImage.transform.GetSiblingIndex();
            if (lostIdx > fillIdx)
                lostHealthImage.transform.SetSiblingIndex(fillIdx);
        }

        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged.AddListener(UpdateHealthBar);
            targetHealth.OnDeath.AddListener(OnDeath);
            UpdateHealthBar(targetHealth.CurrentHealth, targetHealth.MaxHealth);
        }
    }

    void Update()
    {
        if (lostHealthImage == null) return;

        if (_delayTimer > 0f)
        {
            _delayTimer -= Time.unscaledDeltaTime;
        }
        else if (lostHealthImage.fillAmount > fillImage.fillAmount)
        {
            lostHealthImage.fillAmount = Mathf.MoveTowards(
                lostHealthImage.fillAmount, fillImage.fillAmount,
                _lostHealthDecaySpeed * Time.unscaledDeltaTime);
        }
    }

    void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        float fill = currentHealth / Mathf.Max(maxHealth, 1f);

        if (fillImage != null)
            fillImage.fillAmount = fill;

        if (lostHealthImage != null)
        {
            if (fill >= lostHealthImage.fillAmount)
            {
                // Health increased or reset to full — snap the red bar up immediately
                // so there's no phantom red gap at full health.
                lostHealthImage.fillAmount = fill;
            }
            else
            {
                // Health decreased — start the visual decay delay
                _delayTimer = _lostHealthDecayDelay;
            }
        }

        if (healthText != null && showText)
            healthText.text = Mathf.CeilToInt(currentHealth).ToString();
    }

    void OnDeath()
    {
        gameObject.SetActive(false);
    }

    /// <summary>Hides the health bar immediately.</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
