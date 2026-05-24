using UnityEngine;
using UnityEngine.UI; 
using System.Collections.Generic;
using PlayFab.ClientModels;

/// <summary>
/// Controlador principal da tela de ranking
/// Gerencia a interface, a troca de abas (Semanal/Mensal) e a atualização dos dados recebidos do PlayFab
/// </summary>
public class RankingScreenController : MonoBehaviour
{
    [Header("Componentes da Tabela")]
    [SerializeField] private RankingLineService[] linhasRanking;
    [SerializeField] private RankingLineService linhaDoJogador; 

    [Header("Botão Semanal")]
    [SerializeField] private Button btnSemanal;
    [SerializeField] private Image imgSemanal;
    [SerializeField] private Sprite spriteSemanalAceso;
    [SerializeField] private Sprite spriteSemanalApagado;

    [Header("Botão Mensal")]
    [SerializeField] private Button btnMensal;
    [SerializeField] private Image imgMensal;
    [SerializeField] private Sprite spriteMensalAceso;
    [SerializeField] private Sprite spriteMensalApagado;

    [Header("Estado Atual")]
    [SerializeField] private TipoRanking rankingAtivo = TipoRanking.Semanal;

    private void OnEnable()
    {
        RankingService.OnRankingCarregado += PreencherTop15;
        RankingService.OnRankingJogadorCarregado += PreencherLinhaJogador;
        RankingService.OnErroAoCarregar += ExibirErro;

        // Configura os cliques dos botões 
        if (btnSemanal != null) btnSemanal.onClick.AddListener(() => AtualizarFiltroRanking(TipoRanking.Semanal));
        if (btnMensal != null) btnMensal.onClick.AddListener(() => AtualizarFiltroRanking(TipoRanking.Mensal));
    }

    private void OnDisable()
    {
        RankingService.OnRankingCarregado -= PreencherTop15;
        RankingService.OnRankingJogadorCarregado -= PreencherLinhaJogador;
        RankingService.OnErroAoCarregar -= ExibirErro;

        if (btnSemanal != null) btnSemanal.onClick.RemoveAllListeners();
        if (btnMensal != null) btnMensal.onClick.RemoveAllListeners();
    }

    private void Start()
    {
        // Força a UI a iniciar na aba Semanal
        AtualizarFiltroRanking(TipoRanking.Semanal);
    }

    // Alterna entre as abas, altera os sprites dos botões e solicita novos dados ao serviço
    private void AtualizarFiltroRanking(TipoRanking novoTipo)
    {
        rankingAtivo = novoTipo;

        if (novoTipo == TipoRanking.Semanal)
        {
            if (imgSemanal != null) imgSemanal.sprite = spriteSemanalApagado;
            if (imgMensal != null) imgMensal.sprite = spriteMensalAceso;
        }
        else
        {
            if (imgSemanal != null) imgSemanal.sprite = spriteSemanalAceso;
            if (imgMensal != null) imgMensal.sprite = spriteMensalApagado;
        }

        ConfigurarTabelaVazia("Carregando...");
        if (linhaDoJogador != null) linhaDoJogador.SetupLine("-", "Buscando...", "-", "-", "-");

        if (RankingService.Instance != null)
            RankingService.Instance.BaixarRanking(rankingAtivo);
    }

    // Processa a lista de jogadores recebida do PlayFab e preenche as 15 linhas da tabela
    private void PreencherTop15(List<PlayerLeaderboardEntry> ranking, TipoRanking tipoRetornado)
    {
        if (tipoRetornado != rankingAtivo) return;

        // Define quais nomes de estatísticas para pegar no Leaderboard
        string nomeStatVitorias = (tipoRetornado == TipoRanking.Semanal) ? "vitorias_semanal" : "vitorias_mensal";
        string nomeStatPartidas = (tipoRetornado == TipoRanking.Semanal) ? "partidas_totais_semanal" : "partidas_totais_mensal";

        for (int i = 0; i < linhasRanking.Length; i++)
        {
            if (linhasRanking[i] == null) continue;

            if (i < ranking.Count)
            {
                var entry = ranking[i];
                int vits = 0;
                int totais = 0;

                // Extrai as estatísticas do perfil do jogador
                if (entry.Profile != null && entry.Profile.Statistics != null)
                {
                    var statVit = entry.Profile.Statistics.Find(s => s.Name == nomeStatVitorias);
                    var statTot = entry.Profile.Statistics.Find(s => s.Name == nomeStatPartidas);

                    if (statVit != null) vits = statVit.Value;
                    if (statTot != null) totais = statTot.Value;
                }

                // Cálculo do Winrate
                float taxa = totais > 0 ? ((float)vits / totais) * 100f : 0f;
                string winrateStr = taxa.ToString("F1") + "%";

                string nome = string.IsNullOrEmpty(entry.DisplayName) ? "Jogador" : entry.DisplayName;
                string posicao = (entry.Position + 1).ToString();
                string xp = entry.StatValue.ToString();

                // Preenche a linha com os dados
                linhasRanking[i].SetupLine(posicao, nome, xp, vits.ToString(), winrateStr);
            }
            else
            {
                linhasRanking[i].SetupLine("-", "-", "-", "-", "-");
            }
        }
    }

    // Preenche a linha inferior com os dados do jogador logado
    private void PreencherLinhaJogador(PlayerLeaderboardEntry jogadorLogado, TipoRanking tipoRetornado)
    {
        if (tipoRetornado != rankingAtivo || linhaDoJogador == null) return;

        string posicao = (jogadorLogado.Position + 1).ToString();
        string nome = string.IsNullOrEmpty(jogadorLogado.DisplayName) ? "Você" : jogadorLogado.DisplayName;
        string xp = jogadorLogado.StatValue.ToString();

        linhaDoJogador.SetupLine(posicao, nome, xp, "-", "-");
    }

    // Limpa a tabela visualmente enquanto aguarda novos dados
    private void ConfigurarTabelaVazia(string mensagem)
    {
        foreach (var linha in linhasRanking)
        {
            if (linha != null) linha.SetupLine("-", mensagem, "-", "-", "-");
        }
    }

    // Gerencia o feedback visual caso ocorra algum erro na comunicação com o PlayFab
    private void ExibirErro(string mensagem)
    {
        ConfigurarTabelaVazia("Erro ao carregar");
        if (linhaDoJogador != null) linhaDoJogador.SetupLine("-", "Erro", "-", "-", "-");
    }
}