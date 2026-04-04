using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalLevelLoader : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private string nextSceneName = "Level2";

    [Header("Activation")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool useTrigger = true;
    [SerializeField] private float loadDelay = 0f;

    private bool hasLoaded;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!useTrigger)
        {
            return;
        }

        TryLoad(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (useTrigger)
        {
            return;
        }

        TryLoad(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useTrigger)
        {
            return;
        }

        TryLoad(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (useTrigger)
        {
            return;
        }

        TryLoad(collision.gameObject);
    }

    private void TryLoad(GameObject candidate)
    {
        if (hasLoaded)
        {
            return;
        }

        if (candidate == null || !candidate.CompareTag(playerTag))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning("PortalLevelLoader has no target scene set.");
            return;
        }

        hasLoaded = true;

        if (loadDelay > 0f)
        {
            Invoke(nameof(LoadTargetScene), loadDelay);
            return;
        }

        LoadTargetScene();
    }

    private void LoadTargetScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}
