using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ConfigScreenController : MonoBehaviour
{
    [Header("Botoes")]
    [SerializeField] private Button btnRegras;
    [SerializeField] private Button btnLogOut;

    [Header("Cenas")]
    [SerializeField] private string loginScene = "Login";
    [SerializeField] private string regrasScene = "Regras";

    private void Start()
    {
        MainMenuController.Instance?.SetActiveTab(MenuTab.Config);
    }

    private void OnEnable()
    {
        if (btnRegras != null)  btnRegras.onClick.AddListener(AbrirRegras);
        if (btnLogOut != null)  btnLogOut.onClick.AddListener(FazerLogOut);
    }

    private void OnDisable()
    {
        if (btnRegras != null)  btnRegras.onClick.RemoveListener(AbrirRegras);
        if (btnLogOut != null)  btnLogOut.onClick.RemoveListener(FazerLogOut);
    }

    private void AbrirRegras()
    {
        if (!string.IsNullOrWhiteSpace(regrasScene))
            SceneManager.LoadScene(regrasScene);
    }

    private void FazerLogOut()
    {
        PlayFab.PlayFabClientAPI.ForgetAllCredentials();
        SceneManager.LoadScene(loginScene);
    }
}
