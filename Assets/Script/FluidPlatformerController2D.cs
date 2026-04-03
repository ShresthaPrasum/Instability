using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class FluidPlatformerController2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform groundCheck;

    [Header("Ground Detection")]
    [SerializeField] private float groundCheckRadius = 0.12f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Horizontal Movement")]
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float groundAcceleration = 70f;
    [SerializeField] private float groundDeceleration = 85f;
    [SerializeField] private float airAcceleration = 35f;
    [SerializeField] private float airDeceleration = 45f;
    [SerializeField, Range(0f, 1f)] private float airControlMultiplier = 0.65f;
    [SerializeField] private float stopSpeedEpsilon = 0.02f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float jumpCutMultiplier = 0.5f;

    [Header("Gravity Polish")]
    [SerializeField] private float fallGravityMultiplier = 2.2f;
    [SerializeField] private float lowJumpGravityMultiplier = 2.8f;
    [SerializeField] private float maxFallSpeed = -25f;

    [Header("Stability")]
    [SerializeField] private bool freezeRotationZ = true;
    [SerializeField] private bool useInterpolation = true;

    private float horizontalInput;
    private bool jumpPressedThisFrame;
    private bool jumpHeld;

    private bool isGrounded;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private bool hasJumpedSinceGrounded;

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (freezeRotationZ)
        {
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        }

        if (useInterpolation)
        {
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void Update()
    {
        ReadInput();
        UpdateTimers();
    }

    private void FixedUpdate()
    {
        CheckGrounded();
        HandleHorizontalMovement();
        HandleJump();
        ApplyBetterGravity();
        ClampHorizontalVelocity();
    }

    private void ReadInput()
    {
        horizontalInput = ReadHorizontal();
        jumpPressedThisFrame = ReadJumpPressedThisFrame();
        jumpHeld = ReadJumpHeld();
    }

    private void UpdateTimers()
    {
        if (jumpPressedThisFrame)
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if (isGrounded)
        {
            coyoteCounter = coyoteTime;
            hasJumpedSinceGrounded = false;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }
    }

    private void CheckGrounded()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void HandleHorizontalMovement()
    {
        float targetSpeed = horizontalInput * maxSpeed;
        float currentSpeed = rb.linearVelocity.x;

        bool accelerating = Mathf.Abs(targetSpeed) > stopSpeedEpsilon;
        bool groundedNow = isGrounded;

        float accel = groundedNow ? groundAcceleration : airAcceleration * airControlMultiplier;
        float decel = groundedNow ? groundDeceleration : airDeceleration * airControlMultiplier;
        float rate = accelerating ? accel : decel;

        float newX = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.fixedDeltaTime);

        if (groundedNow && Mathf.Abs(horizontalInput) <= stopSpeedEpsilon && Mathf.Abs(newX) < stopSpeedEpsilon)
        {
            newX = 0f;
        }

        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
    }

    private void HandleJump()
    {
        bool canUseBufferedJump = jumpBufferCounter > 0f;
        bool canUseCoyoteJump = coyoteCounter > 0f;
        bool canJumpNow = canUseBufferedJump && canUseCoyoteJump && !hasJumpedSinceGrounded;

        if (canJumpNow)
        {
            Vector2 v = rb.linearVelocity;
            v.y = jumpForce;
            rb.linearVelocity = v;

            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            hasJumpedSinceGrounded = true;
        }

        if (!jumpHeld && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
        }
    }

    private void ApplyBetterGravity()
    {
        float vy = rb.linearVelocity.y;
        float gravityScaleMultiplier = 1f;

        if (vy < 0f)
        {
            gravityScaleMultiplier = fallGravityMultiplier;
        }
        else if (vy > 0f && !jumpHeld)
        {
            gravityScaleMultiplier = lowJumpGravityMultiplier;
        }

        float gravityForce = Physics2D.gravity.y * (gravityScaleMultiplier - 1f) * rb.gravityScale;
        rb.AddForce(new Vector2(0f, gravityForce), ForceMode2D.Force);

        if (rb.linearVelocity.y < maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, maxFallSpeed);
        }
    }

    private void ClampHorizontalVelocity()
    {
        float clampedX = Mathf.Clamp(rb.linearVelocity.x, -maxSpeed, maxSpeed);
        rb.linearVelocity = new Vector2(clampedX, rb.linearVelocity.y);
    }

    private float ReadHorizontal()
    {
#if ENABLE_INPUT_SYSTEM
        float value = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                value -= 1f;
            }

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                value += 1f;
            }
        }

        if (Mathf.Abs(value) > 0.001f)
        {
            return Mathf.Clamp(value, -1f, 1f);
        }

        if (Gamepad.current != null)
        {
            float stick = Gamepad.current.leftStick.ReadValue().x;
            if (Mathf.Abs(stick) <= 0.001f)
            {
                stick = Gamepad.current.dpad.ReadValue().x;
            }

            return Mathf.Abs(stick) <= 0.001f ? 0f : Mathf.Clamp(stick, -1f, 1f);
        }

        return 0f;
#else
        return Input.GetAxisRaw("Horizontal");
#endif
    }

    private bool ReadJumpPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        bool keyboard = Keyboard.current != null &&
                        (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame);
        bool gamepad = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        return keyboard || gamepad;
#else
        return Input.GetButtonDown("Jump");
#endif
    }

    private bool ReadJumpHeld()
    {
#if ENABLE_INPUT_SYSTEM
        bool keyboard = Keyboard.current != null &&
                        (Keyboard.current.spaceKey.isPressed || Keyboard.current.upArrowKey.isPressed);
        bool gamepad = Gamepad.current != null && Gamepad.current.buttonSouth.isPressed;
        return keyboard || gamepad;
#else
        return Input.GetButton("Jump");
#endif
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
