using System.Collections;
using UnityEngine;

/// <summary>
/// Vine Snake — fast jungle enemy (Level 3).
/// From spelmanus: 40 HP, very fast movement, bite attack.
/// Slithers along the ground, detects the player from far away and lunges in
/// to bite. Stand-alone: works via LevelManagerBase, no scene wiring needed.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class VineSnakeAI : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float startHealth   = 40f;
    [SerializeField] private float moveSpeed     = 3f;
    [SerializeField] private float chaseSpeed    = 6.5f;   // very fast
    [SerializeField] private float detectionRange = 11f;
    [SerializeField] private float attackRange   = 1.4f;
    [SerializeField] private int   attackDamage  = 12;
    [SerializeField] private float attackCooldown = 1.1f;
    [SerializeField] private float lungeForce    = 7f;

    [Header("Audio")]
    public AudioClip ambientClip;
    public AudioClip attackClip;
    public AudioClip hurtClip;

    private Rigidbody2D    _rb;
    private Health         _health;
    private SpriteAnimator _anim;
    private SpriteRenderer _sr;
    private Transform      _player;
    private Health         _playerHealth;

    private float _attackTimer;
    private float _wanderTimer;
    private float _ambientTimer;
    private float _wanderDir = 1f;
    private bool  _isDead;
    private Color _origColor = Color.white;

    void Awake()
    {
        _rb     = GetComponent<Rigidbody2D>();
        _health = GetComponent<Health>();
        _anim   = GetComponent<SpriteAnimator>();
        _sr     = GetComponent<SpriteRenderer>();
        _health.SetMaxHealth(startHealth);

        _rb.gravityScale = 3f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { _player = p.transform; _playerHealth = p.GetComponent<Health>(); }

        if (_sr != null) _origColor = _sr.color;
        _health.OnDeath.AddListener(OnDeath);
        _health.OnDamageTaken.AddListener(_ => OnHurt());
        _ambientTimer = Random.Range(2f, 6f);
        _anim?.Play("move");
    }

    void Update()
    {
        if (_isDead || _player == null) return;

        _attackTimer  -= Time.deltaTime;
        _ambientTimer -= Time.deltaTime;
        if (_ambientTimer <= 0f)
        {
            _ambientTimer = Random.Range(4f, 9f);
            if (ambientClip != null)
                SettingsManager.PlaySfxAt(ambientClip, transform.position, 0.5f);
        }
    }

    void FixedUpdate()
    {
        if (_isDead || _player == null) return;

        float dist = Vector2.Distance(transform.position, _player.position);
        float dir  = Mathf.Sign(_player.position.x - transform.position.x);

        if (dist <= detectionRange)
        {
            LevelManagerBase.Current?.NotifyCombatStarted();

            if (dist <= attackRange)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                TryBite(dir);
            }
            else
            {
                _rb.linearVelocity = new Vector2(dir * chaseSpeed, _rb.linearVelocity.y);
            }
        }
        else
        {
            // Idle wander
            _wanderTimer -= Time.fixedDeltaTime;
            if (_wanderTimer <= 0f)
            {
                _wanderTimer = Random.Range(1.5f, 3.5f);
                _wanderDir   = Random.value > 0.5f ? 1f : -1f;
            }
            _rb.linearVelocity = new Vector2(_wanderDir * moveSpeed, _rb.linearVelocity.y);
            dir = _wanderDir;
        }

        if (_sr != null && Mathf.Abs(_rb.linearVelocity.x) > 0.05f)
            _sr.flipX = _rb.linearVelocity.x < 0f;
    }

    private void TryBite(float dir)
    {
        if (_attackTimer > 0f || _playerHealth == null || _playerHealth.IsDead) return;
        _attackTimer = attackCooldown;

        _playerHealth.TakeDamage(attackDamage);
        _rb.AddForce(new Vector2(dir * lungeForce, 2f), ForceMode2D.Impulse);

        if (attackClip != null) SettingsManager.PlaySfxAt(attackClip, transform.position, 0.8f);
        _anim?.Play("attack", true);
        StartCoroutine(ResumeMoveAfter(0.3f));
    }

    private IEnumerator ResumeMoveAfter(float t)
    {
        yield return new WaitForSeconds(t);
        if (!_isDead) _anim?.Play("move");
    }

    private void OnHurt()
    {
        if (_isDead) return;
        if (hurtClip != null) SettingsManager.PlaySfxAt(hurtClip, transform.position, 0.7f);
        StartCoroutine(HitFlash());
    }

    private IEnumerator HitFlash()
    {
        if (_sr == null) yield break;
        _sr.color = new Color(1f, 0.4f, 0.4f);
        yield return new WaitForSeconds(0.09f);
        if (!_isDead && _sr != null) _sr.color = _origColor;
    }

    private void OnDeath()
    {
        if (_isDead) return;
        _isDead = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType       = RigidbodyType2D.Kinematic;   // ignore further impulses (no "flung upward")
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;
        LevelManagerBase.Current?.OnEnemyDefeated();
        StartCoroutine(DeathFade());
    }

    private IEnumerator DeathFade()
    {
        float t = 0f;
        Vector3 baseScale = transform.localScale;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            if (_sr != null)
                _sr.color = new Color(_origColor.r, _origColor.g, _origColor.b, 1f - t / 0.6f);
            transform.localScale = baseScale * (1f - 0.3f * (t / 0.6f));
            transform.Rotate(0f, 0f, 240f * Time.deltaTime);
            yield return null;
        }
        Destroy(gameObject);
    }
}
