using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    
    [Header("Events")]
    public UnityEvent<float, float> OnHealthChanged;
    public UnityEvent OnDeath;
    public UnityEvent<float> OnDamageTaken;
    
    [Header("Invincibility")]
    [SerializeField] private float invincibilityDuration = 0.5f;
    private float invincibilityTimer = 0f;
    
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0f;
    public bool IsInvincible => invincibilityTimer > 0f;
    
    void Start()
    {
        if (currentHealth <= 0)
        {
            currentHealth = maxHealth;
        }
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    
    void Update()
    {
        if (invincibilityTimer > 0f)
        {
            invincibilityTimer -= Time.deltaTime;
        }
    }
    
    public void TakeDamage(float damage)
    {
        if (IsDead) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(damage);
        
        if (currentHealth <= 0f)
        {
            Die();
        }
    }
    
    public void Heal(float amount)
    {
        if (IsDead) return;
        
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    
    void Die()
    {
        OnDeath?.Invoke();
    }
    
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
