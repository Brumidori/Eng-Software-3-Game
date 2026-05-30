using PlayFab;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RegisterScreenHandler : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private InputField nicknameInput;
    [SerializeField] private InputField emailInput;
    [SerializeField] private InputField senhaInput;
    [SerializeField] private InputField confirmSenhaInput;
    [SerializeField] private Button jogarBtn;
    [SerializeField] private Button voltarBtn;
    [SerializeField] private Text feedbackText;
    [SerializeField] private FeedbackPopup feedbackPopup;

    [Header("Cenas")]
    [SerializeField] private string loginScene = "Login";
    [SerializeField] private string successScene = "HomeScreen";

    private bool _isSubmitting;
    private bool _registrationSucceeded;

    private void Awake()
    {
        TryAutoBindElements();
    }

    private void TryAutoBindElements()
    {
        if (nicknameInput == null)
        {
            var go = GameObject.Find("nicknameInput");
            if (go != null) nicknameInput = go.GetComponent<InputField>();
        }
        if (emailInput == null)
        {
            var go = GameObject.Find("emailInput");
            if (go != null) emailInput = go.GetComponent<InputField>();
        }
        if (senhaInput == null)
        {
            var go = GameObject.Find("senhaInput");
            if (go != null) senhaInput = go.GetComponent<InputField>();
        }
        if (confirmSenhaInput == null)
        {
            var go = GameObject.Find("confirmSenhaInput");
            if (go != null) confirmSenhaInput = go.GetComponent<InputField>();
        }
        if (jogarBtn == null)
        {
            var go = GameObject.Find("jogarBtn");
            if (go != null) jogarBtn = go.GetComponent<Button>();
        }
        if (voltarBtn == null)
        {
            var go = GameObject.Find("voltarBtn");
            if (go != null) voltarBtn = go.GetComponent<Button>();
        }
        if (feedbackPopup == null)
            feedbackPopup = FindFirstObjectByType<FeedbackPopup>();
    }

    private void OnEnable()
    {
        if (jogarBtn != null) jogarBtn.onClick.AddListener(HandleRegisterButtonClick);
        if (voltarBtn != null) voltarBtn.onClick.AddListener(HandleVoltarButtonClick);

        PlayFabService.OnRegisterSuccess += HandleRegisterSuccess;
        PlayFabService.OnRegisterFailure += HandleRegisterFailure;
        AuthorizationService.OnRoleValidated += HandleRoleValidated;
        AuthorizationService.OnRoleValidationFailed += HandleRoleValidationFailed;
    }

    private void OnDisable()
    {

        if (jogarBtn != null) jogarBtn.onClick.RemoveListener(HandleRegisterButtonClick);
        if (voltarBtn != null) voltarBtn.onClick.RemoveListener(HandleVoltarButtonClick);

        PlayFabService.OnRegisterSuccess -= HandleRegisterSuccess;
        PlayFabService.OnRegisterFailure -= HandleRegisterFailure;
        AuthorizationService.OnRoleValidated -= HandleRoleValidated;
        AuthorizationService.OnRoleValidationFailed -= HandleRoleValidationFailed;
    }

    private void Start()
    {
        EnsurePlayFabService();
        EnsureAuthorizationService();
        PlayFabService.Instance.Initialize();
        SetFeedback(string.Empty, false);
        SetInteractable(true);
    }

    private void HandleRegisterButtonClick()
    {
        if (_isSubmitting) return;

        var nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
        var email = emailInput != null ? emailInput.text.Trim() : string.Empty;
        var senha = senhaInput != null ? senhaInput.text : string.Empty;
        var confirmSenha = confirmSenhaInput != null ? confirmSenhaInput.text : string.Empty;

        if (!ValidateFields(nickname, email, senha, confirmSenha)) return;

        _isSubmitting = true;
        SetInteractable(false);
        SetFeedback("Criando conta...", false);
        EnsureAuthorizationService();
        AuthorizationService.Instance.ResetRoleState();

        PlayFabService.Instance.RegisterWithEmail(nickname, email, senha);
    }

    private bool ValidateFields(string nickname, string email, string senha, string confirmSenha)
    {
        if (string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(senha) || string.IsNullOrWhiteSpace(confirmSenha))
        {
            SetFeedback("Preencha todos os campos.", true);
            return false;
        }

        if (nickname.Length < 3 || nickname.Length > 20)
        {
            SetFeedback("Nickname deve ter entre 3 e 20 caracteres.", true);
            return false;
        }

        if (!IsValidUsername(nickname))
        {
            SetFeedback("Nickname deve conter apenas letras, numeros e underline.", true);
            return false;
        }

        if (!email.Contains("@"))
        {
            SetFeedback("Email invalido.", true);
            return false;
        }

        if (senha.Length < 6)
        {
            SetFeedback("Senha deve ter pelo menos 6 caracteres.", true);
            return false;
        }

        if (senha != confirmSenha)
        {
            SetFeedback("As senhas nao conferem.", true);
            return false;
        }

        return true;
    }

    private static bool IsValidUsername(string username)
    {
        foreach (char c in username)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }
        return true;
    }

    private void HandleRegisterSuccess()
    {
        _isSubmitting = false;
        _registrationSucceeded = true;
        SetFeedback("Conta criada! Concedendo decks iniciais...", false);
        EnsureStarterDeckGrantService();

        EnsureRankingService();
        RankingService.Instance.RegistroRanking();

        StarterDeckGrantService.Instance.GrantStarterDecks(result =>
        {
            if (result == null || !result.Success)
            {
                _registrationSucceeded = false;
                _isSubmitting = false;
                SetInteractable(true);
                SetFeedback(result != null && !string.IsNullOrWhiteSpace(result.Error)
                    ? result.Error
                    : "Nao foi possivel conceder os decks iniciais.", true);
                return;
            }

            if (InventoryService.Instance != null)
            {
                InventoryService.Instance.LoadInventory();
            }

            SetFeedback(result.AlreadyGranted
                ? "Decks iniciais ja confirmados. Validando perfil..."
                : "Decks iniciais concedidos. Validando perfil...", false);

            EnsureAuthorizationService();
            AuthorizationService.Instance.ValidatePlayerRole();
        });
    }

    private void HandleRegisterFailure(PlayFabError error)
    {
        _registrationSucceeded = false;
        _isSubmitting = false;
        SetInteractable(true);
        SetFeedback(BuildFriendlyError(error), true);
    }

    private void HandleRoleValidated(UserRole role)
    {
        if (!_registrationSucceeded) return;
        StartCoroutine(RedirectAfterDelay());
    }

    private void HandleRoleValidationFailed(string reason)
    {
        if (!_registrationSucceeded) return;
        StartCoroutine(RedirectAfterDelay());
    }

    private System.Collections.IEnumerator RedirectAfterDelay()
    {
        SetFeedback("Conta criada com sucesso!", false);
        yield return new WaitForSeconds(1f);
        var targetScene = string.IsNullOrWhiteSpace(successScene) ? "HomeScreen" : successScene;
        SceneManager.LoadScene(targetScene);
    }

    private void HandleVoltarButtonClick()
    {
        SceneManager.LoadScene(loginScene);
    }

    private string BuildFriendlyError(PlayFabError error)
    {
        if (error == null) return "Erro ao criar conta. Tente novamente.";

        if (error.Error == PlayFabErrorCode.UsernameNotAvailable ||
            error.Error == PlayFabErrorCode.NameNotAvailable)
            return "Este nickname ja esta em uso. Escolha outro.";

        if (error.Error == PlayFabErrorCode.EmailAddressNotAvailable)
            return "Este email ja esta cadastrado.";

        if (error.Error == PlayFabErrorCode.InvalidEmailAddress)
            return "Email invalido.";

        if (error.Error == PlayFabErrorCode.InvalidPassword ||
            error.Error == PlayFabErrorCode.InvalidParams)
            return "Senha invalida. Minimo 6 caracteres.";

        return "Nao foi possivel criar a conta. Tente novamente.";
    }

    private void SetFeedback(string message, bool isError)
    {
        if (feedbackPopup != null)
        {
            if (string.IsNullOrEmpty(message)) { feedbackPopup.Hide(); return; }
            feedbackPopup.Show(message, isError);
            return;
        }

        if (feedbackText == null)
        {
            if (!string.IsNullOrEmpty(message))
                Debug.Log($"[RegisterScreenHandler] {message}");
            return;
        }

        feedbackText.text = message;
        feedbackText.color = isError ? Color.red : Color.white;
    }

    private void SetInteractable(bool interactable)
    {
        if (nicknameInput != null)     nicknameInput.interactable     = interactable;
        if (emailInput != null)        emailInput.interactable        = interactable;
        if (senhaInput != null)        senhaInput.interactable        = interactable;
        if (confirmSenhaInput != null) confirmSenhaInput.interactable = interactable;
        if (jogarBtn != null)          jogarBtn.interactable          = interactable;
        if (voltarBtn != null)         voltarBtn.interactable         = interactable;

        if (interactable)
        {
            RestoreButtonRaycast(jogarBtn);
            RestoreButtonRaycast(voltarBtn);
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }

    private static void RestoreButtonRaycast(Button btn)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
    }

    private static void EnsurePlayFabService()
    {
        if (PlayFabService.Instance != null) return;
        new GameObject("PlayFabService").AddComponent<PlayFabService>();
    }

    private static void EnsureAuthorizationService()
    {
        if (AuthorizationService.Instance != null) return;
        new GameObject("AuthorizationService").AddComponent<AuthorizationService>();
    }

    private static void EnsureStarterDeckGrantService()
    {
        if (StarterDeckGrantService.Instance != null) return;
        new GameObject("StarterDeckGrantService").AddComponent<StarterDeckGrantService>();
    }

    private static void EnsureRankingService()
    {
        if (RankingService.Instance != null) return;
        new GameObject("RankingService").AddComponent<RankingService>();
    }
}
