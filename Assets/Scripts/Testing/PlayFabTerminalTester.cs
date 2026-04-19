using UnityEngine;

public abstract class PlayFabTerminalTester : MonoBehaviour
{
    [SerializeField] private bool autoBootstrapPlayFab = true;

    protected virtual void Start()
    {
        if (autoBootstrapPlayFab)
        {
            EnsurePlayFabService();
        }
    }

    protected void EnsurePlayFabService()
    {
        if (PlayFabService.Instance == null)
        {
            var playFabServiceObject = new GameObject("PlayFabService");
            playFabServiceObject.AddComponent<PlayFabService>();
        }

        if (!PlayFabService.Instance.IsLoggedIn())
        {
            PlayFabService.Instance.Initialize();
        }
    }

    protected T EnsureService<T>() where T : MonoBehaviour
    {
        T service = FindFirstObjectByType<T>();

        if (service != null)
        {
            return service;
        }

        var serviceObject = new GameObject(typeof(T).Name);
        return serviceObject.AddComponent<T>();
    }

    protected bool IsLoggedIn()
    {
        return PlayFabService.Instance != null && PlayFabService.Instance.IsLoggedIn();
    }

    protected static bool HasKeyboard()
    {
        return UnityEngine.InputSystem.Keyboard.current != null;
    }

    protected void PrintReadyMessage(string title, string instructions)
    {
        Debug.Log($"[{title}] {instructions}");
    }
}