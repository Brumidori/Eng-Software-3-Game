using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using PlayFab.ClientModels;

/// <summary>
/// Controlador principal da tela de ranking.
/// Cruza os resultados do Top 15 com a posição do jogador logado,
/// garantindo que ele apareça na tabela mesmo com valor 0 na stat.
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

    // Cache dos últimos dados recebidos do PlayFab
    private List<PlayerLeaderboardEntry> _ultimoTop15;
    private PlayerLeaderboardEntry       _entradaJogador;
    private string _nomeStatVitorias;
    private string _nomeStatPartidas;

    private void OnEnable()
    {
        RankingService.OnRankingCarregado      += AoReceberTop15;
        RankingService.OnRankingJogadorCarregado += AoReceberEntradaJogador;
        RankingService.OnErroAoCarregar        += ExibirErro;

        if (btnSemanal != null) btnSemanal.onClick.AddListener(() => AtualizarFiltroRanking(TipoRanking.Semanal));
        if (btnMensal  != null) btnMensal.onClick.AddListener(() => AtualizarFiltroRanking(TipoRanking.Mensal));
    }

    private void OnDisable()
    {
        RankingService.OnRankingCarregado      -= AoReceberTop15;
        RankingService.OnRankingJogadorCarregado -= AoReceberEntradaJogador;
        RankingService.OnErroAoCarregar        -= ExibirErro;

        if (btnSemanal != null) btnSemanal.onClick.RemoveAllListeners();
        if (btnMensal  != null) btnMensal.onClick.RemoveAllListeners();
    }

    private void Start()
    {
        AtualizarFiltroRanking(TipoRanking.Semanal);
    }

    private void AtualizarFiltroRanking(TipoRanking novoTipo)
    {
        rankingAtivo    = novoTipo;
        _ultimoTop15    = null;
        _entradaJogador = null;

        if (novoTipo == TipoRanking.Semanal)
        {
            if (imgSemanal != null) imgSemanal.sprite = spriteSemanalApagado;
            if (imgMensal  != null) imgMensal.sprite  = spriteMensalAceso;
        }
        else
        {
            if (imgSemanal != null) imgSemanal.sprite = spriteSemanalAceso;
            if (imgMensal  != null) imgMensal.sprite  = spriteMensalApagado;
        }

        ConfigurarTabelaVazia("Carregando...");
        if (linhaDoJogador != null) linhaDoJogador.SetupLine("-", "Buscando...", "-", "-", "-");

        if (RankingService.Instance != null)
            RankingService.Instance.BaixarRanking(rankingAtivo);
    }

    // ------------------------------------------------------------------
    // Callbacks do RankingService
    // ------------------------------------------------------------------

    private void AoReceberTop15(List<PlayerLeaderboardEntry> ranking, TipoRanking tipo)
    {
        if (tipo != rankingAtivo) return;
        _ultimoTop15 = ranking;
        _nomeStatVitorias = (tipo == TipoRanking.Semanal) ? "vitorias_semanal" : "vitorias_mensal";
        _nomeStatPartidas = (tipo == TipoRanking.Semanal) ? "partidas_totais_semanal" : "partidas_totais_mensal";
        RenderizarTabela();
    }

    private void AoReceberEntradaJogador(PlayerLeaderboardEntry jogador, TipoRanking tipo)
    {
        if (tipo != rankingAtivo) return;
        _entradaJogador = jogador;
        RenderizarLinhaJogador();
        // Re-renderiza a tabela para incluir o jogador no slot correto se ele
        // não veio no Top 15 (ex.: valor 0 excluído pelo PlayFab)
        if (_ultimoTop15 != null) RenderizarTabela();
    }

    // ------------------------------------------------------------------
    // Renderização
    // ------------------------------------------------------------------

    private void RenderizarTabela()
    {
        if (linhasRanking == null) return;

        for (int i = 0; i < linhasRanking.Length; i++)
        {
            if (linhasRanking[i] == null) continue;

            // Verifica se a posição i existe no top 15 retornado pelo PlayFab
            if (_ultimoTop15 != null && i < _ultimoTop15.Count)
            {
                linhasRanking[i].SetupLine(
                    EntradaParaPosicao(_ultimoTop15[i]),
                    EntradaParaNome(_ultimoTop15[i]),
                    _ultimoTop15[i].StatValue.ToString(),
                    ExtrairVitorias(_ultimoTop15[i]).ToString(),
                    CalcularWinrate(_ultimoTop15[i])
                );
            }
            // O jogador logado está neste slot mas não veio no top 15 (valor 0)
            else if (_entradaJogador != null && _entradaJogador.Position == i)
            {
                linhasRanking[i].SetupLine(
                    (i + 1).ToString(),
                    EntradaParaNome(_entradaJogador),
                    _entradaJogador.StatValue.ToString(),
                    ExtrairVitorias(_entradaJogador).ToString(),
                    CalcularWinrate(_entradaJogador)
                );
            }
            else
            {
                linhasRanking[i].SetupLine("-", "-", "-", "-", "-");
            }
        }
    }

    private void RenderizarLinhaJogador()
    {
        if (linhaDoJogador == null || _entradaJogador == null) return;

        linhaDoJogador.SetupLine(
            (_entradaJogador.Position + 1).ToString(),
            EntradaParaNome(_entradaJogador),
            _entradaJogador.StatValue.ToString(),
            ExtrairVitorias(_entradaJogador).ToString(),
            CalcularWinrate(_entradaJogador)
        );
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private string EntradaParaPosicao(PlayerLeaderboardEntry e) => (e.Position + 1).ToString();

    private string EntradaParaNome(PlayerLeaderboardEntry e) =>
        string.IsNullOrEmpty(e.DisplayName) ? "Jogador" : e.DisplayName;

    private int ExtrairVitorias(PlayerLeaderboardEntry e)
    {
        if (e.Profile?.Statistics == null || string.IsNullOrEmpty(_nomeStatVitorias)) return 0;
        var stat = e.Profile.Statistics.Find(s => s.Name == _nomeStatVitorias);
        return stat?.Value ?? 0;
    }

    private int ExtrairPartidas(PlayerLeaderboardEntry e)
    {
        if (e.Profile?.Statistics == null || string.IsNullOrEmpty(_nomeStatPartidas)) return 0;
        var stat = e.Profile.Statistics.Find(s => s.Name == _nomeStatPartidas);
        return stat?.Value ?? 0;
    }

    private string CalcularWinrate(PlayerLeaderboardEntry e)
    {
        int vits   = ExtrairVitorias(e);
        int totais = ExtrairPartidas(e);
        float taxa = totais > 0 ? ((float)vits / totais) * 100f : 0f;
        return taxa.ToString("F1") + "%";
    }

    private void ConfigurarTabelaVazia(string mensagem)
    {
        if (linhasRanking == null) return;
        foreach (var linha in linhasRanking)
            if (linha != null) linha.SetupLine("-", mensagem, "-", "-", "-");
    }

    private void ExibirErro(string mensagem)
    {
        ConfigurarTabelaVazia("Erro ao carregar");
        if (linhaDoJogador != null) linhaDoJogador.SetupLine("-", "Erro", "-", "-", "-");
    }
}
