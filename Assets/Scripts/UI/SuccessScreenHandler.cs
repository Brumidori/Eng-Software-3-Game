using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Exibe mensagem de sucesso contextual de acordo com o role validado.
/// </summary>
public class SuccessScreenHandler : MonoBehaviour
{
    [SerializeField] private Text successMessageText;
    [SerializeField] private string userMessage = "Login executado com sucesso!";
    [SerializeField] private string adminMessage = "Login executado com sucesso! Perfil: ADMIN.";

    private void Awake()
    {
        if (successMessageText == null)
        {
            successMessageText = GetComponent<Text>();
        }
    }

    private void Start()
    {
        ApplyRoleMessage();
    }

    private void ApplyRoleMessage()
    {
        if (successMessageText == null)
        {
            return;
        }

        var role = AuthorizationService.Instance != null && AuthorizationService.Instance.HasValidatedRole
            ? AuthorizationService.Instance.CurrentRole
            : UserRole.User;

        successMessageText.text = role == UserRole.Admin ? adminMessage : userMessage;
    }
}
