/// <summary>
/// Script responsável por gerenciar a exibição de dados em uma linha da tabela de ranking.
/// </summary>

using UnityEngine;
using TMPro;

public class RankingLineService : MonoBehaviour
{
    // Referências aos componentes de texto da interface (UI)
    private TextMeshProUGUI txtPosicao;
    private TextMeshProUGUI txtNome;
    private TextMeshProUGUI txtPontuacao; 
    private TextMeshProUGUI txtVitorias;
    private TextMeshProUGUI txtWinrate;

    private void Awake() {
        // Busca os componentes pelos nomes exatos dos objetos na hierarquia
        txtPosicao = transform.Find("txtPosicao")?.GetComponent<TextMeshProUGUI>();
        txtNome = transform.Find("txtNome")?.GetComponent<TextMeshProUGUI>();
        txtPontuacao = transform.Find("txtXP")?.GetComponent<TextMeshProUGUI>(); 
        txtVitorias = transform.Find("txtVitorias")?.GetComponent<TextMeshProUGUI>();
        txtWinrate = transform.Find("txtWinrate")?.GetComponent<TextMeshProUGUI>();

        // Gera um alerta caso algum elemento esteja faltando na hierarquia da Unity
        if (txtPosicao == null || txtNome == null || txtPontuacao == null) {
            Debug.LogWarning($"Aviso: Algum texto não foi encontrado automaticamente no objeto {gameObject.name}. Verifique os nomes no script!");
        }
    }

    public void SetupLine(string posicao, string nome, string pontuacao, string vitorias, string winrate) {
        // Verifica se os componentes foram carregados antes de tentar atribuir texto (evita erros de referência nula)
        if (txtPosicao != null) txtPosicao.text = posicao;
        if (txtNome != null) txtNome.text = nome;
        if (txtPontuacao != null) txtPontuacao.text = pontuacao;
        if (txtVitorias != null) txtVitorias.text = vitorias;
        if (txtWinrate != null) txtWinrate.text = winrate;
    }
}