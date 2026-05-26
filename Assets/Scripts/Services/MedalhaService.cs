using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using System;
using UnityEngine;

public class MedalhaService : MonoBehaviour
{
    public static MedalhaService Instance { get; private set; }

    private void Awake() => Instance = this;

    public void PegarEstatistica(string nomeDoCampo, Action<int> aoSucesso)
    {
        var request = new GetPlayerStatisticsRequest 
        { 
            StatisticNames = new List<string> { nomeDoCampo } 
        };

        PlayFabClientAPI.GetPlayerStatistics(request, 
            result => OnGetStatsSuccess(result, nomeDoCampo, aoSucesso), 
            error => Debug.LogError($"[PlayFab] Erro em {nomeDoCampo}: {error.ErrorMessage}")
        );
    }

    private void OnGetStatsSuccess(GetPlayerStatisticsResult result, string nome, Action<int> callback)
    {
        // Procura a estatística na lista; se não achar, assume 0
        var stat = result.Statistics.Find(s => s.StatisticName == nome);
        callback?.Invoke(stat?.Value ?? 0);
    }
}