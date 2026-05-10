using UnityEngine;
using UnityEngine.SceneManagement;

public static class DebugBootstrap
{
    private const string LoginSceneName = "Login";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        if (!DebugConfig.SkipLogin) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != LoginSceneName) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;

        Debug.Log("[DebugBootstrap] SkipLogin ativo — autenticando usuario de teste...");

        var go = new GameObject("DebugBootstrapRunner");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<DebugBootstrapRunner>();
    }
#endif
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public class DebugBootstrapRunner : MonoBehaviour
{
    private void Start()
    {
        if (PlayFabService.Instance == null)
        {
            var go = new GameObject("PlayFabService");
            go.AddComponent<PlayFabService>();
            DontDestroyOnLoad(go);
        }

        PlayFabService.Instance.Initialize();

        PlayFabService.OnLoginSuccess += OnSuccess;
        PlayFabService.OnLoginFailure += OnFailure;

        var email    = DebugConfig.DebugEmail?.Trim();
        var password = DebugConfig.DebugPassword;

        if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
        {
            Debug.Log("[DebugBootstrap] Autenticando com email de debug...");
            PlayFabService.Instance.LoginWithEmail(email, password);
        }
        else
        {
            Debug.Log("[DebugBootstrap] Autenticando com CustomId de teste...");
            PlayFabService.Instance.LoginWithCustomId(PlayFabConfig.GetTestUserId());
        }
    }

    private void OnSuccess()
    {
        Cleanup();
        Debug.Log("[DebugBootstrap] Autenticado — carregando: " + DebugConfig.SkipLoginTargetScene);
        SceneManager.LoadScene(DebugConfig.SkipLoginTargetScene);
    }

    private void OnFailure(PlayFab.PlayFabError error)
    {
        Cleanup();
        Debug.LogError("[DebugBootstrap] Falha na autenticacao de teste: " + error.GenerateErrorReport());
        SceneManager.LoadScene(DebugConfig.SkipLoginTargetScene);
    }

    private void Cleanup()
    {
        PlayFabService.OnLoginSuccess -= OnSuccess;
        PlayFabService.OnLoginFailure -= OnFailure;
        Destroy(gameObject);
    }
}
#endif
