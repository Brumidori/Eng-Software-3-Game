using System.Collections.Generic;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.InputSystem;

public class TitleDataTester : PlayFabTerminalTester
{
    private const string Title = "TitleDataTester";

    [SerializeField] private string customKey = "game_config";

    protected override void Start()
    {
        base.Start();
        PrintReadyMessage(Title, "1=ler deck_index, 2=ler chave customizada, 3=ler cartas_esportes, 4=mostrar chave customizada atual");
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
            LoadKey("deck_index");
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            LoadKey(customKey);
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            LoadKey("cartas_esportes");
        }
        else if (keyboard.digit4Key.wasPressedThisFrame)
        {
            Debug.Log($"[{Title}] Chave customizada atual: {customKey}");
        }
    }

    private void LoadKey(string key)
    {
        PlayFabService.Client.GetTitleData(new GetTitleDataRequest
        {
            Keys = new List<string> { key }
        },
        result => HandleSuccess(key, result),
        error => Debug.LogError($"[{Title}] ❌ Erro PlayFab ao ler '{key}': {error.GenerateErrorReport()}")
        );

        Debug.Log($"[{Title}] Solicitada leitura da chave '{key}'.");
    }

    private void HandleSuccess(string key, GetTitleDataResult result)
    {
        if (result.Data != null && result.Data.TryGetValue(key, out string value))
        {
            Debug.Log($"[{Title}] ✅ {key}: {value}");
            return;
        }

        Debug.LogWarning($"[{Title}] Nenhum valor encontrado para '{key}'.");
    }
}