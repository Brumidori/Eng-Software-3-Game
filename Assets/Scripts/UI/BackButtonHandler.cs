using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controlador genérico para botões de "Voltar" ou "Fechar".
/// Pode ser reutilizado em qualquer cena que precise retornar ao menu principal ou tela anterior.
/// </summary>
public class BackButtonHandler : MonoBehaviour
{
    [Header("Configurações do Botão")]
    [SerializeField] private Button btnVoltar;
    
    [Header("Cena de Destino")]
    [Tooltip("Nome exato da cena para a qual este botão deve voltar")]
    [SerializeField] private string cenaDestino = "HomeScreen"; 

    private void OnEnable()
    {
        if (btnVoltar != null)
            btnVoltar.onClick.AddListener(AoClicarEmVoltar);
        else
            Debug.LogWarning("[BackButtonHandler] O botão 'btnVoltar' não foi atribuído no Inspector!");
    }

    private void OnDisable()
    {
        if (btnVoltar != null)
            btnVoltar.onClick.RemoveListener(AoClicarEmVoltar);
    }

    private void AoClicarEmVoltar()
    {
        if (!string.IsNullOrWhiteSpace(cenaDestino))
        {
            SceneManager.LoadScene(cenaDestino);
        }
        else
        {
            Debug.LogError("[BackButtonHandler] O nome da cena de destino está vazio!");
        }
    }
}