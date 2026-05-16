using System.Collections;
using UnityEngine;

/// <summary>
/// Jungle Guardian — Level 3 mini-boss (the monkey mini-boss).
/// From spelmanus: 200 HP, two phases.
///  • Phase 1 (100–50%): ground slam + jump attack.
///  • Phase 2 (below 50%): faster and adds ranged throw attacks.
/// When defeated it tells the JungleManager to open the temple door.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class JungleGuardian : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float maxHealth      = 200f;
    [SerializeField] private float moveSpeed      = 2.4f;
    [SerializeField] private float phase2Speed    = 3.6f;
    [SerializeField] private float detectionRange = 16f;
    [SerializeField] private float meleeRange     = 2.4f;
    [SerializeField] private int   slamDamage     = 22;
    [SerializeField] private int   jumpDamage     = 18;
    [SerializeField] private int   throwDamage    = 14;

    [Header("Timing")]
    [SerializeField] private float actionCooldown   = 2.2f;
    [SerializeField] private float phase2Cooldown   = 1.4f;

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
    private BossHealthBar  _bar;

    private bool  _active;        // becomes true once the fight starts
    private bool  _busy;          // performing an action
    private bool  _isDead;
    private bool  _inPhase2;
    private float _actionTimer;
    private Color _origColor = Color.white;
    private Sprite _projSprite;
    private Vector3 _baseScale;

    void Awake()
    {
        _rb     = GetComponent<Rigidbody2D>();
        _health = GetComponent<Health>();
        _anim   = GetComponent<SpriteAnimator>();
        _sr     = GetComponent<SpriteRenderer>();

        _rb.gravityScale   = 4f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Start()
    {
        _health.SetMaxHealth(maxHealth);
        _baseScale = transform.localScale;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { _player = p.transform; _playerHealth = p.GetComponent<Health>(); }

        if (_sr != null) _origColor = _sr.color;
        _health.OnDeath.AddListener(OnDeath);
        _health.OnDamageTaken.AddListener(OnHurt);

        _projSprite  = ProceduralSprite.Circle(8, new Color(0.4f, 0.25f, 0.1f));
        _actionTimer = 1.5f;
        _anim?.Play("idle");
    }

    void Update()
    {
        if (_isDead || _player == null) return;

        if (!_active)
        {
            if (Vector2.Distance(transform.position, _player.position) <= detectionRange)
                BeginFight();
            return;
        }

        if (_busy) return;
        _actionTimer -= Time.deltaTime;
        if (_actionTimer <= 0f)
            StartCoroutine(DoAction());
    }

    void FixedUpdate()
    {
        if (_isDead || !_active || _busy || _player == null) return;

        // Walk toward the player between actions
        float dir  = Mathf.Sign(_player.position.x - transform.position.x);
        float dist = Mathf.Abs(_player.position.x - transform.position.x);
        float spd  = _inPhase2 ? phase2Speed : moveSpeed;
        float vx   = dist > meleeRange * 0.8f ? dir * spd : 0f;
        _rb.linearVelocity = new Vector2(vx, _rb.linearVelocity.y);
        if (_sr != null && dir != 0f) _sr.flipX = dir < 0f;
        _anim?.Play(Mathf.Abs(vx) > 0.1f ? "move" : "idle");
    }

    private void BeginFight()
    {
        _active = true;
        LevelManagerBase.Current?.NotifyCombatStarted();
        _bar = BossHealthBar.Create("JUNGLE GUARDIAN");
        _bar?.SetHealth(1f);
        _bar?.SetPhase("PHASE 1");
    }

    private IEnumerator DoAction()
    {
        _busy = true;
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

        float dist = _player != null
            ? Vector2.Distance(transform.position, _player.position) : 99f;

        int choice;
        if (_inPhase2) choice = Random.Range(0, 3);          // slam / jump / throw
        else           choice = dist < meleeRange ? 0 : 1;   // slam if close else jump

        switch (choice)
        {
            case 0:  yield return GroundSlam();  break;
            case 1:  yield return JumpAttack();  break;
            default: yield return ThrowAttack(); break;
        }

        _actionTimer = _inPhase2 ? phase2Cooldown : actionCooldown;
        _busy = false;
    }

    // ── Attacks ───────────────────────────────────────────────────────────────
    private IEnumerator GroundSlam()
    {
        _anim?.Play("attack", true);
        // Wind-up: rise a little
        yield return Hop(Vector2.up * 6f);
        yield return new WaitForSeconds(0.15f);
        // Slam: force down
        _rb.linearVelocity = new Vector2(0f, -18f);
        if (attackClip != null) AudioSource.PlayClipAtPoint(attackClip, transform.position, 0.9f);

        // Wait until grounded again
        float guard = 0f;
        while (guard < 2f && _rb.linearVelocity.y < -0.5f)
        { guard += Time.deltaTime; yield return null; }

        CameraShakeNudge(0.18f);
        // Shockwave: damage the player if grounded and nearby
        if (_playerHealth != null && _player != null &&
            Vector2.Distance(transform.position, _player.position) < 4.2f)
            _playerHealth.TakeDamage(slamDamage);

        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator JumpAttack()
    {
        _anim?.Play("attack", true);
        yield return new WaitForSeconds(0.25f);

        // Leap toward the player
        float dir = _player != null
            ? Mathf.Sign(_player.position.x - transform.position.x) : 1f;
        _rb.linearVelocity = new Vector2(dir * (_inPhase2 ? 9f : 7f), 12f);
        if (attackClip != null) AudioSource.PlayClipAtPoint(attackClip, transform.position, 0.85f);

        float guard = 0f;
        yield return new WaitForSeconds(0.25f);
        while (guard < 2.5f && _rb.linearVelocity.y > 0.1f)
        { guard += Time.deltaTime; yield return null; }
        // Falling — wait for landing
        while (guard < 2.5f && _rb.linearVelocity.y < -0.5f)
        { guard += Time.deltaTime; yield return null; }

        CameraShakeNudge(0.14f);
        if (_playerHealth != null && _player != null &&
            Vector2.Distance(transform.position, _player.position) < 3.2f)
            _playerHealth.TakeDamage(jumpDamage);

        yield return new WaitForSeconds(0.25f);
    }

    private IEnumerator ThrowAttack()
    {
        _anim?.Play("attack", true);
        if (attackClip != null) AudioSource.PlayClipAtPoint(attackClip, transform.position, 0.8f);
        yield return new WaitForSeconds(0.25f);

        // Lob 3 projectiles in a quick burst
        for (int i = 0; i < 3; i++)
        {
            if (_player == null) break;
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector2 aim = ((Vector2)(_player.position + Vector3.up * 0.4f - origin)).normalized;
            aim = (aim + Vector2.up * 0.3f).normalized;
            Projectile.Spawn(origin, aim, 10f, throwDamage, true, _projSprite, 5f, 0.16f, 60);
            yield return new WaitForSeconds(0.28f);
        }
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator Hop(Vector2 force)
    {
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, force.y);
        yield return new WaitForSeconds(0.1f);
    }

    private void CameraShakeNudge(float amount)
    {
        Camera.main?.GetComponent<CameraShake>()?.Shake(amount, 0.18f);
    }

    // ── Health ────────────────────────────────────────────────────────────────
    private void OnHurt(float dmg)
    {
        if (_isDead) return;
        _bar?.SetHealth(_health.CurrentHealth / _health.MaxHealth);
        if (hurtClip != null) AudioSource.PlayClipAtPoint(hurtClip, transform.position, 0.6f);
        StartCoroutine(HitFlash());

        if (!_inPhase2 && _health.CurrentHealth <= _health.MaxHealth * 0.5f)
            EnterPhase2();
    }

    private void EnterPhase2()
    {
        _inPhase2 = true;
        _bar?.SetPhase("PHASE 2 — ENRAGED");
        transform.localScale = _baseScale * 1.12f;
        StartCoroutine(RoarFlash());
    }

    private IEnumerator RoarFlash()
    {
        CameraShakeNudge(0.25f);
        for (int i = 0; i < 4; i++)
        {
            if (_sr != null) _sr.color = new Color(1f, 0.7f, 0.3f);
            yield return new WaitForSeconds(0.07f);
            if (_sr != null) _sr.color = _origColor;
            yield return new WaitForSeconds(0.07f);
        }
    }

    private IEnumerator HitFlash()
    {
        if (_sr == null) yield break;
        _sr.color = new Color(1f, 0.45f, 0.45f);
        yield return new WaitForSeconds(0.08f);
        if (!_isDead && _sr != null) _sr.color = _origColor;
    }

    private void OnDeath()
    {
        if (_isDead) return;
        _isDead = true;
        StopAllCoroutines();
        _rb.linearVelocity = Vector2.zero;
        _rb.gravityScale   = 0.4f;
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;
        if (deathClip != null) AudioSource.PlayClipAtPoint(deathClip, transform.position, 1f);
        _bar?.Dismiss();

        // Tell the jungle manager the temple may open
        (LevelManagerBase.Current as JungleManager)?.OnGuardianDefeated();

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        _anim?.Play("hurt");
        float t = 0f;
        while (t < 1.4f)
        {
            t += Time.deltaTime;
            if (_sr != null)
                _sr.color = new Color(_origColor.r, _origColor.g, _origColor.b, 1f - t / 1.4f);
            transform.Rotate(0f, 0f, 90f * Time.deltaTime);
            yield return null;
        }
        Destroy(gameObject);
    }
}
