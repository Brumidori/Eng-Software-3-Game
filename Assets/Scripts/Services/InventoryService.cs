using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class InventoryService : MonoBehaviour
{
    public static InventoryService Instance { get; private set; }

    public static event Action<List<ItemInstance>> OnInventoryLoaded;
    public static event Action<string> OnItemConsumed;
    public static event Action<PlayFabError> OnInventoryFailed;

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

    public void LoadInventory()
    {
        if (!ValidateAuth())
        {
            return;
        }

        PlayFabService.Client.GetUserInventory(new GetUserInventoryRequest(), OnLoadInventorySuccess, OnError);
    }

    public void ConsumeItem(string itemInstanceId, int consumeCount = 1)
    {
        if (!ValidateAuth())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(itemInstanceId))
        {
            Debug.LogError("[InventoryService] ItemInstanceId não pode ser vazio.");
            return;
        }

        var request = new ConsumeItemRequest
        {
            ItemInstanceId = itemInstanceId,
            ConsumeCount = consumeCount
        };

        PlayFabService.Client.ConsumeItem(request, result =>
        {
            Debug.Log($"[InventoryService] ✅ Item consumido: {result.ItemInstanceId} ({result.RemainingUses} restantes)");
            OnItemConsumed?.Invoke(result.ItemInstanceId);
        }, OnError);
    }

    private void OnLoadInventorySuccess(GetUserInventoryResult result)
    {
        List<ItemInstance> items = result.Inventory ?? new List<ItemInstance>();
        Debug.Log($"[InventoryService] ✅ Inventário carregado com {items.Count} itens.");
        OnInventoryLoaded?.Invoke(items);
    }

    private bool ValidateAuth()
    {
        if (PlayFabService.Instance == null || !PlayFabService.Instance.IsLoggedIn())
        {
            Debug.LogWarning("[InventoryService] Login PlayFab ainda nao foi concluido.");
            return false;
        }

        return true;
    }

    private void OnError(PlayFabError error)
    {
        Debug.LogError($"[InventoryService] ❌ Erro PlayFab: {error.GenerateErrorReport()}");
        OnInventoryFailed?.Invoke(error);
    }
}