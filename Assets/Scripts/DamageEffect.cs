using System.Collections;
using UnityEngine;

/// <summary>
/// Plays blood particles and sprite flash when this GameObject takes damage.
/// Attach to any GameObject that has a Health component.
/// </summary>
public class DamageEffect : MonoBehaviour
{
    [Header("Blood Particles")]
    [SerializeField] private ParticleSystem bloodParticles;
    [SerializeField] private int bloodParticleAmount = 12;

    [Header("Hurt Flash")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hurtColor = Color.red;
    [SerializeField] private float flashDuration = 0.08f;
    [SerializeField] private int flashCount = 2;

    private Color originalColor;
    private Health health;

    void Start()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        health = GetComponent<Health>();
        if (health != null)
            health.OnDamageTaken.AddListener(OnDamageTaken);
    }

    void OnDamageTaken(float damage)
    {
        EmitBlood();
        StartCoroutine(HurtFlash());
    }

    void EmitBlood()
    {
        if (bloodParticles == null) return;

        // Stop completely then emit a one-shot burst - no Play() needed for Emit()
        bloodParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        bloodParticles.Emit(bloodParticleAmount);
    }

    IEnumerator HurtFlash()
    {
        if (spriteRenderer == null) yield break;

        for (int i = 0; i < flashCount; i++)
        {
            spriteRenderer.color = hurtColor;
            yield return new WaitForSecondsRealtime(flashDuration);
            spriteRenderer.color = originalColor;
            yield return new WaitForSecondsRealtime(flashDuration);
        }

        spriteRenderer.color = originalColor;
    }

    public void ApplyKnockback(Vector2 direction) { }
}
