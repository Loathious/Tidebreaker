using System.Collections;
using UnityEngine;

/// <summary>
/// Sandworm — desert enemy (Level 4).
/// From spelmanus: the large Sandworm guarding Obelisk 1 has 150 HP; smaller
/// ones are used for the enemy wave at Obelisk 3. Crawls across the sand toward
/// the player and lunges in to attack. Animated with the 12 Sandmask frames.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class SandwormAI : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float startHealth    = 150f;
    [SerializeField] private float moveSpeed      = 1.6f;
    [SerializeField] private float chaseSpeed     = 3.2f;
    [SerializeField] private float detectionRange = 13f;
    [SerializeField] private float attackRange    = 1.7f;
    [SerializeField] private int   attackDamage   = 20;
    [SerializeField] private float attackCooldown = 1.6f;
    [SerializeField] private float lungeForce     = 6.5f;

    [Tooltip("Counts toward the level objective when it dies (wave worms only).")]
    public bool countsAsWaveEnemy = false;

    [Header("Audio")]
    public AudioClip moveClip;
    public AudioClip attackClip;
    public AudioClip hurtClip;

    private Rigidbody2D    _rb;
    private Health         _health;
    private SpriteAnimator _anim;
    private SpriteRenderer _sr;
    private Transform      _player;
    private Health         _playerHealth;

    private float _attackTimer;
    private float _moveSoundTimer;
    private float _wanderDir = 1f;
    private float _wanderTimer;
    private bool  _isDead;
    private Color _origColor = Color.white;

    /// <summary>Set health from the spawner before Start runs.</summary>
    public void Configure(float hp, bool waveEnemy)
    {
        startHealth = hp;
        countsAsWaveEnemy = waveEnemy;
    }

    void Awake()
    {
        _rb     = GetComponent<Rigidbody2D>();
        _health = GetComponent<Health>();
        _anim   = GetComponent<SpriteAnimator>();
        _sr     = GetComponent<SpriteRenderer>();

        _rb.gravityScale   = 3f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Start()
    {
        _health.SetMaxHealth(startHealth);

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { _player = p.transform; _playerHealth = p.GetComponent<Health>(); }

        if (_sr != null) _origColor = _sr.color;
        _health.OnDeath.AddListener(OnDeath);
        _health.OnDamageTaken.AddListener(_ => OnHurt());
        _anim?.Play("move");
    }

    void Update()
    {
        if (_isDead) return;
        _attackTimer    -= Time.deltaTime;
        _moveSoundTimer -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (_isDead || _player == null) return;

        float dist = Vector2.Distance(transform.position, _player.position);
        float dir  = Mathf.Sign(_player.position.x - transform.position.x);
        if (dir == 0f) dir = 1f;

        if (dist <= detectionRange)
        {
            LevelManagerBase.Current?.NotifyCombatStarted();

            if (dist <= attackRange)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                TryAttack(dir);
            }
            else
            {
                _rb.linearVelocity = new Vector2(dir * chaseSpeed, _rb.linearVelocity.y);
                PlayMoveSound();
            }
        }
        else
        {
            _wanderTimer -= Time.fixedDeltaTime;
            if (_wanderTimer <= 0f)
            {
                _wanderTimer = Random.Range(2f, 4f);
                _wanderDir   = Random.value > 0.5f ? 1f : -1f;
            }
            _rb.linearVelocity = new Vector2(_wanderDir * moveSpeed, _rb.linearVelocity.y);
            dir = _wanderDir;
        }

        if (_sr != null && Mathf.Abs(_rb.linearVelocity.x) > 0.05f)
            _sr.flipX = _rb.linearVelocity.x < 0f;
    }

    private void PlayMoveSound()
    {
        if (moveClip == null || _moveSoundTimer > 0f) return;
        _moveSoundTimer = 1.4f;
        AudioSource.PlayClipAtPoint(moveClip, transform.position, 0.4f);
    }

    private void TryAttack(float dir)
    {
        if (_attackTimer > 0f || _playerHealth == null || _playerHealth.IsDead) return;
        _attackTimer = attackCooldown;

        _playerHealth.TakeDamage(attackDamage);
        _rb.AddForce(new Vector2(dir * lungeForce, 3f), ForceMode2D.Impulse);
        if (attackClip != null) AudioSource.PlayClipAtPoint(attackClip, transform.position, 0.85f);
        if (_anim != null && _anim.HasClip("attack")) _anim.Play("attack", true);
    }

    private void OnHurt()
    {
        if (_isDead) return;
        if (hurtClip != null) AudioSource.PlayClipAtPoint(hurtClip, transform.position, 0.6f);
        StartCoroutine(HitFlash());
    }

    private IEnumerator HitFlash()
    {
        if (_sr == null) yield break;
        _sr.color = new Color(1f, 0.45f, 0.45f);
        yield return new WaitForSeconds(0.09f);
        if (!_isDead && _sr != null) _sr.color = _origColor;
    }

    private void OnDeath()
    {
        if (_isDead) return;
        _isDead = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.gravityScale   = 0f;
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;

        if (countsAsWaveEnemy) LevelManagerBase.Current?.OnEnemyDefeated();

        // Notify the desert manager so Obelisk 1 (the big sandworm) can unlock.
        var desert = LevelManagerBase.Current as DesertManager;
        desert?.OnSandwormKilled(this);

        StartCoroutine(DeathFade());
    }

    private IEnumerator DeathFade()
    {
        float t = 0f;
        Vector3 baseScale = transform.localScale;
        while (t < 0.7f)
        {
            t += Time.deltaTime;
            if (_sr != null)
                _sr.color = new Color(_origColor.r, _origColor.g, _origColor.b, 1f - t / 0.7f);
            transform.localScale = baseScale * (1f - 0.4f * (t / 0.7f));
            yield return null;
        }
        Destroy(gameObject);
    }
}
