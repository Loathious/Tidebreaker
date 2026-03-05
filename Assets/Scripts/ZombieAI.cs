using UnityEngine;

/// <summary>
/// Zombie enemy: wanders, chases player, attacks on proximity, pushes player off its top.
/// Knockback is applied to the player on each attack hit.
/// </summary>
public class ZombieAI : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed  = 2f;
    [SerializeField] private float chaseSpeed = 2.5f;

    [Header("Detection & Attack")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float attackRange    = 0.9f;
    [SerializeField] private float attackDamage   = 10f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float attackKnockback = 4f;

    [Header("Wander")]
    [SerializeField] private float wanderInterval = 3f;
    [SerializeField] private float pauseChance    = 0.3f;
    [SerializeField] private float pauseDuration  = 2f;

    [Header("Physics")]
    [SerializeField] private float topPushForce = 9f;

    private Transform      playerTransform;
    private Health         playerHealth;
    private Rigidbody2D    rb;
    private Animator       anim;
    private SpriteRenderer sr;
    private Health         health;

    private float   attackTimer;
    private float   wanderTimer;
    private float   pauseTimer;
    private float   knockbackTimer;
    private bool    isPaused;
    private bool    isDead;
    private Vector2 wanderDir;

    private const float KnockbackDuration = 0.35f;

    void Start()
    {
        rb     = GetComponent<Rigidbody2D>();
        anim   = GetComponent<Animator>();
        sr     = GetComponent<SpriteRenderer>();
        health = GetComponent<Health>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerHealth    = playerObj.GetComponent<Health>();
        }

        health?.OnDeath.AddListener(OnDeath);
        PickNewWanderDir();
    }

    void Update()
    {
        if (isDead || playerTransform == null) return;

        attackTimer   -= Time.deltaTime;
        knockbackTimer -= Time.deltaTime;

        // Suppress AI movement while being knocked back
        if (knockbackTimer > 0f)
        {
            anim?.SetBool("isWalking", false);
            return;
        }

        float dist = Vector2.Distance(transform.position, playerTransform.position);
        if (dist <= detectionRange) Chase(dist);
        else                        Wander();

        anim?.SetBool("isWalking", Mathf.Abs(rb.linearVelocity.x) > 0.1f);
    }

    void FixedUpdate()
    {
        if (isDead || sr == null) return;
        if (Mathf.Abs(rb.linearVelocity.x) > 0.05f)
            sr.flipX = rb.linearVelocity.x < 0;
    }

    void Chase(float dist)
    {
        isPaused = false;

        if (dist <= attackRange)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.3f, rb.linearVelocity.y);
            TryAttack();
        }
        else
        {
            float dir = Mathf.Sign(playerTransform.position.x - transform.position.x);
            rb.linearVelocity = new Vector2(dir * chaseSpeed, rb.linearVelocity.y);
        }
    }

    void TryAttack()
    {
        if (attackTimer > 0f || playerHealth == null || playerHealth.IsDead) return;

        playerHealth.TakeDamage(attackDamage);

        Rigidbody2D playerRb = playerTransform.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            Vector2 dir = (playerTransform.position - transform.position).normalized;
            playerRb.AddForce(dir * attackKnockback, ForceMode2D.Impulse);
        }

        attackTimer = attackCooldown;
    }

    void Wander()
    {
        if (isPaused)
        {
            pauseTimer -= Time.deltaTime;
            rb.linearVelocity = Vector2.zero;
            if (pauseTimer <= 0f) { isPaused = false; PickNewWanderDir(); }
            return;
        }

        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0f)
        {
            if (Random.value < pauseChance) { isPaused = true; pauseTimer = pauseDuration; }
            else                            PickNewWanderDir();
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = new Vector2(wanderDir.x * moveSpeed, rb.linearVelocity.y);
    }

    void PickNewWanderDir()
    {
        wanderDir   = new Vector2(Random.value > 0.5f ? 1f : -1f, 0f);
        wanderTimer = wanderInterval;
    }

    /// <summary>
    /// Applies knockback impulse and freezes AI movement for KnockbackDuration seconds
    /// so the velocity is not immediately overwritten by the movement logic.
    /// </summary>
    public void Knockback(Vector2 force)
    {
        rb.linearVelocity = Vector2.zero;           // clear current velocity first
        rb.AddForce(force, ForceMode2D.Impulse);
        knockbackTimer = KnockbackDuration;
    }

    /// <summary>Detects when the player is standing on top and shoves them sideways.</summary>
    void OnCollisionStay2D(Collision2D col)
    {
        if (isDead || !col.gameObject.CompareTag("Player")) return;

        foreach (ContactPoint2D contact in col.contacts)
        {
            // normal.y > 0.6 means the contact surface faces upward → player is on top
            if (contact.normal.y > 0.6f)
            {
                Rigidbody2D playerRb = col.gameObject.GetComponent<Rigidbody2D>();
                if (playerRb == null) break;

                float side = col.gameObject.transform.position.x > transform.position.x ? 1f : -1f;
                playerRb.AddForce(new Vector2(side * topPushForce, 2.5f), ForceMode2D.Impulse);
                break;
            }
        }
    }

    void OnDeath()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        anim?.SetTrigger("isDead");
        Collider2D col = GetComponent<Collider2D>();
        if (col) col.enabled = false;
        Destroy(gameObject, 3f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

