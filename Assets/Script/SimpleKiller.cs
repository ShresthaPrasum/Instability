using UnityEngine;

public class SimpleKiller : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool respawnFromStart = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryKill(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryKill(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryKill(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryKill(collision.gameObject);
    }

    private void TryKill(GameObject candidate)
    {
        if (candidate == null || !candidate.CompareTag(playerTag))
        {
            return;
        }

        PlayerInstabilityController controller = candidate.GetComponent<PlayerInstabilityController>();
        if (controller == null)
        {
            controller = candidate.GetComponentInParent<PlayerInstabilityController>();
        }

        if (controller == null)
        {
            return;
        }

        if (respawnFromStart)
        {
            controller.ForceRespawnFromStart();
        }
        else
        {
            controller.ForceRespawn();
        }
    }
}
