using System.Collections;
using UnityEngine;

/// <summary>
/// Zombie enemy: wanders, chases player, attacks on proximity.
/// Features: wall climbing, escalating combo knockback, dynamic aggression.
/// isAttacking trigger only fired if the parameter exists in the Animator.
/// </summary>
public class ZombieAI : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed    = 2f;
    [SerializeField] private float chaseSpeed   = 2.8f;
    [SerializeField] private float acceleration = 10f;

    [Header("Detection & Attack")]
    [SerializeField] private float detectionRange  = 8f;
    [SerializeField] private float attackRange     = 1.4f;
    [SerializeField] private float attackDamage    = 10f;
    [SerializeField] private float attackCooldown  = 1.5f;
    [SerializeField] private float attackKnockback = 3f;

    [Header("Wander")]
    [SerializeField] private float wanderInterval = 3f;
    [SerializeField] private float pauseChance    = 0.3f;
    [SerializeField] private float pauseDuration  = 2f;

    [Header("Wall Climbing")]
    [SerializeField] private bool  canClimbWalls       = true;
    [SerializeField] private float climbSpeed          = 1.6f;   // SLOW realistic vertical creep
    [SerializeField] private float wallCheckDist       = 0.55f;  // reach for the multi-height raycasts
    [SerializeField] private float climbDuration       = 1.6f;   // how long the slow climb lasts
    [SerializeField] private float climbHopForce       = 4.5f;   // gentle hop over the top
    [SerializeField] private float climbCooldown       = 1.2f;   // shorter so they retry quickly when blocked
    [SerializeField] private float climbWallStickForce = 0.4f;   // lateral push into wall

    [Header("Knockback")]
    [SerializeField] private float knockbackDuration        = 0.45f;
    [SerializeField] private float knockbackStunAdditional  = 0.15f;

    private Transform      _playerTransform;
    private Health         _playerHealth;
    private Rigidbody2D    _rb;
    private Animator       _anim;
    private SpriteRenderer _sr;
    private Health         _health;

    private float   _attackTimer;
    private float   _wanderTimer;
    private float   _pauseTimer;
    private float   _knockbackTimer;
    private float   _stunTimer;
    private float   _pushCooldown;
    private bool    _isPaused;
    private bool    _isDead;
    public  bool    IsDefeated => _isDead;
    private Vector2 _wanderDir;
    private float   _targetVelocityX;
    private float   _climbCooldownTimer;
    private bool    _isClimbing;
    private float   _climbDir;          // lateral direction while climbing (-1 / +1)
    private float   _origGravityScale;  // restored after climb ends
    private bool    _forcedChase;

    // Animator parameter existence cache
    private bool _hasIsAttacking;
    private bool _hasIsWalking;
    private bool _hasIsClimbing;

    // Combo system
    private int   _consecutiveHits;
    private float _hitComboResetTimer;
    private const float HitComboWindow = 2f;

    void Start()
    {
        _rb     = GetComponent<Rigidbody2D>();
        _anim   = GetComponent<Animator>();
        _sr     = GetComponent<SpriteRenderer>();
        _health = GetComponent<Health>();

        // Cache which animator parameters actually exist to avoid console spam
        if (_anim != null)
        {
            foreach (var param in _anim.parameters)
            {
                if (param.name == "isAttacking") _hasIsAttacking = true;
                if (param.name == "isWalking")   _hasIsWalking   = true;
                if (param.name == "isClimbing")  _hasIsClimbing  = true;
            }
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
            _playerHealth    = playerObj.GetComponent<Health>();
        }

        _health?.OnDeath.AddListener(OnDeath);
        PickNewWanderDir();
    }

    void Update()
    {
        if (_isDead || _playerTransform == null) return;

        _attackTimer        -= Time.deltaTime;
        _knockbackTimer     -= Time.deltaTime;
        _pushCooldown       -= Time.deltaTime;
        _stunTimer          -= Time.deltaTime;
        _hitComboResetTimer -= Time.deltaTime;
        _climbCooldownTimer -= Time.deltaTime;

        if (_hitComboResetTimer <= 0f) _consecutiveHits = 0;

        if (_knockbackTimer > 0f || _stunTimer > 0f)
        {
            _targetVelocityX = 0f;
            if (_hasIsWalking) _anim?.SetBool("isWalking", false);
            return;
        }

        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        if (dist <= detectionRange || _forcedChase) DecideChase(dist);
        else                                        DecideWander();

        if (_hasIsWalking)
            _anim?.SetBool("isWalking", Mathf.Abs(_targetVelocityX) > 0.1f);
    }

    void FixedUpdate()
    {
        if (_isDead) return;

        if (_isClimbing)
        {
            // While climbing: lateral push into wall + slow vertical creep + zero gravity
            // (gravity is set to 0 in WallClimbJump and restored when we drop off)
            _rb.linearVelocity = new Vector2(_climbDir * climbWallStickForce, climbSpeed);
            return;
        }

        float smoothVx = Mathf.Lerp(_rb.linearVelocity.x, _targetVelocityX, acceleration * Time.fixedDeltaTime);
        _rb.linearVelocity = new Vector2(smoothVx, _rb.linearVelocity.y);

        if (_sr != null && Mathf.Abs(_rb.linearVelocity.x) > 0.05f)
            _sr.flipX = _rb.linearVelocity.x < 0;

        // Wall climb logic
        if (canClimbWalls && _targetVelocityX != 0f && _climbCooldownTimer <= 0f)
            TryClimbWall();
    }

    // ── Wall Climbing ─────────────────────────────────────────────────────────
    void TryClimbWall()
    {
        float dir = Mathf.Sign(_targetVelocityX);
        if (Mathf.Abs(_rb.linearVelocity.x) > 0.1f) return;   // still moving normally — no wall stuck
        if (_rb.linearVelocity.y > 0.5f)            return;   // already moving up

        // Cast THREE rays at different heights so we catch walls of any height
        // (foot, mid, head). Use "everything" layer mask but reject self/other zombies.
        Collider2D ownCol = GetComponent<Collider2D>();
        Bounds b = ownCol != null ? ownCol.bounds : new Bounds(transform.position, Vector3.one);

        float[] heights = { b.min.y + 0.05f, b.center.y, b.max.y - 0.05f };
        for (int i = 0; i < heights.Length; i++)
        {
            Vector2 origin = new Vector2(transform.position.x, heights[i]);
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.right * dir, wallCheckDist);
            foreach (RaycastHit2D h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.isTrigger) continue;
                if (h.collider.gameObject == gameObject) continue;
                if (h.collider.GetComponent<ZombieAI>() != null) continue;  // ignore other zombies
                if (h.collider.CompareTag("Player")) continue;              // don't climb on player

                // Wall confirmed — start the slow climb
                StartCoroutine(WallClimbJump(dir));
                return;
            }
        }
    }

    IEnumerator WallClimbJump(float dir)
    {
        _isClimbing         = true;
        _climbDir           = dir;
        _climbCooldownTimer = climbCooldown;
        if (_hasIsClimbing) _anim?.SetBool("isClimbing", true);

        // Disable gravity during the slow scale — gives a true "scaling the wall" feel
        _origGravityScale = _rb.gravityScale;
        _rb.gravityScale  = 0f;

        // Spawn dust/effect particles at feet if a child ParticleSystem exists named "ClimbFX"
        Transform fx = transform.Find("ClimbFX");
        if (fx != null) fx.gameObject.SetActive(true);

        // Slow vertical creep up the wall (handled in FixedUpdate via _isClimbing branch)
        yield return new WaitForSeconds(climbDuration);

        // Hop OVER the top of the wall
        _rb.linearVelocity = new Vector2(dir * moveSpeed * 0.9f, climbHopForce);
        _rb.gravityScale   = _origGravityScale;

        yield return new WaitForSeconds(0.4f);

        if (fx != null) fx.gameObject.SetActive(false);

        _isClimbing = false;
        if (_hasIsClimbing) _anim?.SetBool("isClimbing", false);
    }

    // ── Chase / Wander ────────────────────────────────────────────────────────
    void DecideChase(float dist)
    {
        GameManager.Instance?.NotifyCombatStarted();
        _isPaused = false;

        if (dist <= attackRange)
        {
            _targetVelocityX = 0f;
            TryAttack();
        }
        else
        {
            float dir = Mathf.Sign(_playerTransform.position.x - transform.position.x);
            float aggressionBonus = Mathf.Min(_consecutiveHits * 0.15f, 0.6f);
            _targetVelocityX = dir * (chaseSpeed + aggressionBonus);
        }
    }

    void TryAttack()
    {
        if (_attackTimer > 0f || _playerHealth == null || _playerHealth.IsDead) return;

        _playerHealth.TakeDamage(attackDamage);

        Rigidbody2D playerRb = _playerTransform.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            Vector2 dir = (_playerTransform.position - transform.position).normalized;
            playerRb.AddForce(dir * attackKnockback, ForceMode2D.Impulse);
        }

        _attackTimer = attackCooldown;

        // Safe trigger — only fires if parameter exists
        if (_hasIsAttacking) _anim?.SetTrigger("isAttacking");
    }

    void DecideWander()
    {
        if (_isPaused)
        {
            _pauseTimer -= Time.deltaTime;
            _targetVelocityX = 0f;
            if (_pauseTimer <= 0f) { _isPaused = false; PickNewWanderDir(); }
            return;
        }

        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f)
        {
            if (Random.value < pauseChance) { _isPaused = true; _pauseTimer = pauseDuration; }
            else                            PickNewWanderDir();
            _targetVelocityX = 0f;
            return;
        }

        _targetVelocityX = _wanderDir.x * moveSpeed;
    }

    void PickNewWanderDir()
    {
        _wanderDir   = new Vector2(Random.value > 0.5f ? 1f : -1f, 0f);
        _wanderTimer = wanderInterval;
    }

    // ── Public ────────────────────────────────────────────────────────────────
    public void ForceChase()
    {
        _forcedChase = true;
    }

    public void Knockback(Vector2 force)
    {
        _consecutiveHits++;
        _hitComboResetTimer = HitComboWindow;

        float comboMultiplier = 1f + (_consecutiveHits - 1) * 0.2f;
        Vector2 scaledForce   = force * comboMultiplier;

        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(scaledForce, ForceMode2D.Impulse);
        _knockbackTimer  = knockbackDuration;
        _stunTimer       = knockbackDuration + knockbackStunAdditional;
        _targetVelocityX = 0f;

        _isClimbing = false;
        if (_hasIsClimbing) _anim?.SetBool("isClimbing", false);
    }

    void OnDeath()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnEnemyDefeated();
        _isDead          = true;
        _targetVelocityX = 0f;
        _isClimbing      = false;

        // Safe trigger
        if (_anim != null)
        {
            bool hasDead = false;
            foreach (var p in _anim.parameters)
                if (p.name == "isDead") { hasDead = true; break; }
            if (hasDead) _anim.SetTrigger("isDead");
        }

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
