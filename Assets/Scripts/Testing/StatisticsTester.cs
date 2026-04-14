using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class StatisticsTester : PlayFabTerminalTester
{
    private const string Title = "StatisticsTester";

    [SerializeField] private int winScore = 100;
    [SerializeField] private int lossScore = 10;
    [SerializeField] private int leaderboardSize = 10;

    protected override void Start()
    {
        base.Start();
        EnsureService<StatisticsService>();
        StatisticsService.OnStatisticsUpdated += HandleUpdated;
        StatisticsService.OnLeaderboardLoaded += HandleLeaderboardLoaded;
        StatisticsService.OnStatisticsFailed += HandleError;
        PrintReadyMessage(Title, "1=registrar vitoria, 2=registrar derrota, 3=consultar leaderboard, 4=registrar pontuacao customizada");
    }

    private void OnDestroy()
    {
        StatisticsService.OnStatisticsUpdated -= HandleUpdated;
        StatisticsService.OnLeaderboardLoaded -= HandleLeaderboardLoaded;
        StatisticsService.OnStatisticsFailed -= HandleError;
    }

    private void Update()
    {
        if (!HasKeyboard())
        {
            return;
        }

        var keyboard = Keyboard.current;

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            StatisticsService.Instance.UpdateMatchStatistics(winScore, true);
            Debug.Log($"[{Title}] Solicitada estatistica de vitoria.");
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            StatisticsService.Instance.UpdateMatchStatistics(lossScore, false);
            Debug.Log($"[{Title}] Solicitada estatistica de derrota.");
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            StatisticsService.Instance.GetLeaderboard(leaderboardSize);
            Debug.Log($"[{Title}] Solicitada consulta de leaderboard.");
        }
        else if (keyboard.digit4Key.wasPressedThisFrame)
        {
            StatisticsService.Instance.UpdateMatchStatistics(250, true);
            Debug.Log($"[{Title}] Solicitada pontuacao customizada.");
        }
    }

    private void HandleUpdated()
    {
        Debug.Log($"[{Title}] ✅ Estatisticas atualizadas com sucesso.");
    }

    private void HandleLeaderboardLoaded(List<LeaderboardEntryData> entries)
    {
        Debug.Log($"[{Title}] ✅ Leaderboard recebido com {entries.Count} entradas.");

        foreach (var entry in entries)
        {
            Debug.Log($"[{Title}] #{entry.position} {entry.displayName ?? entry.playFabId} => {entry.statisticValue}");
        }
    }

    private void HandleError(PlayFab.PlayFabError error)
    {
        Debug.LogError($"[{Title}] ❌ Erro PlayFab: {error.GenerateErrorReport()}");
    }
}