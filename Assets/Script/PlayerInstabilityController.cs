using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerInstabilityController : MonoBehaviour
{
    // Kept for compatibility with existing scripts. Liquid/Gas are disabled.
    public enum FormState
    {
        Solid,
        Liquid,
        Gas
    }

    [Serializable]
    public class StateTuning
    {
        [Header("Movement")]
        public float moveSpeed = 6f;
        public float acceleration = 25f;
        [Min(0f)] public float jumpForce = 8f;

        [Header("Body")]
        public float mass = 1f;
        public float gravityScale = 3f;
        public float linearDamping = 0f;
        public float angularDamping = 0.05f;

        [Header("Collider")]
        public PhysicsMaterial2D colliderMaterial;
    }

    [Header("Solid Tuning")]
    [SerializeField] private StateTuning solid = new StateTuning
    {
        moveSpeed = 4f,
        acceleration = 18f,
        jumpForce = 8f,
        mass = 3f,
        gravityScale = 4.5f,
        linearDamping = 0.2f
    };

    [Header("Instability")]
    [SerializeField, Range(0f, 100f)] private float instabilityValue;
    [SerializeField] private float instabilityCooldownPerSecond = 16f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.12f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Respawn")]
    [SerializeField] private float killY = -9f;
    [SerializeField] private Vector3 respawnPosition = Vector3.zero;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Color solidColor = new Color(0.75f, 0.95f, 1f, 1f);
    [SerializeField] private ParticleSystem solidVfx;

    [Header("Animation")]
    [SerializeField] private bool driveAnimator = true;
    [SerializeField] private Animator bodyAnimator;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string groundedParam = "Grounded";
    [SerializeField] private string formParam = "Form";

    [Header("HUD")]
    [SerializeField] private bool showInstabilityHud = true;
    [SerializeField] private Vector2 hudAnchor = new Vector2(24f, 24f);
    [SerializeField] private Vector2 hudSize = new Vector2(260f, 18f);
    [SerializeField, Range(0.5f, 3f)] private float hudScale = 1f;
    [SerializeField, Range(10, 36)] private int hudFontSize = 16;

#if ENABLE_INPUT_SYSTEM
    [Header("Input Actions (Optional)")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string jumpActionName = "Jump";
#endif

    public event Action<FormState, FormState> OnStateChanged;

    public float InstabilityValue => instabilityValue;
    public float InstabilityPercent => instabilityValue / 100f;
    public FormState CurrentForm => FormState.Solid;

    public float SolidJumpPower
    {
        get => solid.jumpForce;
        set => solid.jumpForce = Mathf.Max(0f, value);
    }

    // Compatibility properties: map to solid jump power.
    public float LiquidJumpPower
    {
        get => solid.jumpForce;
        set => solid.jumpForce = Mathf.Max(0f, value);
    }

    public float GasJumpPower
    {
        get => solid.jumpForce;
        set => solid.jumpForce = Mathf.Max(0f, value);
    }

    public bool CanPushHeavy => true;
    public bool CanBreakFragilePlatforms => true;
    public bool CanUseVentPassage => false;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private PhysicsMaterial2D defaultColliderMaterial;
    private float horizontalInput;
    private bool jumpPressed;
    private bool inputLocked;
    private bool isGrounded;
    private bool groundJumpConsumed;
    private GUIStyle hudLabelStyle;

#if ENABLE_INPUT_SYSTEM
    private InputAction moveAction;
    private InputAction jumpAction;
#endif

    private const float MinMoveThreshold = 0.05f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        defaultColliderMaterial = bodyCollider != null ? bodyCollider.sharedMaterial : null;

        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (bodyAnimator == null)
        {
            bodyAnimator = GetComponentInChildren<Animator>();
        }

#if ENABLE_INPUT_SYSTEM
        CacheInputActions();
#endif

        ApplySolidTuning();
        ApplySolidVisuals();
    }

    private void Update()
    {
        CheckFallDeath();
        HandleInput();
        UpdateInstability(Time.deltaTime);
        UpdateAnimatorParameters();
    }

    private void FixedUpdate()
    {
        isGrounded = IsGrounded();

        if (isGrounded && rb.linearVelocity.y <= 0.05f)
        {
            groundJumpConsumed = false;
        }

        ApplyHorizontalMovement(Time.fixedDeltaTime);
        TryConsumeJump();
    }

    public bool TrySwitchState(FormState newForm)
    {
        // Liquid/Gas system removed by request.
        return false;
    }

    public void SetJumpPower(FormState form, float jumpPower)
    {
        solid.jumpForce = Mathf.Max(0f, jumpPower);
    }

    public float GetJumpPower(FormState form)
    {
        return solid.jumpForce;
    }

    private void ApplySolidTuning()
    {
        rb.mass = solid.mass;
        rb.gravityScale = solid.gravityScale;
        rb.linearDamping = solid.linearDamping;
        rb.angularDamping = solid.angularDamping;

        if (bodyCollider != null)
        {
            bodyCollider.sharedMaterial = solid.colliderMaterial != null ? solid.colliderMaterial : defaultColliderMaterial;
        }
    }

    private void ApplySolidVisuals()
    {
        if (bodyRenderer != null)
        {
            bodyRenderer.color = solidColor;
        }

        if (solidVfx != null && !solidVfx.isPlaying)
        {
            solidVfx.Play();
        }
    }

    private void HandleInput()
    {
        if (inputLocked)
        {
            horizontalInput = 0f;
            jumpPressed = false;
            return;
        }

        horizontalInput = ReadHorizontalInput();
        jumpPressed |= ReadJumpPressed();
    }

    private float ReadHorizontalInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (moveAction != null)
        {
            Vector2 moveVector = moveAction.ReadValue<Vector2>();
            if (Mathf.Abs(moveVector.x) > MinMoveThreshold)
            {
                return Mathf.Clamp(moveVector.x, -1f, 1f);
            }

            float moveAxis = moveAction.ReadValue<float>();
            if (Mathf.Abs(moveAxis) > MinMoveThreshold)
            {
                return Mathf.Clamp(moveAxis, -1f, 1f);
            }
        }

        float axis = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                axis -= 1f;
            }

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                axis += 1f;
            }
        }

        if (Mathf.Abs(axis) > MinMoveThreshold)
        {
            return Mathf.Clamp(axis, -1f, 1f);
        }

        if (Gamepad.current != null)
        {
            float gamepadAxis = Gamepad.current.leftStick.ReadValue().x;
            if (Mathf.Abs(gamepadAxis) <= MinMoveThreshold)
            {
                gamepadAxis = Gamepad.current.dpad.ReadValue().x;
            }

            return Mathf.Abs(gamepadAxis) <= MinMoveThreshold ? 0f : Mathf.Clamp(gamepadAxis, -1f, 1f);
        }

        return 0f;
#else
        return Input.GetAxisRaw("Horizontal");
#endif
    }

    private bool ReadJumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (jumpAction != null && jumpAction.WasPressedThisFrame())
        {
            return true;
        }

        bool keyboardJump = Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame);
        bool gamepadJump = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        return keyboardJump || gamepadJump;
#else
        return Input.GetButtonDown("Jump");
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void CacheInputActions()
    {
        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        if (playerInput == null || playerInput.actions == null)
        {
            return;
        }

        moveAction = playerInput.actions.FindAction(moveActionName, false);
        jumpAction = playerInput.actions.FindAction(jumpActionName, false);
    }
#endif

    private void ApplyHorizontalMovement(float dt)
    {
        float targetX = horizontalInput * solid.moveSpeed;
        rb.linearVelocity = new Vector2(targetX, rb.linearVelocity.y);
    }

    private void TryConsumeJump()
    {
        if (!jumpPressed)
        {
            return;
        }

        jumpPressed = false;

        if (!isGrounded || groundJumpConsumed)
        {
            return;
        }

        groundJumpConsumed = true;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, solid.jumpForce);
    }

    private bool IsGrounded()
    {
        if (groundCheck != null)
        {
            return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask) != null;
        }

        if (bodyCollider == null)
        {
            return false;
        }

        Vector3 checkPosition = bodyCollider.bounds.min + new Vector3(bodyCollider.bounds.extents.x, 0f, 0f);
        return Physics2D.OverlapCircle(checkPosition, groundCheckRadius, groundMask) != null;
    }

    private void UpdateInstability(float dt)
    {
        if (!isGrounded)
        {
            return;
        }

        bool standingStill = Mathf.Abs(horizontalInput) <= MinMoveThreshold
            && Mathf.Abs(rb.linearVelocity.x) <= MinMoveThreshold
            && Mathf.Abs(rb.linearVelocity.y) <= MinMoveThreshold;

        if (standingStill)
        {
            instabilityValue = Mathf.Max(0f, instabilityValue - instabilityCooldownPerSecond * dt);
        }
    }

    private void CheckFallDeath()
    {
        if (rb.position.y >= killY)
        {
            return;
        }

        RespawnPlayer();
    }

    private void RespawnPlayer()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = respawnPosition;
        transform.position = respawnPosition;

        instabilityValue = 0f;
        horizontalInput = 0f;
        jumpPressed = false;
        isGrounded = false;
        groundJumpConsumed = false;

        OnStateChanged?.Invoke(FormState.Solid, FormState.Solid);
    }

    private void UpdateAnimatorParameters()
    {
        if (!driveAnimator || bodyAnimator == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(speedParam))
        {
            bodyAnimator.SetFloat(speedParam, Mathf.Abs(rb.linearVelocity.x));
        }

        if (!string.IsNullOrEmpty(groundedParam))
        {
            bodyAnimator.SetBool(groundedParam, isGrounded);
        }

        if (!string.IsNullOrEmpty(formParam))
        {
            bodyAnimator.SetInteger(formParam, (int)FormState.Solid);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        if (groundCheck != null)
        {
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
            return;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            return;
        }

        Vector3 checkPosition = col.bounds.min + new Vector3(col.bounds.extents.x, 0f, 0f);
        Gizmos.DrawWireSphere(checkPosition, groundCheckRadius);
    }

    private void OnGUI()
    {
        if (!showInstabilityHud)
        {
            return;
        }

        if (hudLabelStyle == null)
        {
            hudLabelStyle = new GUIStyle(GUI.skin.label)
            {
                richText = false,
                alignment = TextAnchor.MiddleLeft
            };
        }

        float scale = Mathf.Max(0.1f, hudScale);
        hudLabelStyle.fontSize = Mathf.RoundToInt(hudFontSize * scale);
        hudLabelStyle.normal.textColor = Color.black;

        float x = hudAnchor.x;
        float y = hudAnchor.y;
        float width = Mathf.Max(1f, hudSize.x * scale);
        float height = Mathf.Max(1f, hudSize.y * scale);
        float percent = Mathf.Clamp01(InstabilityPercent);
        string labelText = "Instability: " + Mathf.RoundToInt(instabilityValue) + "% [Solid]";

        float labelPadding = 6f * scale;
        float labelHeight = Mathf.Max(
            hudLabelStyle.CalcHeight(new GUIContent(labelText), width),
            hudLabelStyle.fontSize + (4f * scale));
        float labelY = y - labelPadding - labelHeight;

        Rect labelRect = new Rect(x, labelY, width, labelHeight);
        Rect bgRect = new Rect(x, y, width, height);
        Rect fillRect = new Rect(x, y, width * percent, height);
        Rect borderRect = new Rect(x - scale, y - scale, width + (2f * scale), height + (2f * scale));

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(borderRect, Texture2D.whiteTexture);

        GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

        Color low = new Color(0.20f, 0.85f, 0.35f, 1f);
        Color high = new Color(0.95f, 0.15f, 0.15f, 1f);
        GUI.color = Color.Lerp(low, high, percent);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        GUI.color = Color.black;
        GUI.Label(labelRect, labelText, hudLabelStyle);
    }
}
