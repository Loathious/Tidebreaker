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
    private float _respawnCooldown;

    void Awake()
    {
        foreach (Collider2D c in GetComponents<Collider2D>()) c.isTrigger = true;
    }

    void Update()
    {
        if (_respawnCooldown > 0f) _respawnCooldown -= Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (mode == Mode.Respawn)
        {
            if (_respawnCooldown > 0f) return;
            _respawnCooldown = 2.0f;   // extended cooldown to cover the full fall + landing
            RespawnPlayer(other);
        }
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

        // Calculate a safe respawn position well above the water.
        // Use the respawnPoint's position if assigned, otherwise spawn above the
        // player's current position. Always add enough height to clear the platforms.
        Vector3 safePos;
        if (respawnPoint != null)
        {
            // Place the player 6 units above the respawn marker's position so they
            // land on top of it (not inside it) even after a fast fall.
            safePos = respawnPoint.position + Vector3.up * 6f;
        }
        else
        {
            // No respawn point: spawn well above current position.
            safePos = player.transform.position + Vector3.up * 10f;
        }

        // Reset velocity BEFORE teleporting to avoid carry-over momentum.
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        // Teleport: set BOTH the transform and the Rigidbody2D position so the
        // physics engine and the rendering are in sync immediately. Only setting
        // transform.position can cause a one-frame physics desync that pulls the
        // player back into the trigger, restarting the respawn loop.
        player.transform.position = safePos;
        if (rb != null)
            rb.position = new Vector2(safePos.x, safePos.y);

        // Force an immediate physics sync so no stale trigger overlap fires next step.
        Physics2D.SyncTransforms();
    }
}
