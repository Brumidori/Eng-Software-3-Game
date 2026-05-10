using UnityEngine;

/// <summary>
/// Inicializador de PlayFab
/// Responsável apenas por iniciar o PlayFabService centralizado
/// </summary>
public class PlayFabLogin : MonoBehaviour
{
    [SerializeField] private bool disableAutoLoginWhenLoginUiDetected = true;

    private void OnEnable()
    {
        PlayFabService.OnLoginSuccess += OnPlayFabLoginSuccess;
        PlayFabService.OnLoginFailure += OnPlayFabLoginFailure;
    }

    private void OnDisable()
    {
        PlayFabService.OnLoginSuccess -= OnPlayFabLoginSuccess;
        PlayFabService.OnLoginFailure -= OnPlayFabLoginFailure;
    }

    private void Start()
    {
        // Verificar se PlayFabService existe, se não, criar um novo GameObject com o serviço
        if (PlayFabService.Instance == null)
        {
            var playFabServiceGO = new GameObject("PlayFabService");
            playFabServiceGO.AddComponent<PlayFabService>();
        }

        if (PlayFabService.Instance.IsLoggedIn())
        {
            Debug.Log("[PlayFabLogin] Sessao PlayFab ja autenticada.");
        }
        else
        {
            bool loginUiDetected = disableAutoLoginWhenLoginUiDetected && HasLoginUiInScene();

            if (loginUiDetected)
            {
                PlayFabService.Instance.Initialize();
                Debug.Log("[PlayFabLogin] UI de login detectada. Login automatico por Custom ID foi desativado nesta cena.");
                return;
            }

            PlayFabService.Instance.Initialize();
            Debug.Log("[PlayFabLogin] Inicializacao do PlayFab iniciada.");
        }
    }

    private static bool HasLoginUiInScene()
    {
        var loginInput = GameObject.Find("loginInput");
        var passwordInput = GameObject.Find("senhaInput");
        var loginButton = GameObject.Find("loginBtn");

        return loginInput != null && passwordInput != null && loginButton != null;
    }

    private void OnPlayFabLoginSuccess()
    {
        Debug.Log("[PlayFabLogin] Login PlayFab confirmado.");
    }

    private void OnPlayFabLoginFailure(PlayFab.PlayFabError error)
    {
        Debug.LogError("[PlayFabLogin] Login PlayFab falhou.");
    }
}