using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

/// <summary>
/// Define os tipos de ranking disponíveis para filtragem na UI
/// </summary>
public enum TipoRanking { Semanal, Mensal }

/// <summary>
/// Serviço central para comunicação com o PlayFab 
/// Gerencia a busca de rankings (Leaderboards) e a atualização de estatísticas pós-partida
/// </summary>
public class RankingService : MonoBehaviour
{
    public static RankingService Instance { get; private set; }

    //Configuração das Estatísticas"
    private string statSemanal = "xp_mensal";
    private string statMensal = "xp_mensal";

    // Eventos para notificar o RankingScreenController sobre os dados recebidos
    public static event Action<List<PlayerLeaderboardEntry>, TipoRanking> OnRankingCarregado;
    public static event Action<string> OnErroAoCarregar;

    // Evento exclusivo para a linha do jogador logado
    public static event Action<PlayerLeaderboardEntry, TipoRanking> OnRankingJogadorCarregado;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Busca o Top 15 e a posição do jogador atual no ranking solicitado
    public void BaixarRanking(TipoRanking tipo)
    {
        Debug.Log($"[Ranking] Buscando ranking com a conta: {PlayFabSettings.staticPlayer.PlayFabId}");
        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            Debug.LogWarning("[RankingService] Usuário offline.");
            OnErroAoCarregar?.Invoke("Faça login para ver o ranking.");
            return;
        }

        string statAlvo = (tipo == TipoRanking.Semanal) ? statSemanal : statMensal;

        // Requisição do Top 15 Geral para obter xp e as vitórias e total de partidas
        var requestTop15 = new GetLeaderboardRequest
        {
            StatisticName = statAlvo,
            StartPosition = 0,
            MaxResultsCount = 15,
            ProfileConstraints = new PlayerProfileViewConstraints 
            { 
                ShowDisplayName = true,
                ShowStatistics = true 
            }
        };

        PlayFabClientAPI.GetLeaderboard(requestTop15, 
            resultado => OnRankingCarregado?.Invoke(resultado.Leaderboard, tipo), 
            erro => OnErroAoCarregar?.Invoke($"Erro ao buscar Top 15: {erro.GenerateErrorReport()}")
        );

        // Requisição para localizar a posição do jogador no ranking
        var requestJogador = new GetLeaderboardAroundPlayerRequest
        {
            StatisticName = statAlvo,
            MaxResultsCount = 1, 
            ProfileConstraints = new PlayerProfileViewConstraints 
            { 
                ShowDisplayName = true,
                ShowStatistics = true 
            }
        };

        PlayFabClientAPI.GetLeaderboardAroundPlayer(requestJogador,
            resultado => 
            {
                if (resultado.Leaderboard.Count > 0)
                    OnRankingJogadorCarregado?.Invoke(resultado.Leaderboard[0], tipo);
            },
            erro => Debug.LogWarning($"[RankingService] Jogador ainda não tem pontuação neste ranking: {erro.ErrorMessage}")
        );
    }

    // Envia o resultado de uma partida para o PlayFab, atualizando XP, vitórias e contagem total de partidas
    // Exemplo de uso: RankingService.Instance.SalvarFimDePartida(50, true)
    public void SalvarFimDePartida(int xpGanho, bool venceu)
    {
        var atualizacoes = new List<StatisticUpdate>
        {
            // Envia o XP para os dois rankings
            new StatisticUpdate { StatisticName = "xp_semanal", Value = xpGanho },
            new StatisticUpdate { StatisticName = "xp_mensal", Value = xpGanho },
            
            // Adiciona 1 partida total em ambos
            new StatisticUpdate { StatisticName = "partidas_totais_semanal", Value = 1 },
            new StatisticUpdate { StatisticName = "partidas_totais_mensal", Value = 1 }
        };

        // Se o jogador ganhou, adiciona 1 vitória em ambos
        if (venceu)
        {
            atualizacoes.Add(new StatisticUpdate { StatisticName = "vitorias_semanal", Value = 1 });
            atualizacoes.Add(new StatisticUpdate { StatisticName = "vitorias_mensal", Value = 1 });
        }

        var request = new UpdatePlayerStatisticsRequest { Statistics = atualizacoes };

        PlayFabClientAPI.UpdatePlayerStatistics(request, 
            res => Debug.Log("[RankingService] Dados da partida salvos com sucesso!"), 
            err => Debug.LogError($"[RankingService] Erro ao salvar dados: {err.GenerateErrorReport()}")
        );
    }
}