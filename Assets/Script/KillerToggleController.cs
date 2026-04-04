using System.Collections.Generic;
using UnityEngine;

public class KillerToggleController : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private GameObject activationObject;
    [SerializeField] private GameObject activationTouchObject;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool isDeadly = true;
    [SerializeField] private bool startHidden = true;
    [SerializeField] private bool showWhenObjectReEnabled = true;

    private static readonly HashSet<KillerToggleController> AllKillers = new HashSet<KillerToggleController>();
    private static bool globalVisible;
    private static bool globalStateInitialized;

    private KillerActivationTouchRelay relay;
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders3D;
    private Collider2D[] cachedColliders2D;
    private bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        AllKillers.Clear();
        globalVisible = false;
        globalStateInitialized = false;
    }

    private void Update()
    {
        if (initialized && showWhenObjectReEnabled && gameObject.activeInHierarchy)
        {
            globalVisible = true;
        }
    }

    private void RegisterSelf()
    {
        AllKillers.Add(this);

        if (!globalStateInitialized)
        {
            globalVisible = !startHidden;
            globalStateInitialized = true;
        }
    }

    private void Awake()
    {
        CacheHideableComponents();
        EnsureRelay();
    }

    private void OnEnable()
    {
        RegisterSelf();

        if (initialized && showWhenObjectReEnabled)
        {
            SetAllKillersVisible(true);
            return;
        }

        ApplyVisibleState(globalVisible);
        initialized = true;
    }

    private void OnDisable()
    {
        AllKillers.Remove(this);
        UnsubscribeRelay();
    }

    private void OnDestroy()
    {
        AllKillers.Remove(this);
        UnsubscribeRelay();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDeadly && other.CompareTag(playerTag))
        {
            TryRespawnPlayer(other.gameObject);
            SetAllKillersVisible(false);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDeadly && collision.gameObject.CompareTag(playerTag))
        {
            TryRespawnPlayer(collision.gameObject);
            SetAllKillersVisible(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDeadly && other.CompareTag(playerTag))
        {
            TryRespawnPlayer(other.gameObject);
            SetAllKillersVisible(false);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDeadly && collision.gameObject.CompareTag(playerTag))
        {
            TryRespawnPlayer(collision.gameObject);
            SetAllKillersVisible(false);
        }
    }

    private static void TryRespawnPlayer(GameObject playerObject)
    {
        if (playerObject == null)
        {
            return;
        }

        PlayerInstabilityController controller = playerObject.GetComponent<PlayerInstabilityController>();
        if (controller == null)
        {
            controller = playerObject.GetComponentInParent<PlayerInstabilityController>();
        }

        if (controller != null)
        {
            controller.ForceRespawn();
        }
    }

    private void EnsureRelay()
    {
        GameObject relayHost = activationTouchObject != null ? activationTouchObject : activationObject;

        if (relayHost == null)
        {
            Debug.LogWarning($"{nameof(KillerToggleController)} on {name} is missing an activation object.");
            return;
        }

        relay = relayHost.GetComponent<KillerActivationTouchRelay>();
        if (relay == null)
        {
            relay = relayHost.AddComponent<KillerActivationTouchRelay>();
        }

        relay.SetPlayerTag(playerTag);
        relay.PlayerTouched -= HandleActivationTouched;
        relay.PlayerTouched += HandleActivationTouched;
    }

    private void UnsubscribeRelay()
    {
        if (relay != null)
        {
            relay.PlayerTouched -= HandleActivationTouched;
        }
    }

    private void HandleActivationTouched(GameObject touchedBy)
    {
        if (touchedBy != null && touchedBy.CompareTag(playerTag))
        {
            ApplyVisibleState(true);
        }
    }

    private static void SetAllKillersVisible(bool visible)
    {
        globalVisible = visible;

        foreach (KillerToggleController killer in AllKillers)
        {
            if (killer != null)
            {
                killer.ApplyVisibleState(visible);
            }
        }
    }

    private void ApplyVisibleState(bool visible)
    {
        if (cachedRenderers == null || cachedColliders2D == null || cachedColliders3D == null)
        {
            CacheHideableComponents();
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = visible;
            }
        }

        for (int i = 0; i < cachedColliders2D.Length; i++)
        {
            if (cachedColliders2D[i] != null)
            {
                cachedColliders2D[i].enabled = visible;
            }
        }

        for (int i = 0; i < cachedColliders3D.Length; i++)
        {
            if (cachedColliders3D[i] != null)
            {
                cachedColliders3D[i].enabled = visible;
            }
        }
    }

    private void CacheHideableComponents()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders2D = GetComponentsInChildren<Collider2D>(true);
        cachedColliders3D = GetComponentsInChildren<Collider>(true);
    }
}

public class KillerActivationTouchRelay : MonoBehaviour
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

    private void OnTriggerStay2D(Collider2D other)
    {
        NotifyIfPlayer(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        NotifyIfPlayer(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        NotifyIfPlayer(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyIfPlayer(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        NotifyIfPlayer(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        NotifyIfPlayer(collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
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
