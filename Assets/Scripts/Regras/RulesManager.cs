using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class RulesManager : MonoBehaviour
{
    [Header("Paginas das regras")]
    public GameObject[] pages;

    [Header("Botoes")]
    public Button nextButton;
    public Button previousButton;
    public Button configButton;

    private int currentPage = 0;

    void Start()
    {
        // Mostra a primeira página
        ShowPage(currentPage);

        // Eventos dos botões
        nextButton.onClick.AddListener(NextPage);
        previousButton.onClick.AddListener(PreviousPage);
        configButton.onClick.AddListener(LoadConfigScene);
    }

    void ShowPage(int index)
    {
        // Ativa somente a página atual
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == index);
        }

        // Esconde botão anterior na primeira página
        previousButton.gameObject.SetActive(index > 0);

        // Esconde botão próximo na última página
        nextButton.gameObject.SetActive(index < pages.Length - 1);
    }

    public void NextPage()
    {
        if (currentPage < pages.Length - 1)
        {
            currentPage++;
            ShowPage(currentPage);
        }
    }

    public void PreviousPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            ShowPage(currentPage);
        }
    }

    public void LoadConfigScene()
    {
        SceneManager.LoadScene("Config");
    }
}