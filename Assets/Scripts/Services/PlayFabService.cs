using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using System;

/// <summary>
/// Serviço centralizado de conexão com PlayFab
/// Gerencia autenticação e configurações iniciais do SDK
/// Deve ser usado como ponto único de entrada para todas as operações PlayFab
/// </summary>
public class PlayFabService : MonoBehaviour
{
    public static PlayFabService Instance { get; private set; }

    public static event Action OnLoginSuccess;
    public static event Action<PlayFabError> OnLoginFailure;

    private void Awake()
    {
        // Implementar Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Inicializa o PlayFab com as configurações centralizadas
    /// </summary>
    public void Initialize()
    {
        // Configurar Title ID baseado no ambiente
        PlayFabSettings.staticSettings.TitleId = PlayFabConfig.GetTitleId();
        Debug.Log($"[PlayFabService] TitleId configurado: {PlayFabConfig.GetTitleId()} (Ambiente: {PlayFabConfig.CurrentEnv})");

        // Fazer login
        LoginWithCustomId(PlayFabConfig.GetTestUserId());
    }

    /// <summary>
    /// Faz login com um Custom ID
    /// </summary>
    /// <param name="customId">ID customizado do usuário</param>
    public void LoginWithCustomId(string customId)
    {
        var request = new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = PlayFabConfig.GetCreateAccountFlag()
        };

        PlayFabClientAPI.LoginWithCustomID(request,
            OnLoginSuccessCallback,
            OnLoginFailureCallback);

        Debug.Log($"[PlayFabService] Tentando login com ID: {customId}");
    }

    private void OnLoginSuccessCallback(LoginResult result)
    {
        Debug.Log("[PlayFabService] ✅ Login realizado com sucesso!");
        OnLoginSuccess?.Invoke();
    }

    private void OnLoginFailureCallback(PlayFabError error)
    {
        Debug.LogError($"[PlayFabService] ❌ Erro no login: {error.GenerateErrorReport()}");
        OnLoginFailure?.Invoke(error);
    }

    /// <summary>
    /// Alterna entre ambientes (útil para desenvolvimento/testes)
    /// </summary>
    public void SetEnvironment(PlayFabConfig.Environment env)
    {
        PlayFabConfig.CurrentEnv = env;
        Debug.Log($"[PlayFabService] Ambiente alterado para: {env}");
    }

    /// <summary>
    /// Verifica se está logado
    /// </summary>
    public bool IsLoggedIn()
    {
        return PlayFabSettings.staticPlayer.IsEntityLoggedIn();
    }
}
