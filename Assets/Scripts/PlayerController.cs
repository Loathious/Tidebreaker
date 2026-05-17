using System.Collections;
using UnityEngine;

/// <summary>
/// Player movement — fluid dash with air dash, apex modifier, ghost afterimage,
/// momentum carry, soft landing, and responsive air control.
///
/// Controls:
///   A / D          — move
///   W / Space      — jump (coyote time + jump buffer)
///   Left Shift     — dash (ground dash OR one air dash per airborne period)
///   W/Space mid-dash — dash-cancel jump for combo flow
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed    = 6f;
    public float jumpForce    = 13f;
    public float acceleration = 70f;
    public float deceleration = 55f;

    [Header("Air Control")]
    public float airAcceleration      = 55f;
    public float airDeceleration      = 25f;
    [Range(0f, 1f)]
    public float airControlMultiplier = 0.9f;

    [Header("Dash")]
    [Tooltip("Horizontal speed during a ground dash")]
    public float dashSpeed         = 18f;
    [Tooltip("Horizontal speed during an air dash (slightly lower for balance)")]
    public float airDashSpeed      = 14f;
    [Tooltip("How long the dash impulse lasts")]
    public float dashDuration      = 0.18f;
    [Tooltip("Cooldown before next dash")]
    public float dashCooldown      = 0.7f;
    [Tooltip("Fraction of dash speed carried as momentum after dash ends")]
    [Range(0f, 1f)]
    public float dashMomentum      = 0.45f;
    [Tooltip("How long the post-dash momentum lasts (seconds)")]
    public float dashMomentumDecay = 0.22f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float     groundRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Timings")]
    public float landingDuration = 0.12f;
    public float coyoteTime      = 0.15f;
    public float jumpBufferTime  = 0.2f;

    [Header("Particles")]
    public ParticleSystem walkParticles;

    [Header("Jump Feel")]
    [Tooltip("Extra gravity when falling")]
    public float fallGravityMultiplier    = 2.5f;
    [Tooltip("Extra gravity when releasing jump early")]
    public float lowJumpGravityMultiplier = 2.0f;
    [Tooltip("Gravity reduction at jump apex for a brief floaty peak")]
    public float apexGravityMultiplier    = 0.55f;
    [Tooltip("|velocityY| below this value = apex zone")]
    public float apexThreshold            = 2.0f;
    [Tooltip("Horizontal speed bonus at the apex")]
    public float apexSpeedBonus           = 1.5f;

    [Header("Visual")]
    [SerializeField] private float         flipScaleSpeed = 32f;
    [SerializeField] private TrailRenderer dashTrail;

    [Header("Walk Dust")]
    [SerializeField] private float dustEmitInterval = 0.16f;

    // ── Public state ──────────────────────────────────────────────────────────
    public bool FacingRight { get; private set; } = true;

    // ── Private ───────────────────────────────────────────────────────────────
    Rigidbody2D            _rb;
    Animator               _anim;
    SpriteRenderer         _sr;
    WalkParticleController _particleController;
    Health                 _health;

    bool  _isGrounded;
    bool  _wasGrounded;
    float _moveInput;
    float _currentSpeed;
    float _coyoteTimeCounter;
    float _jumpBufferCounter;
    float _landingTimer;
    bool  _isLanding;

    // Dash state
    bool  _isDashing;
    bool  _isAirDashing;
    bool  _canAirDash = true;   // token consumed on air dash, restored on landing
    float _dashTimer;
    float _dashCooldownTimer;
    int   _dashDirection;

    // Momentum carry after dash
    float _momentumTimer;
    float _momentumVelocity;

    bool  _isDead;
    float _airborneTime;
    float _defaultGravityScale;
    bool  _justJumped;
    bool  _atApex;

    private bool  _inputLocked;
    private float _dustTimer;

    // Ghost afterimage
    float _ghostTimer;
    const float GhostInterval = 0.04f;

    const float FallThreshold         = -0.1f;
    const float MoveThreshold         = 0.01f;
    const float MinAirborneForLanding = 0.10f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Start()
    {
        _rb                 = GetComponent<Rigidbody2D>();
        _anim               = GetComponent<Animator>();
        _sr                 = GetComponent<SpriteRenderer>();
        _particleController = GetComponent<WalkParticleController>();
        _health             = GetComponent<Health>();

        if (_rb != null)
        {
            _rb.interpolation    = RigidbodyInterpolation2D.Interpolate;
            _defaultGravityScale = _rb.gravityScale;
        }

        if (_health != null) _health.OnDeath.AddListener(OnDeath);
        if (dashTrail != null) dashTrail.emitting = false;
        _canAirDash = true;
    }

    public void LockInput()   => _inputLocked = true;
    public void UnlockInput() => _inputLocked = false;

    void Update()
    {
        if (_isDead) return;
        HandleInput();
        UpdateTimers();
        HandleJump();
        HandleDashAnim();
        UpdateAnimationState();
        FlipSprite();
        UpdateParticles();
        UpdateDashTrail();
        UpdateGhostAfterimage();
    }

    void FixedUpdate()
    {
        if (_isDead) return;
        CheckGround();
        ApplyMovement();
        ApplyVariableGravity();
    }

    // ── Input ─────────────────────────────────────────────────────────────────
    void HandleInput()
    {
        if (_inputLocked) { _moveInput = 0f; return; }

        if (!_isDashing)
        {
            float raw = 0f;
            if (Input.GetKey(KeyCode.A))      raw = -1f;
            else if (Input.GetKey(KeyCode.D)) raw =  1f;
            _moveInput = raw;
        }

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space))
            _jumpBufferCounter = jumpBufferTime;

        // Shift to dash — no direction held → dash in facing direction
        bool shiftDown = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
        if (shiftDown && _dashCooldownTimer <= 0f && !_isDashing && (_isGrounded || _canAirDash))
        {
            if (Mathf.Abs(_moveInput) < 0.1f)
                _moveInput = FacingRight ? 1f : -1f;
            StartDash();
        }
    }

    // ── Timers ────────────────────────────────────────────────────────────────
    void UpdateTimers()
    {
        _coyoteTimeCounter  = _isGrounded ? coyoteTime : _coyoteTimeCounter - Time.deltaTime;
        _jumpBufferCounter -= Time.deltaTime;
        _dashCooldownTimer -= Time.deltaTime;
        _momentumTimer     -= Time.deltaTime;

        if (_isLanding)
        {
            _landingTimer -= Time.deltaTime;
            if (_landingTimer <= 0f) _isLanding = false;
        }

        if (_isDashing)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f) EndDash();
        }
    }

    // ── Movement ──────────────────────────────────────────────────────────────
    void ApplyMovement()
    {
        if (_isDashing)
        {
            float spd = _isAirDashing ? airDashSpeed : dashSpeed;
            // Air dash: bleed 35 % of vertical velocity rather than zeroing it
            float vy = _isAirDashing ? _rb.linearVelocity.y * 0.35f : 0f;
            _rb.linearVelocity = new Vector2(_dashDirection * spd, vy);
            return;
        }

        if (_isLanding && Mathf.Abs(_moveInput) < MoveThreshold) return;

        float targetSpeed = _moveInput * moveSpeed;

        // Apex speed bonus for satisfying momentum at jump peak
        if (_atApex && !_isGrounded && Mathf.Abs(targetSpeed) > 0.01f)
            targetSpeed += Mathf.Sign(targetSpeed) * apexSpeedBonus;

        float speedDiff = targetSpeed - _currentSpeed;
        float accelRate = _isGrounded
            ? (Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration)
            : (Mathf.Abs(targetSpeed) > 0.01f ? airAcceleration : airDeceleration) * airControlMultiplier;

        _currentSpeed += speedDiff * accelRate * Time.fixedDeltaTime;
        _currentSpeed  = Mathf.Clamp(_currentSpeed, -moveSpeed, moveSpeed);

        // Decaying post-dash momentum bonus
        float bonus = (_momentumTimer > 0f)
            ? _momentumVelocity * Mathf.Clamp01(_momentumTimer / dashMomentumDecay)
            : 0f;

        _rb.linearVelocity = new Vector2(_currentSpeed + bonus, _rb.linearVelocity.y);
    }

    void ApplyVariableGravity()
    {
        if (_isDead || _isDashing) return;

        float vy = _rb.linearVelocity.y;
        _atApex  = !_isGrounded && Mathf.Abs(vy) < apexThreshold && vy > -0.5f;

        if (_atApex)
            _rb.gravityScale = _defaultGravityScale * apexGravityMultiplier;
        else if (vy < 0f)
            _rb.gravityScale = _defaultGravityScale * fallGravityMultiplier;
        else if (vy > 0f && !Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.Space))
            _rb.gravityScale = _defaultGravityScale * lowJumpGravityMultiplier;
        else
            _rb.gravityScale = _defaultGravityScale;
    }

    // ── Jump ──────────────────────────────────────────────────────────────────
    void HandleJump()
    {
        // Dash-cancel jump: pressing jump mid-dash cancels the dash and launches
        // upward — great for combo flow
        if (_isDashing && (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space)))
        {
            EndDash();
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce * 0.9f);
            _justJumped        = true;
            _jumpBufferCounter = 0f;
            return;
        }

        if (_jumpBufferCounter > 0f && _coyoteTimeCounter > 0f && !_isDashing && !_justJumped)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
            _jumpBufferCounter = 0f;
            _coyoteTimeCounter = 0f;
            _isLanding         = false;
            _justJumped        = true;
        }
    }

    // ── Dash ──────────────────────────────────────────────────────────────────
    void StartDash()
    {
        _isAirDashing = !_isGrounded;
        if (_isAirDashing) _canAirDash = false;     // consume air-dash token

        _isDashing         = true;
        _dashTimer         = dashDuration;
        _dashCooldownTimer = dashCooldown;
        _dashDirection     = _moveInput > 0 ? 1 : -1;

        // Ground: zero Y for a crisp horizontal burst; air: keep Y for fluid feel
        if (!_isAirDashing)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);

        // Particle burst + small camera shake
        if (_particleController != null && _isGrounded)
            _particleController.EmitParticles(_dashDirection, true);
        Camera.main?.GetComponent<CameraShake>()?.Shake(0.08f, 0.12f);

        // Camera look-ahead nudge in dash direction, then ease back
        CameraFollow cf       = Camera.main?.GetComponent<CameraFollow>();
        float        origOffX = cf != null ? cf.offset.x : 0f;
        if (cf != null) cf.offset = new Vector3(_dashDirection * 1.8f, cf.offset.y, cf.offset.z);
        StartCoroutine(EaseCameraOffsetBack(cf, origOffX, 0.4f));

        // Trail
        if (dashTrail != null)
        {
            Vector3 tl = dashTrail.transform.localPosition;
            tl.x = -_dashDirection * 0.3f;
            dashTrail.transform.localPosition = tl;
            dashTrail.Clear();
            dashTrail.emitting = true;
        }

        // Wind streaks — more on ground, fewer in air
        SpawnDashWindStreaks(_dashDirection, _isAirDashing ? 4 : 6);
        _ghostTimer = 0f;
    }

    void EndDash()
    {
        bool wasAirDash = _isAirDashing;
        _isDashing    = false;
        _isAirDashing = false;

        // Carry a fraction of dash speed as decaying momentum
        float spd = wasAirDash ? airDashSpeed : dashSpeed;
        _momentumVelocity = _dashDirection * spd * dashMomentum;
        _momentumTimer    = dashMomentumDecay;
        _currentSpeed     = _dashDirection * spd * dashMomentum;

        if (!_isDead) _rb.gravityScale = _defaultGravityScale;
    }

    IEnumerator EaseCameraOffsetBack(CameraFollow cf, float targetX, float duration)
    {
        if (cf == null) yield break;
        float elapsed   = 0f;
        float startX    = cf.offset.x;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float x  = Mathf.Lerp(startX, targetX, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            cf.offset = new Vector3(x, cf.offset.y, cf.offset.z);
            yield return null;
        }
        cf.offset = new Vector3(targetX, cf.offset.y, cf.offset.z);
    }

    void HandleDashAnim()   => _anim?.SetBool("isDashing", _isDashing);
    void UpdateDashTrail()  { if (dashTrail != null) dashTrail.emitting = _isDashing; }

    // ── Ghost afterimage ──────────────────────────────────────────────────────
    void UpdateGhostAfterimage()
    {
        if (!_isDashing || _sr == null || _sr.sprite == null) return;
        _ghostTimer += Time.deltaTime;
        if (_ghostTimer >= GhostInterval) { _ghostTimer = 0f; SpawnGhost(); }
    }

    void SpawnGhost()
    {
        GameObject go = new GameObject("DashGhost");
        go.transform.position   = transform.position;
        go.transform.localScale = transform.localScale;
        go.transform.rotation   = transform.rotation;

        SpriteRenderer gsr = go.AddComponent<SpriteRenderer>();
        gsr.sprite       = _sr.sprite;
        gsr.flipX        = _sr.flipX;
        gsr.sortingOrder = _sr.sortingOrder - 1;
        // Icy blue for ground dash, purple for air dash
        gsr.color = _isAirDashing
            ? new Color(0.8f, 0.5f, 1f,  0.5f)
            : new Color(0.4f, 0.75f, 1f, 0.5f);

        StartCoroutine(FadeGhost(gsr, go));
    }

    IEnumerator FadeGhost(SpriteRenderer gsr, GameObject go)
    {
        float t = 0f, dur = 0.18f;
        Color c = gsr.color;
        while (t < dur && go != null)
        {
            t += Time.deltaTime;
            if (gsr != null) gsr.color = new Color(c.r, c.g, c.b, c.a * (1f - t / dur));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // ── Wind streaks ──────────────────────────────────────────────────────────
    void SpawnDashWindStreaks(int direction, int count)
    {
        for (int i = 0; i < count; i++)
            StartCoroutine(WindStreak(direction, i * 0.03f));
    }

    IEnumerator WindStreak(int direction, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        GameObject go = new GameObject("DashWind");
        go.transform.position = transform.position
            + new Vector3(-direction * 0.3f, Random.Range(-0.4f, 0.4f), 0f);

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white); tex.Apply(); tex.filterMode = FilterMode.Point;
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 100f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = spr;
        sr.color        = new Color(0.6f, 0.85f, 1f, 0.7f);
        sr.sortingOrder = 100;
        go.transform.localScale = new Vector3(Random.Range(55f, 90f), Random.Range(2f, 5f), 1f);

        float t = 0f, dur = Random.Range(0.2f, 0.38f);
        Vector3 start = go.transform.position;
        Vector3 end   = start + new Vector3(-direction * Random.Range(1.2f, 2.2f), 0f, 0f);
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            go.transform.position = Vector3.Lerp(start, end, k);
            sr.color = new Color(0.6f, 0.85f, 1f, 0.7f * (1f - t / dur));
            yield return null;
        }
        Destroy(go);
    }

    // ── Ground check ──────────────────────────────────────────────────────────
    void CheckGround()
    {
        _wasGrounded = _isGrounded;
        _isGrounded  = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer);

        if (!_isGrounded) _airborneTime += Time.fixedDeltaTime;

        if (!_wasGrounded && _isGrounded && _rb.linearVelocity.y <= 0f
            && _airborneTime >= MinAirborneForLanding)
        {
            _isLanding    = true;
            _landingTimer = landingDuration;

            // Soft landing: preserve 60 % of momentum rather than dead-stopping
            _currentSpeed      = _currentSpeed * 0.6f;
            _rb.linearVelocity = new Vector2(_currentSpeed, _rb.linearVelocity.y);

            _particleController?.EmitParticles(_moveInput, false);
            SpawnLandingDust();
        }

        if (_isGrounded)
        {
            _airborneTime = 0f;
            _justJumped   = false;
            _canAirDash   = true;       // restore air-dash token on ground contact
        }
    }

    void SpawnLandingDust()
    {
        for (int i = 0; i < 3; i++)
        {
            Vector3 pos = groundCheck != null
                ? groundCheck.position + new Vector3(Random.Range(-0.35f, 0.35f), 0f, 0f)
                : transform.position + Vector3.down * 0.4f;
            float dirX = i == 1 ? 1f : (i == 2 ? -1f : 0f);
            StartCoroutine(AnimateDust(
                SpawnDustGO(pos, new Color(0.55f, 0.42f, 0.30f, 0.9f)),
                dirX * Random.Range(1.5f, 2.5f),
                Random.Range(2f, 3.5f)));
        }
    }

    // ── Animations ────────────────────────────────────────────────────────────
    void UpdateAnimationState()
    {
        if (_anim == null) return;
        bool isMoving  = Mathf.Abs(_moveInput) > MoveThreshold;
        bool isFalling = _rb.linearVelocity.y < FallThreshold && !_isGrounded;
        bool isJumping = _rb.linearVelocity.y > FallThreshold && !_isGrounded;

        _anim.SetBool("isGrounded", _isGrounded);
        _anim.SetBool("isRunning",  isMoving && _isGrounded && !_isLanding && !_isDashing);
        _anim.SetBool("isFalling",  isFalling);
        _anim.SetBool("isJumping",  isJumping);
        _anim.SetBool("isLanding",  _isLanding);
        _anim.SetBool("isDead",     _isDead);
        _anim.SetFloat("yVelocity", _rb.linearVelocity.y);
        _anim.SetFloat("xVelocity", Mathf.Abs(_rb.linearVelocity.x));
    }

    // ── Sprite flip ───────────────────────────────────────────────────────────
    void FlipSprite()
    {
        if (_isDead) return;
        if (_moveInput > MoveThreshold)       FacingRight = true;
        else if (_moveInput < -MoveThreshold) FacingRight = false;

        float targetX = FacingRight ? 1f : -1f;
        Vector3 s = transform.localScale;
        s.x = Mathf.MoveTowards(s.x, targetX, flipScaleSpeed * Time.deltaTime);
        transform.rotation   = Quaternion.identity;
        transform.localScale = s;
        if (_sr != null) _sr.flipX = false;
    }

    // ── Particles ─────────────────────────────────────────────────────────────
    void UpdateParticles()
    {
        bool walkingOnGround = _isGrounded
            && Mathf.Abs(_rb.linearVelocity.x) > 0.5f
            && !_isLanding && !_isDashing;

        if (_particleController != null && walkingOnGround)
            _particleController.EmitParticles(_rb.linearVelocity.x, false);

        if (walkingOnGround)
        {
            _dustTimer += Time.deltaTime;
            if (_dustTimer >= dustEmitInterval)
            {
                _dustTimer = 0f;
                SpawnDustPuff(Mathf.Sign(_rb.linearVelocity.x));
            }
        }
        else _dustTimer = dustEmitInterval;
    }

    void SpawnDustPuff(float moveDir)
    {
        Vector3 foot = groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.down * 0.4f;

        for (int i = 0; i < 2; i++)
        {
            Vector3 pos = foot + new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(0f, 0.08f), 0f);
            StartCoroutine(AnimateDust(
                SpawnDustGO(pos, new Color(0.55f, 0.42f, 0.30f, 0.85f)),
                -moveDir * Random.Range(1.0f, 1.8f),
                Random.Range(1.0f, 2.0f)));
        }
    }

    GameObject SpawnDustGO(Vector3 pos, Color col)
    {
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white); tex.Apply(); tex.filterMode = FilterMode.Point;
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 100f);

        GameObject go = new GameObject("DustPuff");
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * 8f;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr; sr.color = col; sr.sortingOrder = 5;
        return go;
    }

    IEnumerator AnimateDust(GameObject go, float vx, float vy)
    {
        if (go == null) yield break;
        SpriteRenderer sr  = go.GetComponent<SpriteRenderer>();
        float          life = Random.Range(0.30f, 0.50f), t = 0f;
        Vector3        vel  = new Vector3(vx, vy, 0f);
        Color          base_col = sr != null ? sr.color : Color.white;
        while (t < life && go != null)
        {
            t         += Time.deltaTime;
            vel.y     -= 6f * Time.deltaTime;
            go.transform.position   += vel * Time.deltaTime;
            float k = t / life;
            if (sr != null)
                sr.color = new Color(base_col.r, base_col.g, base_col.b, base_col.a * (1f - k));
            if (go != null)
                go.transform.localScale = Vector3.one * Mathf.Lerp(8f, 14f, k);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // ── Death ─────────────────────────────────────────────────────────────────
    void OnDeath()
    {
        _isDead            = true;
        _rb.gravityScale   = _defaultGravityScale;
        _rb.linearVelocity = new Vector2(0f, -4f);
        foreach (Collider2D col in GetComponents<Collider2D>())
            col.enabled = false;
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;
        _anim?.SetBool("isDead", true);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        }
    }
}
