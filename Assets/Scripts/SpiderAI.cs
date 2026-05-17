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
    private Collider2D   _col;
    private Health       _health;
    private Transform    _player;
    private float        _attackTimer;
    private float        _walkTimer;
    private bool         _useWalk1;
    private bool         _dropped;
    private bool         _isDead;
    private bool         _knockedBack;
    private float        _knockbackTimer;
    private Color        _origColor = Color.white;

    void Awake()
    {
        _rb     = GetComponent<Rigidbody2D>();
        _col    = GetComponent<Collider2D>();
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
        _health.OnDamageTaken.AddListener(_ => { StartCoroutine(HitFlash()); SpawnBlood(); });

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
        else
        {
            _rb.gravityScale           = 3f;
            _rb.constraints            = RigidbodyConstraints2D.FreezeRotation;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _dropped = true;
        }
    }

    void Update()
    {
        if (_isDead || _player == null || LevelManagerBase.MonstersFrozen) return;

        if (_knockedBack)
        {
            _knockbackTimer -= Time.deltaTime;
            if (_knockbackTimer <= 0f) _knockedBack = false;
            return;
        }

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
        if (_knockedBack) return;

        float dist = Vector2.Distance(transform.position, _player.position);

        if (dist > attackRange)
        {
            float dir = _player.position.x > transform.position.x ? 1f : -1f;
            _rb.linearVelocity = new Vector2(dir * moveSpeed, _rb.linearVelocity.y);
            if (spriteRenderer != null) spriteRenderer.flipX = dir < 0f;
        }
        else
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
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
        _rb.gravityScale           = 3f;
        _rb.constraints            = RigidbodyConstraints2D.FreezeRotation;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.linearVelocity         = Vector2.zero;
        if (spriteRenderer != null && spriteWalk1 != null)
            spriteRenderer.sprite = spriteWalk1;
        StartCoroutine(SnapToFloor());
    }

    private IEnumerator SnapToFloor()
    {
        // Wait a few frames so physics can settle, then hard-snap to floor if embedded.
        for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();

        if (_isDead) yield break;

        // Raycast downward from the collider center to find the floor
        Vector2 origin = _col != null ? (Vector2)_col.bounds.center : (Vector2)transform.position;
        float halfH    = _col != null ? _col.bounds.extents.y : 0.5f;

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, halfH + 1.5f, ~LayerMask.GetMask("Enemy"));
        if (hit.collider != null && !hit.collider.isTrigger &&
            hit.collider.gameObject != gameObject)
        {
            // Snap so the bottom of the collider sits exactly on the hit surface
            float correctedY = hit.point.y + halfH + (_col != null ? _col.offset.y : 0f);
            if (correctedY > transform.position.y - 0.05f)  // only snap UP (never sink further)
            {
                Vector3 pos = transform.position;
                pos.y = correctedY;
                transform.position = pos;
                _rb.position = new Vector2(pos.x, pos.y);
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
            }
        }
    }

    /// <summary>Called by PlayerCombat to apply hit-knockback.</summary>
    public void Knockback(Vector2 force)
    {
        if (_isDead) return;
        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(force, ForceMode2D.Impulse);
        _knockedBack    = true;
        _knockbackTimer = 0.35f;
    }

    private void SpawnBlood()
    {
        if (_isDead) return;
        Vector3 pos = _col != null ? (Vector3)_col.bounds.center : transform.position;
        var go = new GameObject("BloodFX");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        go.AddComponent<BloodParticleSetup>();
        ps.Emit(7);
        Destroy(go, 2f);
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
        if (_col != null) _col.enabled = false;

        // Notify whichever manager is active (works in Cave, Jungle, etc.)
        LevelManagerBase.Current?.OnEnemyDefeated();
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
