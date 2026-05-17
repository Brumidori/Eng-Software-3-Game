using UnityEngine;

/// <summary>
/// Inicializador de PlayFab
/// Responsável apenas por iniciar o PlayFabService centralizado
/// </summary>
public class PlayFabLogin : MonoBehaviour
{
    [SerializeField] private bool enableAutoLogin = true;
    [SerializeField] private string autoLoginEmail = "user@email.com";
    [SerializeField] private string autoLoginPassword = "senha123";

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
            return;
        }

        PlayFabService.Instance.Initialize();

        if (!enableAutoLogin)
        {
            Debug.Log("[PlayFabLogin] Inicializacao do PlayFab iniciada sem auto login.");
            return;
        }

        if (string.IsNullOrWhiteSpace(autoLoginEmail) || string.IsNullOrWhiteSpace(autoLoginPassword))
        {
            Debug.LogWarning("[PlayFabLogin] Auto login habilitado, mas email ou senha estao vazios.");
            return;
        }

        Debug.Log($"[PlayFabLogin] Auto login habilitado para '{autoLoginEmail.Trim()}'.");
        PlayFabService.Instance.LoginWithEmail(autoLoginEmail, autoLoginPassword);
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