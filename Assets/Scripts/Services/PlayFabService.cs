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
    public static IPlayFabClientFacade Client { get; private set; } = new PlayFabClientFacade();
    public string CurrentCustomId { get; private set; }
    public string CurrentEmail { get; private set; }

    public static event Action OnLoginSuccess;
    public static event Action<PlayFabError> OnLoginFailure;
    public static event Action OnRegisterSuccess;
    public static event Action<PlayFabError> OnRegisterFailure;

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
        ConfigureTitleId();
        Debug.Log("[PlayFabService] Inicializado. Aguardando login manual.");
    }

    /// <summary>
    /// Aplica configuracoes basicas do SDK PlayFab sem autenticar o jogador.
    /// </summary>
    public void ConfigureTitleId()
    {
        PlayFabSettings.staticSettings.TitleId = PlayFabConfig.GetTitleId();
        Debug.Log($"[PlayFabService] TitleId configurado: {PlayFabConfig.GetTitleId()} (Ambiente: {PlayFabConfig.CurrentEnv})");
    }

    /// <summary>
    /// Faz login com um Custom ID
    /// </summary>
    /// <param name="customId">ID customizado do usuário</param>
    public void LoginWithCustomId(string customId)
    {
        ConfigureTitleId();
        CurrentCustomId = customId;
        CurrentEmail = null;

        var request = new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = PlayFabConfig.GetCreateAccountFlag()
        };

        Client.LoginWithCustomID(request,
            OnLoginSuccessCallback,
            OnLoginFailureCallback);

        Debug.Log($"[PlayFabService] Tentando login com ID: {customId}");
    }

    /// <summary>
    /// Faz login com Email + Senha.
    /// </summary>
    public void LoginWithEmail(string email, string password, Action<bool> callback = null)
    {
        ConfigureTitleId();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Debug.LogWarning("[PlayFabService] Login com email/senha ignorado: campos vazios.");
            callback?.Invoke(false);
            return;
        }

        CurrentEmail = email.Trim();
        CurrentCustomId = null;

        var request = new LoginWithEmailAddressRequest
        {
            Email = CurrentEmail,
            Password = password
        };

        Client.LoginWithEmailAddress(
            request,
            result =>
            {
                OnLoginSuccessCallback(result);
                callback?.Invoke(true);
            },
            error =>
            {
                OnLoginFailureCallback(error);
                callback?.Invoke(false);
            });

        Debug.Log($"[PlayFabService] Tentando login com email: {CurrentEmail}");
    }

    /// <summary>
    /// Registra um novo usuário com nickname, email e senha.
    /// Após o registro, o PlayFab já faz o login automaticamente.
    /// </summary>
    public void RegisterWithEmail(string username, string email, string password)
    {
        ConfigureTitleId();
        CurrentEmail = email.Trim();
        CurrentCustomId = null;

        var request = new RegisterPlayFabUserRequest
        {
            Username    = username.Trim(),
            DisplayName = username.Trim(),   // define o Display Name atomicamente no registro
            Email       = CurrentEmail,
            Password    = password,
            RequireBothUsernameAndEmail = true
        };

        Client.RegisterPlayFabUser(request,
            result =>
            {
                Debug.Log($"[PlayFabService] Registro realizado com sucesso. PlayFabId: {result.PlayFabId}");
                OnRegisterSuccess?.Invoke();
            },
            error =>
            {
                Debug.LogError($"[PlayFabService] Erro no registro: {error.GenerateErrorReport()}");
                OnRegisterFailure?.Invoke(error);
            });

        Debug.Log($"[PlayFabService] Tentando registrar usuario: {username.Trim()} / {CurrentEmail}");
    }

    private void OnLoginSuccessCallback(LoginResult result)
    {
        var identifier = !string.IsNullOrEmpty(CurrentEmail) ? CurrentEmail : CurrentCustomId;
        Debug.Log($"[PlayFabService] Login realizado com sucesso para '{identifier}'!");
        OnLoginSuccess?.Invoke();
        TryGrantStarterDecks();
    }

    private void TryGrantStarterDecks()
    {
        if (StarterDeckGrantService.Instance == null)
        {
            var go = new GameObject("StarterDeckGrantService");
            go.AddComponent<StarterDeckGrantService>();
        }
        StarterDeckGrantService.Instance.GrantStarterDecks(r =>
        {
            if (!r.Success)
                Debug.LogWarning($"[PlayFabService] GrantStarterDecks falhou: {r.Error}");
            else if (!r.AlreadyGranted)
                Debug.Log($"[PlayFabService] Decks iniciais concedidos: {string.Join(", ", r.GrantedItemIds)}");
        });
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
        return PlayFabClientAPI.IsClientLoggedIn();
    }
}
