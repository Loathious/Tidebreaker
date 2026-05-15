using System.Collections;
using UnityEngine;

/// <summary>
/// Spider enemy for the Cave level.
/// Hangs on the ceiling until the player gets close, then drops, lands, and
/// chases. From spelmanus: 30 HP, 15 damage on contact.
/// Stand-alone — does NOT depend on GameManager (which only exists in Village).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class SpiderAI : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float startHealth   = 30f;
    [SerializeField] private float moveSpeed     = 3f;
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private float attackRange    = 1.5f;
    [SerializeField] private int   attackDamage   = 15;
    [SerializeField] private float attackCooldown = 1.1f;

    [Header("Drop")]
    [SerializeField] private bool  startsOnCeiling = true;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite spriteWalk1;
    [SerializeField] private Sprite spriteWalk2;
    [SerializeField] private float walkFrameTime = 0.18f;

    private Rigidbody2D  _rb;
    private Health       _health;
    private Transform    _player;
    private float        _attackTimer;
    private float        _walkTimer;
    private bool         _useWalk1;
    private bool         _dropped;
    private bool         _isDead;
    private Color        _origColor = Color.white;

    void Awake()
    {
        _rb     = GetComponent<Rigidbody2D>();
        _health = GetComponent<Health>();
        _health.SetMaxHealth(startHealth);

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();

        // Auto-load sprite frames from Resources if not assigned
        if (spriteWalk1 == null) spriteWalk1 = LoadSprite("spideranimation1");
        if (spriteWalk2 == null) spriteWalk2 = LoadSprite("spideranimation2");
    }

    void Start()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) _player = playerGO.transform;

        _health.OnDeath.AddListener(OnDeath);
        _health.OnDamageTaken.AddListener(_ => StartCoroutine(HitFlash()));

        if (spriteRenderer != null)
        {
            if (spriteWalk1 != null) spriteRenderer.sprite = spriteWalk1;
            _origColor = spriteRenderer.color;
        }

        if (startsOnCeiling)
        {
            _rb.gravityScale = 0f;
            _rb.constraints  = RigidbodyConstraints2D.FreezeAll;
        }
    }

    void Update()
    {
        if (_isDead || _player == null) return;

        float dist = Vector2.Distance(transform.position, _player.position);

        // --- Hanging phase ---
        if (!_dropped && startsOnCeiling)
        {
            if (dist < detectionRange) Drop();
            return;
        }

        // Tick down the cooldown; damage fires in OnCollisionStay2D
        if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;

        // Walk animation (purely visual — velocity set in FixedUpdate)
        if (dist > attackRange)
        {
            _walkTimer += Time.deltaTime;
            if (_walkTimer >= walkFrameTime && spriteRenderer != null)
            {
                _walkTimer = 0f;
                _useWalk1  = !_useWalk1;
                Sprite frame = _useWalk1 ? spriteWalk1 : spriteWalk2;
                if (frame != null) spriteRenderer.sprite = frame;
            }
        }
    }

    void FixedUpdate()
    {
        if (_isDead || _player == null || !_dropped) return;

        float dist = Vector2.Distance(transform.position, _player.position);

        if (dist > attackRange)
        {
            float dir = _player.position.x > transform.position.x ? 1f : -1f;
            // Clamp Y to ≤ 0 so setting velocity never fights the floor's contact normal
            float vy = Mathf.Min(_rb.linearVelocity.y, 0f);
            _rb.linearVelocity = new Vector2(dir * moveSpeed, vy);
            if (spriteRenderer != null) spriteRenderer.flipX = dir < 0f;
        }
        else
        {
            // Stop horizontal movement when in attack range; let gravity handle Y
            _rb.linearVelocity = new Vector2(0f, Mathf.Min(_rb.linearVelocity.y, 0f));
        }
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (_isDead || !_dropped) return;
        if (!col.gameObject.CompareTag("Player")) return;
        if (_attackTimer <= 0f)
        {
            _attackTimer = attackCooldown;
            Attack();
        }
    }

    private void Drop()
    {
        _dropped = true;
        _rb.gravityScale            = 1.2f;                                   // gentle fall — prevents tunnelling
        _rb.constraints             = RigidbodyConstraints2D.FreezeRotation;
        _rb.collisionDetectionMode  = CollisionDetectionMode2D.Continuous;    // no tunnelling through thin floors
        _rb.linearVelocity          = Vector2.zero;
        if (spriteRenderer != null && spriteWalk1 != null)
            spriteRenderer.sprite = spriteWalk1;
    }

    private void Attack()
    {
        if (_player == null) return;
        Health ph = _player.GetComponent<Health>() ?? _player.GetComponentInChildren<Health>();
        if (ph != null && !ph.IsDead)
            ph.TakeDamage(attackDamage);
    }

    private void OnDeath()
    {
        if (_isDead) return;
        _isDead = true;

        _rb.linearVelocity = Vector2.zero;
        _rb.gravityScale   = 0f;

        // Cave doesn't track enemy count for objective — just notify if a manager is present
        GameManager.Instance?.OnEnemyDefeated();

        StartCoroutine(DeathFlash());
    }

    private IEnumerator HitFlash()
    {
        if (spriteRenderer == null) yield break;
        spriteRenderer.color = new Color(1f, 0.3f, 0.3f);
        yield return new WaitForSeconds(0.08f);
        spriteRenderer.color = _origColor;
    }

    private IEnumerator DeathFlash()
    {
        if (spriteRenderer != null)
        {
            for (int i = 0; i < 4; i++)
            {
                spriteRenderer.color = Color.white;
                yield return new WaitForSeconds(0.06f);
                spriteRenderer.color = new Color(0.6f, 0f, 0f);
                yield return new WaitForSeconds(0.06f);
            }
        }
        Destroy(gameObject);
    }

    private static Sprite LoadSprite(string name)
    {
        Sprite s = Resources.Load<Sprite>("Spider/" + name)
               ?? Resources.Load<Sprite>(name);
        if (s != null) return s;
        foreach (Sprite found in Resources.FindObjectsOfTypeAll<Sprite>())
            if (found != null && found.name.StartsWith(name)) return found;
        return null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
