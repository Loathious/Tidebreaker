using UnityEngine;

/// <summary>
/// Generic 2D projectile. Used for the player's arrows, monkey coconuts and the
/// Kraken's energy blasts. Travels in a straight line, damages the first valid
/// target it overlaps, then dies. Also dies on hitting solid ground or on timeout.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [Header("Flight")]
    public Vector2 velocity   = Vector2.right * 14f;
    public float   lifetime   = 5f;
    public float   gravity    = 0f;
    public bool    faceVelocity = true;

    [Header("Damage")]
    public float damage      = 25f;
    public bool  hitsPlayer  = false;   // enemy projectiles set this true
    public bool  hitsEnemies = true;    // player arrows set this true

    [Header("FX")]
    public GameObject hitEffectPrefab;
    public AudioClip  hitClip;
    public AudioClip  missClip;

    private Rigidbody2D _rb;
    private bool        _spent;
    private bool        _playedFlightAudio;

    /// <summary>Configures the projectile in one call (used by spawners).</summary>
    public void Launch(Vector2 dir, float speed, float dmg, bool toPlayer)
    {
        velocity    = dir.normalized * speed;
        damage      = dmg;
        hitsPlayer  = toPlayer;
        hitsEnemies = !toPlayer;
    }

    /// <summary>
    /// Builds a projectile GameObject from scratch and launches it. No prefab needed.
    /// </summary>
    public static Projectile Spawn(Vector3 pos, Vector2 dir, float speed, float dmg,
                                   bool toPlayer, Sprite sprite, float gravity = 0f,
                                   float radius = 0.18f, int sortingOrder = 50)
    {
        GameObject go = new GameObject("Projectile");
        go.transform.position = pos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite != null ? sprite : ProceduralSprite.Circle(8, Color.white);
        sr.sortingOrder = sortingOrder;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius    = radius;
        col.isTrigger = true;

        Projectile p = go.AddComponent<Projectile>();
        p.gravity = gravity;
        p.Launch(dir, speed, dmg, toPlayer);
        return p;
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.bodyType     = RigidbodyType2D.Kinematic;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        foreach (Collider2D c in GetComponents<Collider2D>())
            c.isTrigger = true;
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (_spent) return;

        if (gravity != 0f)
            velocity += Vector2.down * gravity * Time.deltaTime;

        transform.position += (Vector3)(velocity * Time.deltaTime);

        if (faceVelocity && velocity.sqrMagnitude > 0.001f)
        {
            float ang = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_spent) return;
        if (other.isTrigger) return;

        bool isPlayer = other.CompareTag("Player");

        // Solid ground / walls stop the projectile
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            Miss();
            return;
        }

        Health hp = other.GetComponent<Health>() ?? other.GetComponentInParent<Health>();
        if (hp == null) return;

        if (isPlayer && hitsPlayer)
        {
            hp.TakeDamage(damage);
            Hit(other.bounds.center);
        }
        else if (!isPlayer && hitsEnemies)
        {
            // Don't let player arrows hit other enemies' projectiles, etc.
            if (other.GetComponent<Projectile>() != null) return;
            hp.TakeDamage(damage);
            Hit(other.bounds.center);
        }
    }

    private void Hit(Vector3 at)
    {
        _spent = true;
        if (hitEffectPrefab != null) Instantiate(hitEffectPrefab, at, Quaternion.identity);
        HitSpark.Spawn(at);
        if (hitClip != null)
            AudioSource.PlayClipAtPoint(hitClip, at, 0.8f);
        Destroy(gameObject);
    }

    private void Miss()
    {
        _spent = true;
        if (missClip != null)
            AudioSource.PlayClipAtPoint(missClip, transform.position, 0.6f);
        Destroy(gameObject, 0.02f);
    }
}
