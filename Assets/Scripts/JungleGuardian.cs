using System.Collections;
using UnityEngine;

/// <summary>
/// Jungle Guardian â€” Level 3 mini-boss (the monkey mini-boss).
/// From spelmanus: 200 HP, two phases.
///  â€¢ Phase 1 (100â€“50%): ground slam + jump attack + throw.
///  â€¢ Phase 2 (below 50%): faster, more projectiles, enraged.
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

    // Visual center of the boss â€” sprite pivot is at corner so bounds.center is accurate.
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

        // Ensure the sprite renderer is always visible.
        // If the editor didn't assign a sprite, create a green placeholder so the
        // boss is never invisible regardless of SpriteAnimator clip setup.
        if (_sr != null)
        {
            if (_sr.sprite == null)
                _sr.sprite = ProceduralSprite.Box(32, 40, new Color(0.25f, 0.55f, 0.2f));
            _sr.color = Color.white;
        }
        _origColor = Color.white;

        _health.OnDeath.AddListener(OnDeath);
        _health.OnDamageTaken.AddListener(OnHurt);

        _projSprite  = ProceduralSprite.Circle(8, new Color(0.4f, 0.25f, 0.1f));
        _actionTimer = 1.5f;
        _anim?.Play("idle");
    }

    void Update()
    {
        if (_isDead || _player == null || LevelManagerBase.MonstersFrozen) return;

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
        if (_isDead || LevelManagerBase.MonstersFrozen) return;

        // ALWAYS hard-clamp X to patrol bounds â€” even during attacks.
        // This prevents any velocity-based overshoot from carrying the boss outside.
        if (_active && _col != null)
        {
            float cx = WorldCenter.x;
            if (cx < _minPatrolX || cx > _maxPatrolX)
            {
                float clamped = Mathf.Clamp(cx, _minPatrolX, _maxPatrolX);
                Vector3 pos = transform.position;
                pos.x += clamped - cx;
                transform.position = pos;
                _rb.position = new Vector2(pos.x, pos.y);
                // Kill horizontal velocity so the boss doesn't immediately drift back out.
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            }
        }

        if (!_active || _busy || _player == null) return;

        float wcx = WorldCenter.x;
        float spd  = _inPhase2 ? phase2Speed : moveSpeed;
        float vx;

        float dir2 = Mathf.Sign(_player.position.x - wcx);
        float dist2 = Vector2.Distance(WorldCenter, _player.position);
        bool atEdge = (wcx <= _minPatrolX + 0.5f && dir2 < 0f) ||
                      (wcx >= _maxPatrolX - 0.5f && dir2 > 0f);

        vx = (dist2 > meleeRange * 0.8f && !atEdge) ? dir2 * spd : 0f;
        if (dir2 != 0f && _sr != null) _sr.flipX = dir2 < 0f;

        _rb.linearVelocity = new Vector2(vx, _rb.linearVelocity.y);
        _anim?.Play(Mathf.Abs(vx) > 0.1f ? "move" : "idle");
    }

    private void BeginFight()
    {
        _active = true;
        LevelManagerBase.Current?.NotifyCombatStarted();
        _bar = BossHealthBar.Create("JUNGLE GUARDIAN");
        if (_bar == null)
            Debug.LogWarning("[JungleGuardian] BossHealthBar.Create returned null â€” no overlay canvas found!");
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

        // Close range â†’ ground punch.
        // Phase 2 mid-range â†’ jump attack.
        // Far range â†’ throw projectiles.
        if (dist < meleeRange * 1.8f)
            yield return GroundSlam();
        else if (_inPhase2 && dist < meleeRange * 4f)
            yield return JumpAttack();
        else
            yield return ThrowAttack();

        _actionTimer = _inPhase2 ? phase2Cooldown : actionCooldown;
        _busy = false;
    }

    // â”€â”€ Attacks â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // Maximum air time before the jump coroutine forces the boss back to
    // tracking ground state â€” prevents getting stuck airborne on collision edge-cases.
    private const float kAirTime = 0.9f;

    // GroundSlam: pure animation-based â€” zero velocity changes so there is no
    // physics stutter or apparent teleportation. Boss stays on the ground.
    private IEnumerator GroundSlam()
    {
        _anim?.Play("attack", true);
        // Wind-up
        yield return new WaitForSeconds(0.25f);

        if (attackClip != null) SettingsManager.PlaySfxAt(attackClip, WorldCenter, 0.9f);
        CameraShakeNudge(0.28f);

        if (_playerHealth != null && _player != null &&
            Vector2.Distance(WorldCenter, _player.position) < 4.5f)
            _playerHealth.TakeDamage(slamDamage);

        // Recovery
        yield return new WaitForSeconds(0.45f);
        _anim?.Play("idle");
    }

    // JumpAttack: boss leaps toward the player then slams on landing.
    // Used in Phase 2 when the player is at mid range.
    private IEnumerator JumpAttack()
    {
        if (_player == null) yield break;
        _anim?.Play("attack", true);
        yield return new WaitForSeconds(0.2f);

        float dir = Mathf.Sign(_player.position.x - WorldCenter.x);
        // Velocity capped via kAirTime: vy chosen so airborne time â‰¤ kAirTime.
        // v = g * kAirTime / 2  â†’  lands within the cap even if gravity varies.
        float vy = _rb.gravityScale * Mathf.Abs(Physics2D.gravity.y) * kAirTime * 0.5f;
        float vx = dir * (phase2Speed + 3f);
        _rb.linearVelocity = new Vector2(vx, vy);

        if (attackClip != null) SettingsManager.PlaySfxAt(attackClip, WorldCenter, 0.9f);

        // Wait for landing (velocity turns downward) or until kAirTime expires.
        float airElapsed = 0f;
        yield return null; // skip one frame so velocity is applied
        while (airElapsed < kAirTime && _rb.linearVelocity.y > -0.5f)
        {
            airElapsed += Time.deltaTime;
            yield return null;
        }
        // Kill horizontal velocity on landing to stop skidding.
        _rb.linearVelocity = new Vector2(0f, Mathf.Min(_rb.linearVelocity.y, 0f));

        CameraShakeNudge(0.3f);
        if (_playerHealth != null && _player != null &&
            Vector2.Distance(WorldCenter, _player.position) < 3.5f)
            _playerHealth.TakeDamage(slamDamage);

        yield return new WaitForSeconds(0.35f);
        _anim?.Play("idle");
    }

    private IEnumerator ThrowAttack()
    {
        _anim?.Play("attack", true);
        if (attackClip != null) SettingsManager.PlaySfxAt(attackClip, WorldCenter, 0.8f);
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

    // â”€â”€ Health â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void OnHurt(float dmg)
    {
        if (_isDead) return;
        _bar?.SetHealth(_health.CurrentHealth / _health.MaxHealth);
        if (hurtClip != null) SettingsManager.PlaySfxAt(hurtClip, WorldCenter, 0.6f);
        SpawnBlood();
        StartCoroutine(HitFlash());

        if (!_inPhase2 && _health.CurrentHealth <= _health.MaxHealth * 0.5f)
            EnterPhase2();
    }

    private void EnterPhase2()
    {
        _inPhase2 = true;
        _bar?.SetPhase("PHASE 2 â€” ENRAGED");
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
        if (deathClip != null) SettingsManager.PlaySfxAt(deathClip, WorldCenter, 1f);
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
