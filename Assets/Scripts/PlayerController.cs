using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;
    public float acceleration = 50f;
    public float deceleration = 50f;
    
    [Header("Dash Settings")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    
    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.2f;
    public LayerMask groundLayer;
    
    [Header("Animation Timers")]
    public float landingDuration = 0.2f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.2f;
    
    [Header("Particle Effects")]
    public ParticleSystem walkParticles;
    public float particleEmissionSpeed = 2f;
    
    Rigidbody2D rb;
    Animator anim;
    SpriteRenderer sr;
    WalkParticleController particleController;
    Health health;
    
    bool isGrounded;
    bool wasGrounded;
    float moveInput;
    float currentSpeed;
    float coyoteTimeCounter;
    float jumpBufferCounter;
    float landingTimer;
    bool isLanding;
    
    bool isDashing;
    float dashTimer;
    float dashCooldownTimer;
    int dashDirection;
    
    bool isDead;
    
    float airborneTime;
    
    const float FALL_THRESHOLD = -0.1f;
    const float MOVE_THRESHOLD = 0.01f;
    const float MIN_AIRBORNE_FOR_LANDING = 0.12f;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        particleController = GetComponent<WalkParticleController>();
        health = GetComponent<Health>();
        
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
        
        if (health != null)
        {
            health.OnDeath.AddListener(OnDeath);
        }
    }
    
    void Update()
    {
        if (isDead) return;
        
        HandleInput();
        UpdateTimers();
        HandleJump();
        HandleDash();
        UpdateAnimationState();
        FlipSprite();
        UpdateParticles();
    }
    
    void FixedUpdate()
    {
        if (isDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        
        CheckGround();
        ApplyMovement();
    }
    
    void HandleInput()
    {
        if (!isDashing)
        {
            moveInput = Input.GetAxisRaw("Horizontal");
        }
        
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (dashCooldownTimer <= 0f && !isDashing && Mathf.Abs(moveInput) > 0.1f)
            {
                StartDash();
            }
        }
    }
    
    void UpdateTimers()
    {
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
        
        jumpBufferCounter -= Time.deltaTime;
        dashCooldownTimer -= Time.deltaTime;
        
        if (isLanding)
        {
            landingTimer -= Time.deltaTime;
            if (landingTimer <= 0f)
            {
                isLanding = false;
            }
        }
        
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
            }
        }
    }
    
    void ApplyMovement()
    {
        if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDirection * dashSpeed, rb.linearVelocity.y);
        }
        else if (!isLanding)
        {
            float targetSpeed = moveInput * moveSpeed;
            float speedDiff = targetSpeed - currentSpeed;
            float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;
            float movement = speedDiff * accelRate * Time.fixedDeltaTime;
            
            currentSpeed += movement;
            currentSpeed = Mathf.Clamp(currentSpeed, -moveSpeed, moveSpeed);
            
            rb.linearVelocity = new Vector2(currentSpeed, rb.linearVelocity.y);
        }
    }
    
    void HandleJump()
    {
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f && !isDashing)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;
            isLanding = false;
        }
    }
    
    void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        dashDirection = moveInput > 0 ? 1 : -1;
        
        if (particleController != null && isGrounded)
        {
            particleController.EmitParticles(dashDirection, true);
        }
    }
    
    void HandleDash()
    {
        if (anim != null)
        {
            anim.SetBool("isDashing", isDashing);
        }
    }
    
    void CheckGround()
    {
        wasGrounded = isGrounded;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer);
        
        if (!isGrounded)
        {
            airborneTime += Time.fixedDeltaTime;
        }
        
        if (!wasGrounded && isGrounded && rb.linearVelocity.y <= 0f && airborneTime >= MIN_AIRBORNE_FOR_LANDING)
        {
            isLanding = true;
            landingTimer = landingDuration;
            
            if (particleController != null)
            {
                particleController.EmitParticles(moveInput, false);
            }
        }
        
        if (isGrounded)
        {
            airborneTime = 0f;
        }
    }
    
    void UpdateAnimationState()
    {
        bool isMoving = Mathf.Abs(moveInput) > MOVE_THRESHOLD;
        bool isFalling = rb.linearVelocity.y < FALL_THRESHOLD && !isGrounded;
        bool isJumping = rb.linearVelocity.y > FALL_THRESHOLD && !isGrounded;
        
        anim.SetBool("isGrounded", isGrounded);
        anim.SetBool("isRunning", isMoving && isGrounded && !isLanding && !isDashing);
        anim.SetBool("isFalling", isFalling);
        anim.SetBool("isJumping", isJumping);
        anim.SetBool("isLanding", isLanding);
        anim.SetBool("isDead", isDead);
        
        anim.SetFloat("yVelocity", rb.linearVelocity.y);
        anim.SetFloat("xVelocity", Mathf.Abs(rb.linearVelocity.x));
    }
    
    void FlipSprite()
    {
        if (isDead) return;
        
        if (moveInput > 0.01f)
        {
            sr.flipX = false;
        }
        else if (moveInput < -0.01f)
        {
            sr.flipX = true;
        }
    }
    
    void UpdateParticles()
    {
        if (particleController != null && isGrounded && Mathf.Abs(rb.linearVelocity.x) > 0.5f && !isLanding && !isDashing)
        {
            particleController.EmitParticles(rb.linearVelocity.x, false);
        }
    }
    
    void OnDeath()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        
        if (anim != null)
        {
            anim.SetBool("isDead", true);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        }
    }
}