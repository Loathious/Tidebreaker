using UnityEngine;

public class ZombieAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator anim;
    [SerializeField] private SpriteRenderer sr;
    
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float chaseSpeed = 2.5f;
    
    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private LayerMask playerLayer;
    
    [Header("Attack Settings")]
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.5f;
    
    [Header("Wander Settings")]
    [SerializeField] private float wanderChangeInterval = 3f;
    [SerializeField] private float wanderPauseChance = 0.3f;
    [SerializeField] private float wanderPauseDuration = 2f;
    
    private Vector2 wanderDirection;
    private float wanderTimer;
    private float attackTimer;
    private bool isWanderPaused;
    private float pauseTimer;
    private bool isDead;
    
    private Health zombieHealth;
    private Health playerHealth;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        zombieHealth = GetComponent<Health>();
        
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                playerHealth = playerObj.GetComponent<Health>();
            }
        }
        else if (playerHealth == null)
        {
            playerHealth = player.GetComponent<Health>();
        }
        
        if (zombieHealth != null)
        {
            zombieHealth.OnDeath.AddListener(OnDeath);
        }
        
        ChooseNewWanderDirection();
    }
    
    void Update()
    {
        if (isDead || player == null) return;
        
        attackTimer -= Time.deltaTime;
        
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        
        if (distanceToPlayer <= detectionRange)
        {
            ChasePlayer(distanceToPlayer);
        }
        else
        {
            Wander();
        }
        
        UpdateAnimation();
    }
    
    void FixedUpdate()
    {
        if (isDead) return;
        
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            FlipSprite();
        }
    }
    
    void ChasePlayer(float distance)
    {
        isWanderPaused = false;
        
        if (distance <= attackRange)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.3f, rb.linearVelocity.y);
            AttackPlayer();
        }
        else
        {
            Vector2 directionToPlayer = (player.position - transform.position).normalized;
            Vector2 horizontalDirection = new Vector2(directionToPlayer.x, 0f).normalized;
            rb.linearVelocity = new Vector2(horizontalDirection.x * chaseSpeed, rb.linearVelocity.y);
        }
    }
    
    void Wander()
    {
        if (isWanderPaused)
        {
            pauseTimer -= Time.deltaTime;
            rb.linearVelocity = Vector2.zero;
            
            if (pauseTimer <= 0f)
            {
                isWanderPaused = false;
                ChooseNewWanderDirection();
            }
            return;
        }
        
        wanderTimer -= Time.deltaTime;
        
        if (wanderTimer <= 0f)
        {
            if (Random.value < wanderPauseChance)
            {
                isWanderPaused = true;
                pauseTimer = wanderPauseDuration;
                rb.linearVelocity = Vector2.zero;
            }
            else
            {
                ChooseNewWanderDirection();
            }
        }
        
        rb.linearVelocity = wanderDirection * moveSpeed;
    }
    
    void ChooseNewWanderDirection()
    {
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        wanderDirection = new Vector2(Mathf.Cos(randomAngle), 0f).normalized;
        wanderTimer = wanderChangeInterval;
    }
    
    void AttackPlayer()
    {
        if (attackTimer <= 0f && playerHealth != null && !playerHealth.IsDead)
        {
            Debug.Log("Zombie attacking player!");
            playerHealth.TakeDamage(attackDamage);
            
            DamageEffect damageEffect = player.GetComponent<DamageEffect>();
            if (damageEffect != null)
            {
                Vector2 knockbackDir = (player.position - transform.position).normalized;
                damageEffect.ApplyKnockback(knockbackDir);
            }
            
            attackTimer = attackCooldown;
        }
    }
    
    void FlipSprite()
    {
        if (sr != null)
        {
            if (rb.linearVelocity.x > 0.1f)
            {
                sr.flipX = false;
            }
            else if (rb.linearVelocity.x < -0.1f)
            {
                sr.flipX = true;
            }
        }
    }
    
    void UpdateAnimation()
    {
        if (anim != null)
        {
            bool isMoving = rb.linearVelocity.magnitude > 0.1f;
            anim.SetBool("isWalking", isMoving);
        }
    }
    
    void OnDeath()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        
        if (anim != null)
        {
            anim.SetTrigger("isDead");
        }
        
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }
        
        Destroy(gameObject, 3f);
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;
        
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Zombie collision with player - ENTER");
            AttackPlayer();
        }
    }
    
    void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead) return;
        
        if (collision.gameObject.CompareTag("Player"))
        {
            AttackPlayer();
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
