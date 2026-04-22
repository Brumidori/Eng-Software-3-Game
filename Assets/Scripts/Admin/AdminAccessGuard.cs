using UnityEngine;
using UnityEngine.SceneManagement;

public class AdminAccessGuard : MonoBehaviour
{
    [SerializeField] private string loginSceneName = "Login";

    private void OnEnable()
    {
        AuthorizationService.OnRoleValidated += HandleRoleValidated;
        AuthorizationService.OnRoleValidationFailed += HandleRoleValidationFailed;
    }

    private void OnDisable()
    {
        AuthorizationService.OnRoleValidated -= HandleRoleValidated;
        AuthorizationService.OnRoleValidationFailed -= HandleRoleValidationFailed;
    }

    private void Start()
    {
        if (AuthorizationService.Instance == null)
        {
            RedirectToLogin("Servico de autorizacao indisponivel.");
            return;
        }

        if (!AuthorizationService.Instance.HasValidatedRole)
        {
            AuthorizationService.Instance.ValidatePlayerRole();
            return;
        }

        ValidateCurrentRole();
    }

    private void HandleRoleValidated(UserRole _)
    {
        ValidateCurrentRole();
    }

    private void HandleRoleValidationFailed(string reason)
    {
        RedirectToLogin(string.IsNullOrWhiteSpace(reason) ? "Falha ao validar acesso admin." : reason);
    }

    private void ValidateCurrentRole()
    {
        if (AuthorizationService.Instance.CurrentRole != UserRole.Admin)
        {
            RedirectToLogin("Acesso restrito para administradores.");
        }
    }

    private void RedirectToLogin(string reason)
    {
        Debug.LogError($"[AdminAccessGuard] {reason}");
        if (!string.IsNullOrWhiteSpace(loginSceneName))
        {
            SceneManager.LoadScene(loginSceneName);
        }
    }
}
