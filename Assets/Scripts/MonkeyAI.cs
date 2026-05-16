using System.Collections;
using UnityEngine;

/// <summary>
/// Monkey — jungle enemy (Level 3).
/// From spelmanus: 50 HP, hops around, throw attack (coconuts).
/// Keeps its distance from the player, hops left/right and lobs coconuts.
/// Stand-alone — reports to LevelManagerBase.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class MonkeyAI : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float startHealth    = 50f;
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private float preferredRange = 5.5f;   // tries to stay this far away
    [SerializeField] private float hopForce       = 7f;
    [SerializeField] private float hopSpeed       = 3.2f;
    [SerializeField] private float throwCooldown  = 2.2f;
    [SerializeField] private int   coconutDamage  = 10;
    [SerializeField] private int   contactDamage  = 8;
    [SerializeField] private float coconutSpeed   = 9f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDist = 0.6f;

    [Header("Audio")]
    public AudioClip attackClip;
    public AudioClip hurtClip;
    public AudioClip deathClip;

    private Rigidbody2D    _rb;
    private Health         _health;
    private SpriteAnimator _anim;
    private SpriteRenderer _sr;
    private Transform      _player;
    private Health         _playerHealth;

    private float _throwTimer;
    private float _hopTimer;
    private bool  _isDead;
    private Color _origColor = Color.white;
    private Sprite _coconut;

    void Awake()
    {
        _rb     = GetComponent<Rigidbody2D>();
        _health = GetComponent<Health>();
        _anim   = GetComponent<SpriteAnimator>();
        _sr     = GetComponent<SpriteRenderer>();
        _health.SetMaxHealth(startHealth);

        _rb.gravityScale   = 3.5f;
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

        _coconut    = ProceduralSprite.Circle(7, new Color(0.45f, 0.27f, 0.12f));
        _throwTimer = Random.Range(0.5f, throwCooldown);
        _anim?.Play("idle");
    }

    void Update()
    {
        if (_isDead) return;
        _throwTimer -= Time.deltaTime;
        _hopTimer   -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (_isDead || _player == null) return;

        float dist = Vector2.Distance(transform.position, _player.position);
        float dir  = Mathf.Sign(_player.position.x - transform.position.x);
        if (dir == 0f) dir = 1f;

        if (dist > detectionRange)
        {
            _anim?.Play("idle");
            return;
        }

        LevelManagerBase.Current?.NotifyCombatStarted();
        if (_sr != null) _sr.flipX = dir < 0f;

        bool grounded = IsGrounded();

        // Throw coconuts when roughly at preferred range
        if (_throwTimer <= 0f && grounded)
        {
            _throwTimer = throwCooldown;
            ThrowCoconut(dir);
        }

        // Hop to maintain distance
        if (_hopTimer <= 0f && grounded)
        {
            _hopTimer = Random.Range(0.7f, 1.4f);
            float moveDir = dist < preferredRange ? -dir : dir;   // back off if too close
            _rb.linearVelocity = new Vector2(moveDir * hopSpeed, hopForce);
            _anim?.Play("idle");
        }
    }

    private bool IsGrounded()
    {
        return Physics2D.Raycast(transform.position, Vector2.down, groundCheckDist,
                                 LayerMask.GetMask("Ground")) ||
               Mathf.Abs(_rb.linearVelocity.y) < 0.05f;
    }

    private void ThrowCoconut(float dir)
    {
        if (_player == null) return;
        _anim?.Play("attack", true);
        if (attackClip != null) AudioSource.PlayClipAtPoint(attackClip, transform.position, 0.8f);

        Vector3 origin = transform.position + Vector3.up * 0.3f;
        Vector2 toPlayer = (_player.position + Vector3.up * 0.4f - origin);
        // Lob slightly upward
        Vector2 aim = (toPlayer.normalized + Vector2.up * 0.35f).normalized;
        Projectile.Spawn(origin, aim, coconutSpeed, coconutDamage, true, _coconut, 6f, 0.16f, 40);

        StartCoroutine(ResumeIdle(0.35f));
    }

    private IEnumerator ResumeIdle(float t)
    {
        yield return new WaitForSeconds(t);
        if (!_isDead) _anim?.Play("idle");
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (_isDead || !col.gameObject.CompareTag("Player")) return;
        if (_playerHealth != null && !_playerHealth.IsDead && !_playerHealth.IsInvincible)
            _playerHealth.TakeDamage(contactDamage);
    }

    private void OnHurt()
    {
        if (_isDead) return;
        if (hurtClip != null) AudioSource.PlayClipAtPoint(hurtClip, transform.position, 0.7f);
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
        _rb.gravityScale   = 0.5f;
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;
        if (deathClip != null) AudioSource.PlayClipAtPoint(deathClip, transform.position, 0.9f);
        LevelManagerBase.Current?.OnEnemyDefeated();
        StartCoroutine(DeathFade());
    }

    private IEnumerator DeathFade()
    {
        _anim?.Play("hurt");
        float t = 0f;
        while (t < 0.7f)
        {
            t += Time.deltaTime;
            if (_sr != null)
                _sr.color = new Color(_origColor.r, _origColor.g, _origColor.b, 1f - t / 0.7f);
            transform.Rotate(0f, 0f, 200f * Time.deltaTime);
            yield return null;
        }
        Destroy(gameObject);
    }
}
