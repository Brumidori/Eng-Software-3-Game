using UnityEngine;

/// <summary>
/// Inicializador de PlayFab
/// Responsável apenas por iniciar o PlayFabService centralizado
/// </summary>
public class PlayFabLogin : MonoBehaviour
{
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
            // Inicializar o PlayFab e aguardar callback de login para seguir
            PlayFabService.Instance.Initialize();
            Debug.Log("[PlayFabLogin] Inicializacao do PlayFab iniciada. Aguardando autenticacao.");
        }
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