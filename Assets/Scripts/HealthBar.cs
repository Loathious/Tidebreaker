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
        // Auto-find fillImage by searching every Image in children if not assigned
        if (fillImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (Image img in images)
            {
                if (img.gameObject.name == "Fill")
                {
                    fillImage = img;
                    break;
                }
            }
        }

        // Auto-find targetHealth on parent if not assigned
        if (targetHealth == null)
            targetHealth = GetComponentInParent<Health>();

        if (fillImage != null)
        {
            // Strip the gradient sprite/material so the fill renders as a clean
            // solid color. PRESERVE whatever color the prefab/scene set
            // (player = green, zombie = red, etc.) — never overwrite it.
            fillImage.sprite     = null;
            fillImage.material   = null;
            fillImage.type       = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 1f;
            // Force full alpha so a partially-transparent prefab color reads cleanly.
            Color c = fillImage.color; c.a = 1f; fillImage.color = c;
        }

        if (lostHealthImage != null)
        {
            lostHealthImage.sprite     = null;
            lostHealthImage.material   = null;
            lostHealthImage.type       = Image.Type.Filled;
            lostHealthImage.fillMethod = Image.FillMethod.Horizontal;
            lostHealthImage.fillOrigin = 0;
            lostHealthImage.fillAmount = 1f;
            // Preserve prefab color, just guarantee a faded alpha for the trail look.
            Color lc = lostHealthImage.color;
            if (lc.a > 0.7f) lc.a = 0.6f;
            lostHealthImage.color = lc;
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
        if (fillImage != null)
            fillImage.fillAmount = currentHealth / Mathf.Max(maxHealth, 1f);

        _delayTimer = _lostHealthDecayDelay;

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
