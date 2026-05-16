using UnityEngine;

/// <summary>
/// A hazard trigger volume. Two modes:
///  • Damage  — drains health while the player stands in it (spikes, lava).
///  • Respawn — bounces the player back to a safe point and deals a small hit
///              (used for the ocean water below the Kraken arena platforms).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HazardZone : MonoBehaviour
{
    public enum Mode { Damage, Respawn }

    [SerializeField] private Mode mode = Mode.Damage;
    [SerializeField] private float damage = 12f;
    [SerializeField] private float tickInterval = 0.5f;
    [Tooltip("Where the player is sent in Respawn mode. Defaults to this object's start.")]
    public Transform respawnPoint;

    private float _tickTimer;

    void Awake()
    {
        foreach (Collider2D c in GetComponents<Collider2D>()) c.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (mode == Mode.Respawn) RespawnPlayer(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (mode != Mode.Damage) return;

        _tickTimer -= Time.deltaTime;
        if (_tickTimer <= 0f)
        {
            _tickTimer = tickInterval;
            Health hp = other.GetComponent<Health>() ?? other.GetComponentInParent<Health>();
            if (hp != null && !hp.IsDead) hp.TakeDamage(damage);
        }
    }

    private void RespawnPlayer(Collider2D player)
    {
        Health hp = player.GetComponent<Health>() ?? player.GetComponentInParent<Health>();
        if (hp != null && !hp.IsDead) hp.TakeDamage(damage);

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (respawnPoint != null)
            player.transform.position = respawnPoint.position;
        else
            player.transform.position += Vector3.up * 6f;
    }
}
