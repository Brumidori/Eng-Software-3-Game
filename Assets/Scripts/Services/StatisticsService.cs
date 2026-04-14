using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class StatisticsService : MonoBehaviour
{
    private const string ScoreStatistic = "score";
    private const string WinsStatistic = "wins";
    private const string LossesStatistic = "losses";

    public static StatisticsService Instance { get; private set; }

    public static event Action OnStatisticsUpdated;
    public static event Action<List<LeaderboardEntryData>> OnLeaderboardLoaded;
    public static event Action<PlayFabError> OnStatisticsFailed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (PlayFabService.Instance != null && PlayFabService.Instance.IsLoggedIn())
        {
            return;
        }

        PlayFabService.OnLoginSuccess += HandleLoginSuccess;
    }

    private void OnDestroy()
    {
        PlayFabService.OnLoginSuccess -= HandleLoginSuccess;
    }

    private void HandleLoginSuccess()
    {
        PlayFabService.OnLoginSuccess -= HandleLoginSuccess;
    }

    public void UpdateMatchStatistics(int scoreDelta, bool wonMatch)
    {
        if (!ValidateAuth())
        {
            return;
        }

        var statistics = new List<StatisticUpdate>
        {
            new StatisticUpdate { StatisticName = ScoreStatistic, Value = scoreDelta },
            new StatisticUpdate { StatisticName = wonMatch ? WinsStatistic : LossesStatistic, Value = 1 }
        };

        var request = new UpdatePlayerStatisticsRequest
        {
            Statistics = statistics
        };

        PlayFabService.Client.UpdatePlayerStatistics(request, OnUpdateStatisticsSuccess, OnError);
    }

    public void GetLeaderboard(int maxResults = 10)
    {
        if (!ValidateAuth())
        {
            return;
        }

        var request = new GetLeaderboardRequest
        {
            StatisticName = ScoreStatistic,
            MaxResultsCount = maxResults
        };

        PlayFabService.Client.GetLeaderboard(request, OnGetLeaderboardSuccess, OnError);
    }

    private void OnUpdateStatisticsSuccess(UpdatePlayerStatisticsResult result)
    {
        Debug.Log("[StatisticsService] ✅ Estatísticas atualizadas com sucesso.");
        OnStatisticsUpdated?.Invoke();
    }

    private void OnGetLeaderboardSuccess(GetLeaderboardResult result)
    {
        var entries = new List<LeaderboardEntryData>();

        if (result.Leaderboard != null)
        {
            foreach (var entry in result.Leaderboard)
            {
                entries.Add(new LeaderboardEntryData
                {
                    playFabId = entry.PlayFabId,
                    displayName = entry.DisplayName,
                    position = entry.Position,
                    statisticValue = entry.StatValue
                });
            }
        }

        Debug.Log($"[StatisticsService] ✅ Leaderboard carregado com {entries.Count} entradas.");
        OnLeaderboardLoaded?.Invoke(entries);
    }

    private void OnError(PlayFabError error)
    {
        Debug.LogError($"[StatisticsService] ❌ Erro PlayFab: {error.GenerateErrorReport()}");
        OnStatisticsFailed?.Invoke(error);
    }

    private bool ValidateAuth()
    {
        if (PlayFabService.Instance == null || !PlayFabService.Instance.IsLoggedIn())
        {
            Debug.LogWarning("[StatisticsService] Login PlayFab ainda nao foi concluido.");
            return false;
        }

        return true;
    }
}