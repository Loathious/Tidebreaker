using UnityEngine;

public class DamageEffect : MonoBehaviour
{
    [Header("Blood Particles")]
    [SerializeField] private ParticleSystem bloodParticles;
    [SerializeField] private int bloodParticleAmount = 10;
    
    [Header("Hurt Flash Effect")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color hurtColor = Color.red;
    [SerializeField] private int flashCount = 2;
    
    [Header("Knockback")]
    [SerializeField] private bool enableKnockback = true;
    [SerializeField] private float knockbackForce = 5f;
    
    private Color originalColor;
    private Rigidbody2D rb;
    private Health health;
    
    void Start()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        
        if (health != null)
        {
            health.OnDamageTaken.AddListener(OnDamageTaken);
        }
    }
    
    void OnDamageTaken(float damage)
    {
        Debug.Log($"DamageEffect: Taking {damage} damage, emitting blood");
        EmitBlood();
        StartCoroutine(HurtFlash());
    }
    
    void EmitBlood()
    {
        if (bloodParticles != null)
        {
            Debug.Log($"Emitting blood particles! IsPlaying: {bloodParticles.isPlaying}, Particle Count: {bloodParticles.particleCount}");
            
            bloodParticles.Clear();
            bloodParticles.Play();
            bloodParticles.Emit(bloodParticleAmount);
        }
        else
        {
            Debug.LogWarning("Blood particles is null!");
        }
    }
    
    System.Collections.IEnumerator HurtFlash()
    {
        if (spriteRenderer == null) yield break;
        
        for (int i = 0; i < flashCount; i++)
        {
            spriteRenderer.color = hurtColor;
            yield return new WaitForSeconds(flashDuration);
            
            spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(flashDuration);
        }
        
        spriteRenderer.color = originalColor;
    }
    
    public void ApplyKnockback(Vector2 direction)
    {
        // Knockback disabled - was causing player to get stuck
        // if (enableKnockback && rb != null)
        // {
        //     rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        //     rb.AddForce(direction.normalized * knockbackForce, ForceMode2D.Impulse);
        // }
    }
}
