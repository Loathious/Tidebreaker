using System.Collections;
using UnityEngine;

/// <summary>
/// Jungle Guardian — Level 3 mini-boss (the monkey mini-boss).
/// From spelmanus: 200 HP, two phases.
///  • Phase 1 (100–50%): ground slam + jump attack + throw.
///  • Phase 2 (below 50%): faster, more projectiles, enraged.
///
/// NOTE: This boss has a sprite whose pivot is at the corner (0,0), so
/// transform.position != visual center.  All gameplay calculations
/// (distances, projectile origins, audio) use WorldCenter which reads
/// Collider2D.bounds.center instead, giving the correct visual position.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class JungleGuardian : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float maxHealth      = 200f;
    [SerializeField] private float moveSpeed      = 2.4f;
    [SerializeField] private float phase2Speed    = 3.8f;
    [SerializeField] private float detectionRange = 20f;
    [SerializeField] private float meleeRange     = 2.8f;
    [SerializeField] private int   slamDamage     = 22;
    [SerializeField] private int   jumpDamage     = 18;
    [SerializeField] private int   throwDamage    = 14;

    [Header("Timing")]
    [SerializeField] private float actionCooldown  = 2.2f;
    [SerializeField] private float phase2Cooldown  = 1.2f;

    [Header("Audio")]
    public AudioClip attackClip;
    public AudioClip hurtClip;
    public AudioClip deathClip;

    private Rigidbody2D    _rb;
    private Health         _health;
    private SpriteAnimator _anim;
    private SpriteRenderer _sr;
    private Collider2D     _col;
    private Transform      _player;
    private Health         _playerHealth;
    private BossHealthBar  _bar;

    private bool  _active;
    private bool  _busy;
    private bool  _isDead;
    private bool  _inPhase2;
    private float _actionTimer;
    private Color _origColor = Color.white;
    private Sprite _projSprite;
    private Vector3 _baseScale;

    // Arena X limits (visual-center space). Defaulting to huge values means
    // nothing clamps until SetPatrolBounds() is called by JungleManager.
    private float _minPatrolX = -999f;
    private float _maxPatrolX =  999f;

    // Visual center of the boss — sprite pivot is at corner so bounds.center is accurate.
    private Vector3 WorldCenter =>
        _col != null ? (Vector3)_col.bounds.center : transform.position;

    /// <summary>Called by JungleManager to define the arena X limits (visual-center space).</summary>
    public void SetPatrolBounds(float minX, float maxX)
    {
        _minPatrolX = minX;
        _maxPatrolX = maxX;
        Debug.Log($"[JungleGuardian] Patrol bounds set: [{minX:F2}, {maxX:F2}]  VisualCenterX now={WorldCenter.x:F2}");
    }

    void Awake()
    {
        _rb     = GetComponent<Rigidbody2D>();
        _health = GetComponent<Health>();
        _anim   = GetComponent<SpriteAnimator>();
        _sr     = GetComponent<SpriteRenderer>();
        _col    = GetComponent<Collider2D>();

        _rb.gravityScale   = 4f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Sync the physics body to the transform position immediately so that
        // col.bounds is correct during Start() / OnLevelStart() calls.
        _rb.position = new Vector2(transform.position.x, transform.position.y);
        Physics2D.SyncTransforms();
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
            if (Vector2.Distance(WorldCenter, _player.position) <= detectionRange)
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

        float cx  = WorldCenter.x;
        float spd = _inPhase2 ? phase2Speed : moveSpeed;
        float vx;

        if (cx < _minPatrolX)
        {
            // Outside left bound — walk right to re-enter arena (no snap).
            vx = spd;
            if (_sr != null) _sr.flipX = false;
        }
        else if (cx > _maxPatrolX)
        {
            // Outside right bound — walk left to re-enter arena (no snap).
            vx = -spd;
            if (_sr != null) _sr.flipX = true;
        }
        else
        {
            // Within bounds: walk toward player, soft-stop at the edge so the
            // boss never pushes past the boundary under its own movement.
            float dir  = Mathf.Sign(_player.position.x - cx);
            float dist = Vector2.Distance(WorldCenter, _player.position);
            bool  atEdge = (cx <= _minPatrolX + 0.5f && dir < 0f) ||
                           (cx >= _maxPatrolX - 0.5f && dir > 0f);

            vx = (dist > meleeRange * 0.8f && !atEdge) ? dir * spd : 0f;
            if (dir != 0f && _sr != null) _sr.flipX = dir < 0f;
        }

        _rb.linearVelocity = new Vector2(vx, _rb.linearVelocity.y);
        _anim?.Play(Mathf.Abs(vx) > 0.1f ? "move" : "idle");
    }

    private void BeginFight()
    {
        _active = true;
        LevelManagerBase.Current?.NotifyCombatStarted();
        _bar = BossHealthBar.Create("JUNGLE GUARDIAN");
        if (_bar == null)
            Debug.LogWarning("[JungleGuardian] BossHealthBar.Create returned null — no overlay canvas found!");
        else
            Debug.Log("[JungleGuardian] BossHealthBar created OK.");
        _bar?.SetHealth(1f);
        _bar?.SetPhase("PHASE 1");
        StartCoroutine(BossIntro());
    }

    private IEnumerator BossIntro()
    {
        _busy = true;
        CameraShakeNudge(0.22f);
        for (int i = 0; i < 3; i++)
        {
            if (_sr != null) _sr.color = new Color(1f, 0.65f, 0.2f);
            yield return new WaitForSeconds(0.08f);
            if (_sr != null) _sr.color = _origColor;
            yield return new WaitForSeconds(0.07f);
        }
        _actionTimer = 0.7f;
        _busy = false;
    }

    private IEnumerator DoAction()
    {
        _busy = true;
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

        float dist = _player != null
            ? Vector2.Distance(WorldCenter, _player.position) : 99f;

        int choice;
        if (_inPhase2)
        {
            // Phase 2: distance-weighted so the boss picks useful attacks.
            // Close → slam or jump; far → jump or throw (not slam from across the arena).
            choice = dist < meleeRange * 1.5f
                ? Random.Range(0, 2)   // 0 = slam, 1 = jump
                : Random.Range(1, 3);  // 1 = jump, 2 = throw
        }
        else
        {
            choice = dist < meleeRange        ? 0
                   : dist < meleeRange * 2.5f ? 1
                   : 2;
        }

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
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 6f);
        yield return new WaitForSeconds(0.18f);

        _rb.linearVelocity = new Vector2(0f, -20f);
        if (attackClip != null) AudioSource.PlayClipAtPoint(attackClip, WorldCenter, 0.9f);

        float guard = 0f;
        while (guard < 2f && _rb.linearVelocity.y < -0.5f)
        { guard += Time.deltaTime; yield return null; }

        CameraShakeNudge(0.24f);
        if (_playerHealth != null && _player != null &&
            Vector2.Distance(WorldCenter, _player.position) < 4.5f)
            _playerHealth.TakeDamage(slamDamage);

        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator JumpAttack()
    {
        _anim?.Play("attack", true);
        yield return new WaitForSeconds(0.22f);

        if (_player == null) yield break;

        float dir    = Mathf.Sign(_player.position.x - WorldCenter.x);
        float jumpVX = _inPhase2 ? 9f : 7f;

        // Clamp jump so the boss overshoots its patrol boundary by at most 2 units.
        // The soft overshoot avoids hard snaps — FixedUpdate walks the boss back smoothly.
        const float kAirTime = 0.72f; // approx at gravityScale=4, initial Y=14
        float roomRight = (_maxPatrolX + 2f) - WorldCenter.x;
        float roomLeft  = WorldCenter.x - (_minPatrolX - 2f);
        if (dir > 0f) jumpVX = Mathf.Clamp(jumpVX, 0.5f, roomRight / kAirTime);
        else          jumpVX = Mathf.Clamp(jumpVX, 0.5f, roomLeft  / kAirTime);

        _rb.linearVelocity = new Vector2(dir * jumpVX, 14f);
        Debug.Log($"[JungleGuardian] JumpAttack: centerX={WorldCenter.x:F2} dir={dir} vX={dir*jumpVX:F2} bounds=[{_minPatrolX:F2},{_maxPatrolX:F2}]");
        if (attackClip != null) AudioSource.PlayClipAtPoint(attackClip, WorldCenter, 0.85f);

        // Wait for apex (going up), then wait for landing (going down → zero).
        yield return new WaitForSeconds(0.2f);
        float t1 = 0f;
        while (t1 < 1.5f && _rb.linearVelocity.y > 0.1f)
        { t1 += Time.deltaTime; yield return null; }
        float t2 = 0f;
        while (t2 < 1.5f && _rb.linearVelocity.y < -0.3f)
        { t2 += Time.deltaTime; yield return null; }

        CameraShakeNudge(0.18f);
        if (_playerHealth != null && _player != null &&
            Vector2.Distance(WorldCenter, _player.position) < 3.5f)
            _playerHealth.TakeDamage(jumpDamage);

        yield return new WaitForSeconds(0.28f);
    }

    private IEnumerator ThrowAttack()
    {
        _anim?.Play("attack", true);
        if (attackClip != null) AudioSource.PlayClipAtPoint(attackClip, WorldCenter, 0.8f);
        yield return new WaitForSeconds(0.22f);

        int count = _inPhase2 ? 4 : 3;
        for (int i = 0; i < count; i++)
        {
            if (_player == null) break;
            Vector3 origin = WorldCenter + Vector3.up * 0.4f;
            Vector2 aim = ((Vector2)(_player.position + Vector3.up * 0.5f - origin)).normalized;
            aim = (aim + Vector2.up * 0.25f).normalized;
            Projectile.Spawn(origin, aim, 10f, throwDamage, true, _projSprite, 5f, 0.16f, 60);
            yield return new WaitForSeconds(0.24f);
        }
        yield return new WaitForSeconds(0.2f);
    }

    private void CameraShakeNudge(float amount)
    {
        Camera.main?.GetComponent<CameraShake>()?.Shake(amount, 0.22f);
    }

    private void SpawnBlood()
    {
        var go = new GameObject("BloodFX");
        go.transform.position = WorldCenter;
        go.AddComponent<ParticleSystem>();
        go.AddComponent<BloodParticleSetup>();
        go.GetComponent<ParticleSystem>().Emit(12);
        Destroy(go, 2f);
    }

    // ── Health ────────────────────────────────────────────────────────────────

    private void OnHurt(float dmg)
    {
        if (_isDead) return;
        _bar?.SetHealth(_health.CurrentHealth / _health.MaxHealth);
        if (hurtClip != null) AudioSource.PlayClipAtPoint(hurtClip, WorldCenter, 0.6f);
        SpawnBlood();
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
        CameraShakeNudge(0.3f);
        for (int i = 0; i < 5; i++)
        {
            if (_sr != null) _sr.color = new Color(1f, 0.45f, 0.15f);
            yield return new WaitForSeconds(0.07f);
            if (_sr != null) _sr.color = _origColor;
            yield return new WaitForSeconds(0.06f);
        }
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
        StopAllCoroutines();
        _rb.linearVelocity = Vector2.zero;
        _rb.gravityScale   = 0.4f;
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;
        if (deathClip != null) AudioSource.PlayClipAtPoint(deathClip, WorldCenter, 1f);
        _bar?.Dismiss();

        (LevelManagerBase.Current as JungleManager)?.OnGuardianDefeated();

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        CameraShakeNudge(0.32f);
        for (int i = 0; i < 3; i++) { SpawnBlood(); yield return new WaitForSeconds(0.12f); }

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
