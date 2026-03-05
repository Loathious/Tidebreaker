using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Health targetHealth;
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private bool showText = true;
    
    void Start()
    {
        if (fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
        }
        
        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged.AddListener(UpdateHealthBar);
            targetHealth.OnDeath.AddListener(OnDeath);
            UpdateHealthBar(targetHealth.CurrentHealth, targetHealth.MaxHealth);
        }
    }
    
    void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (fillImage != null)
        {
            float fillPercent = currentHealth / maxHealth;
            fillImage.fillAmount = fillPercent;
        }
        
        if (healthText != null && showText)
        {
            healthText.text = Mathf.CeilToInt(currentHealth).ToString();
        }
    }
    
    void OnDeath()
    {
        gameObject.SetActive(false);
    }
    
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
