using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class EscapeToHomeManager : MonoBehaviour
{
    [SerializeField] private string homeSceneName = "Home";

    private static EscapeToHomeManager instance;
    private bool isLoading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("EscapeToHomeManager");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<EscapeToHomeManager>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isLoading = false;
    }

    private void Update()
    {
        if (isLoading)
        {
            return;
        }

        if (!WasEscapePressedThisFrame())
        {
            return;
        }

        string currentSceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(currentSceneName, homeSceneName, System.StringComparison.Ordinal))
        {
            return;
        }

        isLoading = true;
        SceneManager.LoadScene(homeSceneName);
    }

    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }
}
