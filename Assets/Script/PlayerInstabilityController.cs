using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerInstabilityController : MonoBehaviour
{
    public event Action<Vector3> OnRespawn;

    public enum FormState
    {
        Solid,
        Liquid
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
    [SerializeField] private bool useCurrentPositionAsInitialRespawn = true;
    [SerializeField] private string checkpointTag = "Checkpoint";

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Color solidColor = new Color(0.75f, 0.95f, 1f, 1f);
    [SerializeField] private ParticleSystem solidVfx;

    [Header("Animation")]
    [SerializeField] private bool driveAnimator = true;
    [SerializeField] private Animator bodyAnimator;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string groundedParam = "Grounded";

    [Header("Slingshot")]
    [SerializeField] private float launchForce = 25f;
    [SerializeField] private float maxDragDistance = 4f;
    [SerializeField] private float launchCooldown = 0.1f;
    [SerializeField] private LineRenderer trajectoryLine;

    [Header("Squash & Stretch")]
    [SerializeField] private float squashAmount = 1.2f;
    [SerializeField] private float stretchAmount = 0.8f;
    [SerializeField] private float squashDuration = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip launchSound;

    [Header("HUD")]
    [SerializeField] private bool showInstabilityHud = true;
    [SerializeField] private Vector2 hudAnchor = new Vector2(24f, 24f);
    [SerializeField] private Vector2 hudSize = new Vector2(260f, 18f);
    [SerializeField, Range(0.5f, 3f)] private float hudScale = 1f;
    [SerializeField, Range(10, 36)] private int hudFontSize = 16;

#if ENABLE_INPUT_SYSTEM
#endif

    public float InstabilityValue => instabilityValue;
    public float InstabilityPercent => instabilityValue / 100f;
    public FormState CurrentForm => FormState.Solid;

    public float SolidJumpPower
    {
        get => solid.jumpForce;
        set => solid.jumpForce = Mathf.Max(0f, value);
    }

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private PhysicsMaterial2D defaultColliderMaterial;
    private bool inputLocked;
    private bool isGrounded;
    private GUIStyle hudLabelStyle;
    private Camera mainCam;
    private Vector3 normalScale;
    private Vector3 startRespawnPosition;
    private AudioSource audioSource;
    private float cooldownTimer;
    private float squashTimer;
    private Transform lastCheckpointTransform;

    private bool isDragging;
    private Vector2 dragStart;

#if ENABLE_INPUT_SYSTEM
    private InputAction moveAction;
    private InputAction jumpAction;
#endif

    private const float MinMoveThreshold = 0.05f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        bodyCollider = GetComponent<Collider2D>();
        defaultColliderMaterial = bodyCollider != null ? bodyCollider.sharedMaterial : null;
        mainCam = Camera.main;
        normalScale = transform.localScale;

        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (bodyAnimator == null)
        {
            bodyAnimator = GetComponentInChildren<Animator>();
        }

        if (useCurrentPositionAsInitialRespawn)
        {
            respawnPosition = transform.position;
        }

        startRespawnPosition = respawnPosition;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = 0.3f;

        if (FindFirstObjectByType<AudioListener>() == null)
        {
            if (mainCam != null)
                mainCam.gameObject.AddComponent<AudioListener>();
        }

        if (trajectoryLine == null)
        {
            trajectoryLine = gameObject.AddComponent<LineRenderer>();
            trajectoryLine.startWidth = 0.1f;
            trajectoryLine.endWidth = 0.2f;
            trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
            trajectoryLine.startColor = Color.red;
            trajectoryLine.endColor = Color.yellow;
        }
        trajectoryLine.positionCount = 0;

        ApplySolidTuning();
        ApplySolidVisuals();
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (squashTimer > 0f)
        {
            squashTimer -= Time.deltaTime;
            float progress = squashTimer / squashDuration;
            float squashX = Mathf.Lerp(1f, squashAmount, progress);
            float squashY = Mathf.Lerp(1f, stretchAmount, progress);
            transform.localScale = new Vector3(normalScale.x * squashX, normalScale.y * squashY, normalScale.z);
        }
        else
        {
            transform.localScale = normalScale;
        }

        CheckFallDeath();
        HandleSlingshotInput();
        UpdateInstability(Time.deltaTime);
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        if (!driveAnimator || bodyAnimator == null)
        {
            return;
        }

        float speed = rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
        bodyAnimator.SetFloat(speedParam, speed);
        bodyAnimator.SetBool(groundedParam, isGrounded);
    }

    private void FixedUpdate()
    {
        isGrounded = IsGrounded();
    }

    public void SetCheckpoint(Vector3 checkpointPosition)
    {
        respawnPosition = checkpointPosition;
    }

    public void ForceRespawn()
    {
        RespawnPlayer();
    }

    public void ForceRespawnFromStart()
    {
        RespawnPlayer(startRespawnPosition);
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

    private void HandleSlingshotInput()
    {
        if (inputLocked)
            return;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return;

        Vector2 mouseWorld = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        if (Mouse.current.leftButton.wasPressedThisFrame && cooldownTimer <= 0f)
        {
            isDragging = true;
            dragStart = mouseWorld;
        }

        if (isDragging)
        {
            Vector2 dragCurrent = mouseWorld;
            Vector2 dragDelta = dragStart - dragCurrent;

            if (dragDelta.magnitude > maxDragDistance)
                dragDelta = dragDelta.normalized * maxDragDistance;

            Vector2 launchDir = dragDelta.normalized;
            float power = (dragDelta.magnitude / maxDragDistance) * launchForce;

            trajectoryLine.positionCount = 2;
            trajectoryLine.SetPosition(0, (Vector2)transform.position);
            trajectoryLine.SetPosition(1, (Vector2)transform.position + launchDir * (power / launchForce) * 2f);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
        {
            isDragging = false;
            trajectoryLine.positionCount = 0;

            Vector2 dragEnd = mouseWorld;
            Vector2 dragDelta = dragStart - dragEnd;

            if (dragDelta.magnitude > maxDragDistance)
                dragDelta = dragDelta.normalized * maxDragDistance;

            float power = (dragDelta.magnitude / maxDragDistance) * launchForce;
            Vector2 launchDir = dragDelta.normalized;

            rb.linearVelocity = Vector2.zero;
            rb.AddForce(launchDir * power, ForceMode2D.Impulse);

            if (launchSound != null && audioSource != null)
                audioSource.PlayOneShot(launchSound);

            squashTimer = squashDuration;

            float chargePercent = dragDelta.magnitude / maxDragDistance;
            instabilityValue = Mathf.Min(100f, instabilityValue + (chargePercent * 30f));

            cooldownTimer = launchCooldown;
        }
#else
        Vector2 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);

        if (Input.GetMouseButtonDown(0) && cooldownTimer <= 0f)
        {
            isDragging = true;
            dragStart = mouseWorld;
        }

        if (isDragging)
        {
            Vector2 dragCurrent = mouseWorld;
            Vector2 dragDelta = dragStart - dragCurrent;

            if (dragDelta.magnitude > maxDragDistance)
                dragDelta = dragDelta.normalized * maxDragDistance;

            Vector2 launchDir = dragDelta.normalized;
            float power = (dragDelta.magnitude / maxDragDistance) * launchForce;

            trajectoryLine.positionCount = 2;
            trajectoryLine.SetPosition(0, (Vector2)transform.position);
            trajectoryLine.SetPosition(1, (Vector2)transform.position + launchDir * (power / launchForce) * 2f);
        }

        if (Input.GetMouseButtonUp() && isDragging)
        {
            isDragging = false;
            trajectoryLine.positionCount = 0;

            Vector2 dragEnd = mouseWorld;
            Vector2 dragDelta = dragStart - dragEnd;

            if (dragDelta.magnitude > maxDragDistance)
                dragDelta = dragDelta.normalized * maxDragDistance;

            float power = (dragDelta.magnitude / maxDragDistance) * launchForce;
            Vector2 launchDir = dragDelta.normalized;

            rb.linearVelocity = Vector2.zero;
            rb.AddForce(launchDir * power, ForceMode2D.Impulse);

            if (launchSound != null && audioSource != null)
                audioSource.PlayOneShot(launchSound);

            squashTimer = squashDuration;
            float chargePercent = dragDelta.magnitude / maxDragDistance;
            instabilityValue = Mathf.Min(100f, instabilityValue + (chargePercent * 30f));
            cooldownTimer = launchCooldown;
        }
#endif
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
        if (isGrounded)
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        TrySetCheckpointFromCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TrySetCheckpointFromCollider(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TrySetCheckpointFromCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TrySetCheckpointFromCollision(collision);
    }

    private void TrySetCheckpointFromCollider(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        TrySetCheckpointFromObject(other.gameObject);

        if (other.attachedRigidbody != null)
        {
            TrySetCheckpointFromObject(other.attachedRigidbody.gameObject);
        }
    }

    private void TrySetCheckpointFromCollision(Collision2D collision)
    {
        if (collision == null)
        {
            return;
        }

        TrySetCheckpointFromObject(collision.gameObject);

        if (collision.rigidbody != null)
        {
            TrySetCheckpointFromObject(collision.rigidbody.gameObject);
        }
    }

    private void TrySetCheckpointFromObject(GameObject candidate)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(checkpointTag))
        {
            return;
        }

        if (candidate.CompareTag(checkpointTag))
        {
            SetCheckpointFromTransform(candidate.transform);
            return;
        }

        Transform candidateTransform = candidate.transform;
        if (candidateTransform.parent != null && candidateTransform.parent.CompareTag(checkpointTag))
        {
            SetCheckpointFromTransform(candidateTransform.parent);
            return;
        }

        Transform root = candidateTransform.root;
        if (root != null && root != candidateTransform && root.CompareTag(checkpointTag))
        {
            SetCheckpointFromTransform(root);
        }
    }

    private void SetCheckpointFromTransform(Transform checkpointTransform)
    {
        if (checkpointTransform == null)
        {
            return;
        }

        if (lastCheckpointTransform == checkpointTransform)
        {
            return;
        }

        lastCheckpointTransform = checkpointTransform;
        SetCheckpoint(checkpointTransform.position);
    }

    private void RespawnPlayer()
    {
        RespawnPlayer(respawnPosition);
    }

    private void RespawnPlayer(Vector3 targetPosition)
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = targetPosition;
        transform.position = targetPosition;
        transform.localScale = normalScale;

        instabilityValue = 0f;
        isDragging = false;
        isGrounded = false;
        squashTimer = 0f;
        cooldownTimer = 0f;
        trajectoryLine.positionCount = 0;

        OnRespawn?.Invoke(targetPosition);
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
