using System.Collections;
using UnityEngine;

/// <summary>
/// Monkey — jungle enemy (Level 3).
/// Walks smoothly within a patrol range centred on its spawn position.
/// When the player enters detection range the monkey closes in to preferred
/// distance, backs off if too close, and lobs coconuts.
/// Uses no random-hop velocity blasts — all movement is a continuous
/// horizontal velocity so there are no teleport-like jumps.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class MonkeyAI : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float startHealth    = 50f;
    [SerializeField] private float moveSpeed      = 2.8f;
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private float patrolRange    = 8f;    // max X-distance from spawn the monkey may roam
    [SerializeField] private float preferredDist  = 4.5f;  // tries to stay this far from the player
    [SerializeField] private float throwRange     = 10f;   // throw coconuts when player is within this range
    [SerializeField] private float throwCooldown  = 2.4f;
    [SerializeField] private int   coconutDamage  = 10;
    [SerializeField] private int   contactDamage  = 8;
    [SerializeField] private float coconutSpeed   = 9f;
    [SerializeField] private float contactCooldown = 0.6f;  // seconds between contact-damage ticks

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

    private float   _throwTimer;
    private float   _contactTimer;
    private bool    _isDead;
    private Color   _origColor = Color.white;
    private Sprite  _coconut;
    private Vector3 _spawnPos;
    private bool    _combatNotified;

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
        _spawnPos = transform.position;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { _player = p.transform; _playerHealth = p.GetComponent<Health>(); }

        if (_sr != null) _origColor = _sr.color;
        _health.OnDeath.AddListener(OnDeath);
        _health.OnDamageTaken.AddListener(_ => OnHurt());

        _coconut    = ProceduralSprite.Circle(7, new Color(0.45f, 0.27f, 0.12f));
        _throwTimer = Random.Range(0.3f, throwCooldown);
        _anim?.Play("idle");
    }

    void Update()
    {
        if (_isDead) return;
        if (_throwTimer   > 0f) _throwTimer   -= Time.deltaTime;
        if (_contactTimer > 0f) _contactTimer  -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (_isDead || _player == null) return;

        float cx          = transform.position.x;
        float distToSpawn = Mathf.Abs(cx - _spawnPos.x);
        float distToPlayer = Vector2.Distance(transform.position, _player.position);
        float dirToPlayer  = Mathf.Sign(_player.position.x - cx);
        float dirToSpawn   = Mathf.Sign(_spawnPos.x - cx);

        // ── Out of patrol range → walk back toward spawn, ignore player ────────
        if (distToSpawn > patrolRange)
        {
            float vx = (dirToSpawn == 0f ? 1f : dirToSpawn) * moveSpeed;
            ApplyVelocity(vx);
            return;
        }

        // ── Outside detection range → idle / drift back toward spawn ─────────
        if (distToPlayer > detectionRange)
        {
            float vx = distToSpawn > 1f ? dirToSpawn * moveSpeed * 0.5f : 0f;
            ApplyVelocity(vx);
            return;
        }

        // ── Player in range — notify combat once ──────────────────────────────
        if (!_combatNotified)
        {
            _combatNotified = true;
            LevelManagerBase.Current?.NotifyCombatStarted();
        }

        // ── Throw attack ──────────────────────────────────────────────────────
        if (_throwTimer <= 0f && distToPlayer <= throwRange)
        {
            _throwTimer = throwCooldown + Random.Range(-0.3f, 0.3f);
            ThrowCoconut();
        }

        // ── Horizontal movement ───────────────────────────────────────────────
        float targetVX;

        bool wouldLeaveLeft  = cx - moveSpeed * Time.fixedDeltaTime < _spawnPos.x - patrolRange;
        bool wouldLeaveRight = cx + moveSpeed * Time.fixedDeltaTime > _spawnPos.x + patrolRange;
        bool atEdge = (dirToPlayer < 0f && wouldLeaveLeft) ||
                      (dirToPlayer > 0f && wouldLeaveRight);

        if (distToPlayer > preferredDist + 0.8f && !atEdge)
        {
            // Chase player (staying inside patrol bounds)
            targetVX = dirToPlayer * moveSpeed;
        }
        else if (distToPlayer < preferredDist - 0.8f)
        {
            // Back off from player
            float backDir = -dirToPlayer;
            bool wouldLeaveB = (backDir < 0f && wouldLeaveLeft) || (backDir > 0f && wouldLeaveRight);
            targetVX = wouldLeaveB ? 0f : backDir * moveSpeed * 0.7f;
        }
        else
        {
            // Stand ground at preferred distance
            targetVX = 0f;
        }

        ApplyVelocity(targetVX);
    }

    private void ApplyVelocity(float vx)
    {
        _rb.linearVelocity = new Vector2(vx, _rb.linearVelocity.y);
        bool moving = Mathf.Abs(vx) > 0.1f;
        if (_sr != null && moving) _sr.flipX = vx < 0f;
        _anim?.Play(moving ? "move" : "idle");
    }

    private void ThrowCoconut()
    {
        if (_player == null) return;
        _anim?.Play("attack", true);
        if (attackClip != null) AudioSource.PlayClipAtPoint(attackClip, transform.position, 0.8f);

        Vector3 origin   = transform.position + Vector3.up * 0.3f;
        Vector2 toPlayer = ((Vector2)(_player.position + Vector3.up * 0.4f) - (Vector2)origin).normalized;
        Vector2 aim      = (toPlayer + Vector2.up * 0.35f).normalized;
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
        if (_contactTimer > 0f) return;
        if (_playerHealth != null && !_playerHealth.IsDead && !_playerHealth.IsInvincible)
        {
            _playerHealth.TakeDamage(contactDamage);
            _contactTimer = contactCooldown;
        }
    }

    private void OnHurt()
    {
        if (_isDead) return;
        if (hurtClip != null) AudioSource.PlayClipAtPoint(hurtClip, transform.position, 0.7f);
        SpawnBlood();
        StartCoroutine(HitFlash());
    }

    private void SpawnBlood()
    {
        var go = new GameObject("BloodFX");
        go.transform.position = transform.position;
        go.AddComponent<ParticleSystem>();
        go.AddComponent<BloodParticleSetup>();
        go.GetComponent<ParticleSystem>().Emit(8);
        Destroy(go, 2f);
    }

    private IEnumerator HitFlash()
    {
        if (_sr == null) yield break;
        _sr.color = new Color(1f, 0.25f, 0.25f);
        yield return new WaitForSeconds(0.1f);
        if (!_isDead && _sr != null) _sr.color = _origColor;
    }

    private void OnDeath()
    {
        if (_isDead) return;
        _isDead = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.gravityScale   = 3.5f;
        _rb.bodyType       = RigidbodyType2D.Kinematic;
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
