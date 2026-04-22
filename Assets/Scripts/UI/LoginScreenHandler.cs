using PlayFab;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controla o fluxo de login por Email + Senha na UI.
/// Se os objetos loginInput/senhaInput/loginBtn existirem na cena,
/// o handler e ligado automaticamente em tempo de execucao.
/// </summary>
public class LoginScreenHandler : MonoBehaviour
{
    private const string LoginInputObjectName = "loginInput";
    private const string PasswordInputObjectName = "senhaInput";
    private const string LoginButtonObjectName = "loginBtn";

    [Header("Referencias UI")]
    [SerializeField] private InputField loginInput;
    [SerializeField] private InputField senhaInput;
    [SerializeField] private Button loginBtn;
    [SerializeField] private Text feedbackText;

    [Header("Comportamento")]
    [SerializeField] private bool autoFindElementsByName = true;
    [SerializeField] private bool clearPasswordOnFailure = true;
    [SerializeField] private string nextSceneOnSuccess = string.Empty;
    [SerializeField] private string userSuccessScene = "LoginSuccess";
    [SerializeField] private string adminSuccessScene = "DeckAdminMenu";

    private static bool sceneHookRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterAutoAttach()
    {
        if (!sceneHookRegistered)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            sceneHookRegistered = true;
        }

        TryAttachToScene();
    }

    private static void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        TryAttachToScene();
    }

    private static void TryAttachToScene()
    {
        if (Object.FindFirstObjectByType<LoginScreenHandler>() != null)
        {
            return;
        }

        var loginGo = GameObject.Find(LoginInputObjectName);
        var passwordGo = GameObject.Find(PasswordInputObjectName);
        var buttonGo = GameObject.Find(LoginButtonObjectName);

        if (loginGo == null || passwordGo == null || buttonGo == null)
        {
            return;
        }

        var host = buttonGo.GetComponent<LoginScreenHandler>();
        if (host == null)
        {
            host = buttonGo.AddComponent<LoginScreenHandler>();
        }

        host.loginInput = loginGo.GetComponent<InputField>();
        host.senhaInput = passwordGo.GetComponent<InputField>();
        host.loginBtn = buttonGo.GetComponent<Button>();

        var feedback = GameObject.Find("loginFeedback");
        if (feedback != null)
        {
            host.feedbackText = feedback.GetComponent<Text>();
        }
    }

    private void Awake()
    {
        if (autoFindElementsByName)
        {
            TryAutoBindElements();
        }
    }

    private void OnEnable()
    {
        if (loginBtn != null)
        {
            loginBtn.onClick.AddListener(HandleLoginButtonClick);
        }

        PlayFabService.OnLoginSuccess += HandleLoginSuccess;
        PlayFabService.OnLoginFailure += HandleLoginFailure;
        AuthorizationService.OnRoleValidated += HandleRoleValidated;
        AuthorizationService.OnRoleValidationFailed += HandleRoleValidationFailed;
    }

    private void OnDisable()
    {
        if (loginBtn != null)
        {
            loginBtn.onClick.RemoveListener(HandleLoginButtonClick);
        }

        PlayFabService.OnLoginSuccess -= HandleLoginSuccess;
        PlayFabService.OnLoginFailure -= HandleLoginFailure;
        AuthorizationService.OnRoleValidated -= HandleRoleValidated;
        AuthorizationService.OnRoleValidationFailed -= HandleRoleValidationFailed;
    }

    private void Start()
    {
        EnsurePlayFabService();
        EnsureAuthorizationService();
        PlayFabService.Instance.Initialize(false);

        SetFeedback(string.Empty, false);
        SetInteractable(true);
    }

    public void HandleLoginButtonClick()
    {
        EnsurePlayFabService();
        EnsureAuthorizationService();

        var email = loginInput != null ? loginInput.text.Trim() : string.Empty;
        var password = senhaInput != null ? senhaInput.text : string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            SetFeedback("Informe email e senha.", true);
            return;
        }

        SetInteractable(false);
        SetFeedback("Autenticando...", false);
        AuthorizationService.Instance.ResetRoleState();

        PlayFabService.Instance.LoginWithEmail(email, password, success =>
        {
            if (!success)
            {
                SetInteractable(true);
            }
        });
    }

    private void HandleLoginSuccess()
    {
        SetFeedback("Login realizado. Validando perfil...", false);
        EnsureAuthorizationService();
        AuthorizationService.Instance.ValidatePlayerRole();
    }

    private void HandleRoleValidated(UserRole role)
    {
        SetInteractable(true);
        SetFeedback(role == UserRole.Admin ? "Perfil admin validado." : "Perfil de usuario validado.", false);

        var targetScene = ResolveTargetScene(role);
        if (string.IsNullOrWhiteSpace(targetScene))
        {
            SetFeedback("Cena de destino nao configurada.", true);
            return;
        }

        Debug.Log($"[LoginScreenHandler] Redirecionando role '{role}' para cena '{targetScene}'.");
        SceneManager.LoadScene(targetScene);
    }

    private void HandleRoleValidationFailed(string reason)
    {
        SetInteractable(true);
        SetFeedback(string.IsNullOrWhiteSpace(reason) ? "Falha ao validar perfil de acesso." : reason, true);
    }

    private void HandleLoginFailure(PlayFabError error)
    {
        SetInteractable(true);

        if (clearPasswordOnFailure && senhaInput != null)
        {
            senhaInput.text = string.Empty;
        }

        SetFeedback(BuildFriendlyError(error), true);
    }

    private string BuildFriendlyError(PlayFabError error)
    {
        if (error == null)
        {
            return "Falha no login. Tente novamente.";
        }

        var report = error.GenerateErrorReport();
        if (!string.IsNullOrEmpty(report) && report.ToLowerInvariant().Contains("password"))
        {
            return "Email ou senha invalidos.";
        }

        if (!string.IsNullOrEmpty(report) && report.ToLowerInvariant().Contains("not found"))
        {
            return "Conta nao encontrada.";
        }

        return "Nao foi possivel autenticar. Verifique suas credenciais.";
    }

    private void SetInteractable(bool interactable)
    {
        if (loginBtn != null)
        {
            loginBtn.interactable = interactable;
        }
    }

    private void SetFeedback(string message, bool isError)
    {
        if (feedbackText == null)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Debug.Log(isError ? $"[LoginScreenHandler] {message}" : $"[LoginScreenHandler] {message}");
            }

            return;
        }

        feedbackText.text = message;
        feedbackText.color = isError ? Color.red : Color.white;
    }

    private void TryAutoBindElements()
    {
        if (loginInput == null)
        {
            var loginGo = GameObject.Find(LoginInputObjectName);
            if (loginGo != null)
            {
                loginInput = loginGo.GetComponent<InputField>();
            }
        }

        if (senhaInput == null)
        {
            var senhaGo = GameObject.Find(PasswordInputObjectName);
            if (senhaGo != null)
            {
                senhaInput = senhaGo.GetComponent<InputField>();
            }
        }

        if (loginBtn == null)
        {
            var loginButtonGo = GameObject.Find(LoginButtonObjectName);
            if (loginButtonGo != null)
            {
                loginBtn = loginButtonGo.GetComponent<Button>();
            }
        }
    }

    private static void EnsurePlayFabService()
    {
        if (PlayFabService.Instance != null)
        {
            return;
        }

        var playFabServiceGO = new GameObject("PlayFabService");
        playFabServiceGO.AddComponent<PlayFabService>();
    }

    private static void EnsureAuthorizationService()
    {
        if (AuthorizationService.Instance != null)
        {
            return;
        }

        var authorizationServiceGO = new GameObject("AuthorizationService");
        authorizationServiceGO.AddComponent<AuthorizationService>();
    }

    private string ResolveTargetScene(UserRole role)
    {
        if (role == UserRole.Admin && !string.IsNullOrWhiteSpace(adminSuccessScene))
        {
            return adminSuccessScene;
        }

        if (role == UserRole.User && !string.IsNullOrWhiteSpace(userSuccessScene))
        {
            return userSuccessScene;
        }

        return nextSceneOnSuccess;
    }
}
