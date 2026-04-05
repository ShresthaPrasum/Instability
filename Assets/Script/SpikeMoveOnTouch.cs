using UnityEngine;

public class SpikeMoveOnTouch : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private GameObject activationObject;
    [SerializeField] private string triggerTag = "Player";
    [SerializeField, Min(0f)] private float movementStartDelay = 0f;

    [Header("Movement")]
    [SerializeField] private Vector3 moveOffset = new Vector3(0f, 3f, 0f);
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private bool moveInLocalSpace;

    [Header("Disappearance")]
    [SerializeField] private bool startDisabled = true;
    [SerializeField] private float disappearDelay = 1f;
    [SerializeField] private bool disableRenderer = true;
    [SerializeField] private bool disableCollider = true;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private bool hasStartedMoving;
    private bool waitingToStartMoving;
    private bool hasDisappeared;
    private float disappearTimer;
    private float movementStartTimer;
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders3D;
    private Collider2D[] cachedColliders2D;
    private SpikeActivationRelay activationRelay;
    private PlayerInstabilityController playerController;

    private void Awake()
    {
        startPosition = transform.position;
        targetPosition = moveInLocalSpace ? startPosition + transform.TransformVector(moveOffset) : startPosition + moveOffset;
        hasStartedMoving = false;
        waitingToStartMoving = false;
        hasDisappeared = false;
        disappearTimer = 0f;
        movementStartTimer = 0f;

        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders3D = GetComponentsInChildren<Collider>(true);
        cachedColliders2D = GetComponentsInChildren<Collider2D>(true);

        GameObject relayHost = activationObject != null ? activationObject : gameObject;
        activationRelay = relayHost.GetComponent<SpikeActivationRelay>();

        if (activationRelay == null)
        {
            activationRelay = relayHost.AddComponent<SpikeActivationRelay>();
        }

        activationRelay.SetPlayerTag(triggerTag);
        activationRelay.PlayerTouched += HandleActivationTouched;

        playerController = FindFirstObjectByType<PlayerInstabilityController>();
        if (playerController != null)
        {
            playerController.OnRespawn += HandlePlayerRespawned;
        }

        EnableSpikeVisualsAndColliders(!startDisabled);
    }

    private void OnDestroy()
    {
        if (activationRelay != null)
        {
            activationRelay.PlayerTouched -= HandleActivationTouched;
        }

        if (playerController != null)
        {
            playerController.OnRespawn -= HandlePlayerRespawned;
        }
    }

    private void Update()
    {
        if (hasDisappeared)
        {
            return;
        }

        if (waitingToStartMoving)
        {
            movementStartTimer += Time.deltaTime;
            if (movementStartTimer >= movementStartDelay)
            {
                waitingToStartMoving = false;
                hasStartedMoving = true;
                movementStartTimer = 0f;
            }
        }

        if (hasStartedMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            if ((transform.position - targetPosition).sqrMagnitude <= 0.0001f)
            {
                disappearTimer += Time.deltaTime;
                if (disappearTimer >= disappearDelay)
                {
                    Disappear();
                }
            }
        }
    }

    private void HandleActivationTouched(GameObject touchedObject)
    {
        if (hasStartedMoving || hasDisappeared)
        {
            return;
        }

        EnableSpikeVisualsAndColliders(true);

        if (movementStartDelay <= 0f)
        {
            hasStartedMoving = true;
            waitingToStartMoving = false;
            movementStartTimer = 0f;
            return;
        }

        waitingToStartMoving = true;
        hasStartedMoving = false;
        movementStartTimer = 0f;
    }

    private void HandlePlayerRespawned(Vector3 respawnPosition)
    {
        transform.position = startPosition;
        targetPosition = moveInLocalSpace ? startPosition + transform.TransformVector(moveOffset) : startPosition + moveOffset;
        hasStartedMoving = false;
        waitingToStartMoving = false;
        hasDisappeared = false;
        disappearTimer = 0f;
        movementStartTimer = 0f;

        EnableSpikeVisualsAndColliders(false);
    }

    private void EnableSpikeVisualsAndColliders(bool enabled)
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = enabled;
            }
        }

        for (int i = 0; i < cachedColliders3D.Length; i++)
        {
            if (cachedColliders3D[i] != null)
            {
                cachedColliders3D[i].enabled = enabled;
            }
        }

        for (int i = 0; i < cachedColliders2D.Length; i++)
        {
            if (cachedColliders2D[i] != null)
            {
                cachedColliders2D[i].enabled = enabled;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryRestartPlayer(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryRestartPlayer(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRestartPlayer(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryRestartPlayer(collision.gameObject);
    }

    private void TryRestartPlayer(GameObject candidate)
    {
        if (candidate == null)
        {
            return;
        }

        PlayerInstabilityController controller = candidate.GetComponent<PlayerInstabilityController>();
        if (controller == null)
        {
            controller = candidate.GetComponentInParent<PlayerInstabilityController>();
        }

        if (controller != null)
        {
            controller.ForceRespawnFromStart();
        }
    }

    private void Disappear()
    {
        hasDisappeared = true;

        if (disableRenderer)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null)
                {
                    cachedRenderers[i].enabled = false;
                }
            }
        }

        if (disableCollider)
        {
            for (int i = 0; i < cachedColliders3D.Length; i++)
            {
                if (cachedColliders3D[i] != null)
                {
                    cachedColliders3D[i].enabled = false;
                }
            }

            for (int i = 0; i < cachedColliders2D.Length; i++)
            {
                if (cachedColliders2D[i] != null)
                {
                    cachedColliders2D[i].enabled = false;
                }
            }
        }

        gameObject.SetActive(false);
    }
}

public class SpikeActivationRelay : MonoBehaviour
{
    public event System.Action<GameObject> PlayerTouched;

    [SerializeField] private string playerTag = "Player";

    public void SetPlayerTag(string tagName)
    {
        if (!string.IsNullOrWhiteSpace(tagName))
        {
            playerTag = tagName;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        NotifyIfPlayer(other.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyIfPlayer(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        NotifyIfPlayer(collision.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        NotifyIfPlayer(collision.gameObject);
    }

    private void NotifyIfPlayer(GameObject candidate)
    {
        if (candidate != null && candidate.CompareTag(playerTag))
        {
            PlayerTouched?.Invoke(candidate);
        }
    }
}
