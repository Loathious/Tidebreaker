using UnityEngine;

/// <summary>
/// Player movement: WASD-only, no arrow keys.
/// Fixes: flipX (no scale snap), double-jump prevention, improved dash with trail.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed    = 5f;
    public float jumpForce    = 12f;
    public float acceleration = 60f;
    public float deceleration = 60f;

    [Header("Air Control")]
    public float airAcceleration = 35f;
    public float airDeceleration = 20f;
    [Range(0f, 1f)]
    public float airControlMultiplier = 0.75f;

    [Header("Dash Settings")]
    public float dashSpeed    = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Animation Timers")]
    public float landingDuration = 0.18f;
    public float coyoteTime      = 0.15f;
    public float jumpBufferTime  = 0.2f;

    [Header("Particle Effects")]
    public ParticleSystem walkParticles;
    public float particleEmissionSpeed = 2f;

    [Header("Jump Feel")]
    [Tooltip("Extra gravity applied when falling")]
    public float fallGravityMultiplier     = 2.2f;
    [Tooltip("Extra gravity when releasing jump early")]
    public float lowJumpGravityMultiplier  = 1.8f;

    [Header("Visual Flip")]
    [Tooltip("How fast the visual scale flips when changing direction (1/sec). Higher = snappier. ~25 = instant-feeling but smooth.")]
    [SerializeField] private float flipScaleSpeed = 28f;

    [Header("Dash Trail")]
    [SerializeField] private TrailRenderer dashTrail;

    // ── Public state ──────────────────────────────────────────────────────────
    /// <summary>True when the player is facing right.</summary>
    public bool FacingRight { get; private set; } = true;

    // ── Private ───────────────────────────────────────────────────────────────
    Rigidbody2D    _rb;
    Animator       _anim;
    SpriteRenderer _sr;
    WalkParticleController _particleController;
    Health         _health;

    bool  _isGrounded;
    bool  _wasGrounded;
    float _moveInput;
    float _currentSpeed;
    float _coyoteTimeCounter;
    float _jumpBufferCounter;
    float _landingTimer;
    bool  _isLanding;
    bool  _isDashing;
    float _dashTimer;
    float _dashCooldownTimer;
    int   _dashDirection;
    bool  _isDead;
    float _airborneTime;
    float _defaultGravityScale;
    bool  _justJumped;       // ← bulletproof double-jump prevention

    // Locks player input (during dialogue / cutscenes)
    private bool _inputLocked;

    const float FallThreshold         = -0.1f;
    const float MoveThreshold         = 0.01f;
    const float MinAirborneForLanding = 0.12f;

    void Start()
    {
        _rb                = GetComponent<Rigidbody2D>();
        _anim              = GetComponent<Animator>();
        _sr                = GetComponent<SpriteRenderer>();
        _particleController = GetComponent<WalkParticleController>();
        _health            = GetComponent<Health>();

        if (_rb != null)
        {
            _rb.interpolation    = RigidbodyInterpolation2D.Interpolate;
            _defaultGravityScale = _rb.gravityScale;
        }

        if (_health != null)
            _health.OnDeath.AddListener(OnDeath);

        // Trail renderer starts disabled — only shows during dash
        if (dashTrail != null)
            dashTrail.emitting = false;
    }

    // ── Public API ────────────────────────────────────────────────────────────
    public void LockInput()   => _inputLocked = true;
    public void UnlockInput() => _inputLocked = false;

    // ── Update / FixedUpdate ──────────────────────────────────────────────────
    void Update()
    {
        if (_isDead) return;

        HandleInput();
        UpdateTimers();
        HandleJump();
        HandleDash();
        UpdateAnimationState();
        FlipSprite();
        UpdateParticles();
        UpdateDashTrail();
    }

    void FixedUpdate()
    {
        if (_isDead)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        CheckGround();
        ApplyMovement();
        ApplyVariableGravity();
    }

    // ── Input ─────────────────────────────────────────────────────────────────
    void HandleInput()
    {
        if (_inputLocked)
        {
            _moveInput = 0f;
            return;
        }

        if (!_isDashing)
        {
            // WASD ONLY — A = left, D = right, no arrow keys
            float raw = 0f;
            if (Input.GetKey(KeyCode.A)) raw = -1f;
            else if (Input.GetKey(KeyCode.D)) raw = 1f;
            _moveInput = raw;
        }

        // W or Space to jump
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space))
            _jumpBufferCounter = jumpBufferTime;

        // Ctrl + horizontal movement to dash
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            && _dashCooldownTimer <= 0f && !_isDashing && Mathf.Abs(_moveInput) > 0.1f)
        {
            StartDash();
        }
    }

    // ── Timers ────────────────────────────────────────────────────────────────
    void UpdateTimers()
    {
        _coyoteTimeCounter = _isGrounded ? coyoteTime : _coyoteTimeCounter - Time.deltaTime;
        _jumpBufferCounter  -= Time.deltaTime;
        _dashCooldownTimer  -= Time.deltaTime;

        if (_isLanding)
        {
            _landingTimer -= Time.deltaTime;
            if (_landingTimer <= 0f) _isLanding = false;
        }

        if (_isDashing)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f)
            {
                _isDashing = false;
            }
        }
    }

    // ── Movement ──────────────────────────────────────────────────────────────
    void ApplyMovement()
    {
        if (_isDashing)
        {
            _rb.linearVelocity = new Vector2(_dashDirection * dashSpeed, _rb.linearVelocity.y);
            return;
        }

        if (_isLanding && Mathf.Abs(_moveInput) < MoveThreshold) return;

        float targetSpeed = _moveInput * moveSpeed;
        float speedDiff   = targetSpeed - _currentSpeed;

        float accelRate;
        if (_isGrounded)
            accelRate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
        else
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f ? airAcceleration : airDeceleration) * airControlMultiplier;

        float movement = speedDiff * accelRate * Time.fixedDeltaTime;
        _currentSpeed += movement;
        _currentSpeed  = Mathf.Clamp(_currentSpeed, -moveSpeed, moveSpeed);

        _rb.linearVelocity = new Vector2(_currentSpeed, _rb.linearVelocity.y);
    }

    void ApplyVariableGravity()
    {
        if (_isDead || _isDashing) return;

        if (_rb.linearVelocity.y < 0f)
            _rb.gravityScale = _defaultGravityScale * fallGravityMultiplier;
        else if (_rb.linearVelocity.y > 0f && !Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.Space))
            _rb.gravityScale = _defaultGravityScale * lowJumpGravityMultiplier;
        else
            _rb.gravityScale = _defaultGravityScale;
    }

    // ── Jump ──────────────────────────────────────────────────────────────────
    void HandleJump()
    {
        // _justJumped is set true on jump and only cleared when truly grounded.
        // This is bulletproof — even if coyote time mis-fires from grazing geometry
        // or rapid direction changes, the player can't jump again until they land.
        if (_jumpBufferCounter > 0f && _coyoteTimeCounter > 0f && !_isDashing && !_justJumped)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
            _jumpBufferCounter = 0f;
            _coyoteTimeCounter = 0f;
            _isLanding         = false;
            _justJumped        = true;   // ← lock until grounded again
        }
    }

    // ── Dash ──────────────────────────────────────────────────────────────────
    void StartDash()
    {
        _isDashing         = true;
        _dashTimer         = dashDuration;
        _dashCooldownTimer = dashCooldown;
        _dashDirection     = _moveInput > 0 ? 1 : -1;

        // Burst of particles at dash start
        if (_particleController != null && _isGrounded)
            _particleController.EmitParticles(_dashDirection, true);

        // Directional wind trail — orient the trail OPPOSITE the dash direction
        if (dashTrail != null)
        {
            Vector3 trailLocal = dashTrail.transform.localPosition;
            trailLocal.x = -_dashDirection * 0.3f;          // sit behind the player
            dashTrail.transform.localPosition = trailLocal;
            dashTrail.Clear();                              // start fresh each dash
            dashTrail.emitting = true;
        }

        // Spawn lightweight wind streaks behind the dash for extra polish
        SpawnDashWindStreaks(_dashDirection);

        // Momentarily cancel vertical velocity for a crisp horizontal dash
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
    }

    /// <summary>
    /// Procedural directional wind effect — spawns three short-lived white streak sprites
    /// behind the player at dash start. No assets required.
    /// </summary>
    void SpawnDashWindStreaks(int direction)
    {
        for (int i = 0; i < 3; i++)
            StartCoroutine(WindStreak(direction, i * 0.05f));
    }

    System.Collections.IEnumerator WindStreak(int direction, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);

        GameObject go = new GameObject("DashWindStreak");
        go.transform.position = transform.position + new Vector3(-direction * 0.4f,
                                                                  Random.Range(-0.3f, 0.3f), 0f);

        // Build a 1-pixel white texture
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = spr;
        sr.color        = new Color(1f, 1f, 1f, 0.6f);
        sr.sortingOrder = 100;

        // Streak shape — long horizontal sliver
        go.transform.localScale = new Vector3(60f, 4f, 1f);

        // Animate fade + slide opposite to dash direction
        float t = 0f;
        float duration = 0.35f;
        Vector3 startPos = go.transform.position;
        Vector3 endPos   = startPos + new Vector3(-direction * 1.5f, 0f, 0f);

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;
            go.transform.position = Vector3.Lerp(startPos, endPos, k);
            sr.color = new Color(1f, 1f, 1f, 0.6f * (1f - k));
            yield return null;
        }
        Destroy(go);
    }

    void HandleDash()
    {
        _anim?.SetBool("isDashing", _isDashing);
    }

    void UpdateDashTrail()
    {
        if (dashTrail == null) return;
        dashTrail.emitting = _isDashing;
    }

    // ── Ground check ──────────────────────────────────────────────────────────
    void CheckGround()
    {
        _wasGrounded = _isGrounded;
        _isGrounded  = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer);

        if (!_isGrounded)
            _airborneTime += Time.fixedDeltaTime;

        if (!_wasGrounded && _isGrounded && _rb.linearVelocity.y <= 0f && _airborneTime >= MinAirborneForLanding)
        {
            _isLanding    = true;
            _landingTimer = landingDuration;
            _currentSpeed      = 0f;
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            _particleController?.EmitParticles(_moveInput, false);
        }

        if (_isGrounded)
        {
            _airborneTime = 0f;
            _justJumped   = false;   // ← only ground contact unlocks the next jump
        }
    }

    // ── Animations ────────────────────────────────────────────────────────────
    void UpdateAnimationState()
    {
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
    /// <summary>
    /// Quick localScale.x flip — gives a snappy 2D-mirror feel (no 3D-spin look).
    /// At a high flipScaleSpeed (~28/sec), the transition is fast enough that any
    /// pivot offset is essentially imperceptible.
    /// </summary>
    void FlipSprite()
    {
        if (_isDead) return;

        if (_moveInput > MoveThreshold)       FacingRight = true;
        else if (_moveInput < -MoveThreshold) FacingRight = false;

        // Quick scale-X tween — mirrors the sprite in classic 2D fashion
        float targetX = FacingRight ? 1f : -1f;
        Vector3 s = transform.localScale;
        s.x = Mathf.MoveTowards(s.x, targetX, flipScaleSpeed * Time.deltaTime);
        // Force a clean Y-rotation reset in case anything else nudged it
        transform.rotation = Quaternion.identity;
        transform.localScale = s;

        // We're using localScale to face — keep flipX neutral
        if (_sr != null) _sr.flipX = false;
    }

    // ── Particles ─────────────────────────────────────────────────────────────
    [Header("Walk Dust")]
    [SerializeField] private float dustEmitInterval = 0.18f; // seconds between procedural dust puffs
    private float _dustTimer;

    void UpdateParticles()
    {
        bool walkingOnGround = _isGrounded
                            && Mathf.Abs(_rb.linearVelocity.x) > 0.5f
                            && !_isLanding && !_isDashing;

        // Existing assigned particle system (if wired in scene)
        if (_particleController != null && walkingOnGround)
            _particleController.EmitParticles(_rb.linearVelocity.x, false);

        // Procedural dirt-kick puffs — work even with no assigned ParticleSystem
        if (walkingOnGround)
        {
            _dustTimer += Time.deltaTime;
            if (_dustTimer >= dustEmitInterval)
            {
                _dustTimer = 0f;
                SpawnDustPuff(Mathf.Sign(_rb.linearVelocity.x));
            }
        }
        else
        {
            _dustTimer = dustEmitInterval; // ready to puff on next step
        }
    }

    /// <summary>Spawns a small dirt puff at the player's feet, kicked opposite to motion direction.</summary>
    void SpawnDustPuff(float moveDir)
    {
        Vector3 footPos = groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.down * 0.4f;
        StartCoroutine(DustPuffRoutine(footPos, moveDir));
    }

    System.Collections.IEnumerator DustPuffRoutine(Vector3 origin, float moveDir)
    {
        // Build a 1×1 white texture (will be tinted brown)
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);

        // Spawn 2 small particles per puff
        for (int i = 0; i < 2; i++)
        {
            GameObject puff = new GameObject("DustPuff");
            puff.transform.position = origin + new Vector3(Random.Range(-0.1f, 0.1f),
                                                            Random.Range(0f, 0.08f), 0f);

            SpriteRenderer sr = puff.AddComponent<SpriteRenderer>();
            sr.sprite       = spr;
            sr.color        = new Color(0.55f, 0.42f, 0.30f, 0.85f); // dirt brown
            sr.sortingOrder = 5;
            puff.transform.localScale = new Vector3(8f, 8f, 1f); // ~0.08 world units

            // Kick opposite to movement, slight upward arc
            float kickX = -moveDir * Random.Range(1.0f, 1.8f);
            float kickY = Random.Range(1.0f, 2.0f);
            StartCoroutine(AnimateDust(puff, sr, kickX, kickY));
        }
        yield break;
    }

    System.Collections.IEnumerator AnimateDust(GameObject go, SpriteRenderer sr, float vx, float vy)
    {
        float life = Random.Range(0.35f, 0.55f);
        float t = 0f;
        Vector3 vel = new Vector3(vx, vy, 0f);
        Color baseColor = sr.color;
        while (t < life && go != null)
        {
            t += Time.deltaTime;
            // Simple gravity-affected motion
            vel.y -= 6f * Time.deltaTime;
            go.transform.position += vel * Time.deltaTime;
            // Fade + grow slightly
            float k = t / life;
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - k));
            float scale = Mathf.Lerp(8f, 14f, k);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // ── Death ─────────────────────────────────────────────────────────────────
    void OnDeath()
    {
        _isDead            = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.gravityScale   = _defaultGravityScale;
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
