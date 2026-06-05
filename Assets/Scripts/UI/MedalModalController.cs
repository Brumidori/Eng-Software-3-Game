using UnityEngine;
using UnityEngine.UI;

public class MedalModalController : MonoBehaviour
{
    [Header("Configurações de Painel")]
    [SerializeField] private Transform panel;
    [SerializeField] private GameObject medalPrefab;

    [Header("Sprites das Medalhas")]
    [SerializeField] private Sprite[] spritesApagados;
    [SerializeField] private Sprite[] spritesAcesos;

    private void Start()
    {
        InicializarInterface();
        CarregarProgressoDoJogador();
    }

    private void InicializarInterface()
    {
        // Garante que o painel comece limpo
        foreach (Transform child in panel) Destroy(child.gameObject);

        // Gera as medalhas inicialmente apagadas
        for (int i = 0; i < spritesApagados.Length; i++)
        {
            var medal = Instantiate(medalPrefab, panel);
            medal.GetComponent<Image>().sprite = spritesApagados[i];
        }
    }

    private void CarregarProgressoDoJogador()
    {
        var service = MedalhaService.Instance;

        // Partidas Totais
        service.PegarEstatistica("partidas_totais", valor => {
            ValidarConquista(valor, 1, 0);
            ValidarConquista(valor, 10, 1);
            ValidarConquista(valor, 50, 2);
        });

        // Acertos Totais
        service.PegarEstatistica("acertos_totais", valor => {
            ValidarConquista(valor, 50, 3);
            ValidarConquista(valor, 200, 4);
            ValidarConquista(valor, 500, 5);
        });

        // Acertos Tema
        service.PegarEstatistica("acertos_tema_temp", valor => {
            ValidarConquista(valor, 10, 6);
            ValidarConquista(valor, 30, 7);
            ValidarConquista(valor, 100, 8);
        });

        // Escudos
        service.PegarEstatistica("escudos", valor => {
            ValidarConquista(valor, 10, 9);
            ValidarConquista(valor, 25, 10);
            ValidarConquista(valor, 50, 11);
        });
    }

    private void ValidarConquista(int valorAtual, int meta, int indice)
    {
        if (valorAtual >= meta) 
            AtivarMedalhaVisual(indice);
    }

    private void AtivarMedalhaVisual(int index)
    {
        if (index < 0 || index >= panel.childCount) return;

        var imagemMedalha = panel.GetChild(index).GetComponent<Image>();
        imagemMedalha.sprite = spritesAcesos[index];
    }
}