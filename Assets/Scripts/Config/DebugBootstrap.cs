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
        Debug.Log("[DebugBootstrap] SkipLogin ativo — redirecionando para: " + DebugConfig.SkipLoginTargetScene);
        SceneManager.LoadScene(DebugConfig.SkipLoginTargetScene);
    }
#endif
}
