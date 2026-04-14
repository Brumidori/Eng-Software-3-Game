using System.Collections.Generic;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryTester : PlayFabTerminalTester
{
    private const string Title = "InventoryTester";

    [SerializeField] private string fallbackItemInstanceId = string.Empty;

    private readonly List<ItemInstance> cachedItems = new List<ItemInstance>();

    protected override void Start()
    {
        base.Start();
        EnsureService<InventoryService>();
        InventoryService.OnInventoryLoaded += HandleInventoryLoaded;
        InventoryService.OnItemConsumed += HandleItemConsumed;
        InventoryService.OnInventoryFailed += HandleError;
        PrintReadyMessage(Title, "1=carregar inventario, 2=consumir item configurado, 3=consumir primeiro item, 4=mostrar resumo");
    }

    private void OnDestroy()
    {
        InventoryService.OnInventoryLoaded -= HandleInventoryLoaded;
        InventoryService.OnItemConsumed -= HandleItemConsumed;
        InventoryService.OnInventoryFailed -= HandleError;
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
            InventoryService.Instance.LoadInventory();
            Debug.Log($"[{Title}] Solicitado carregamento do inventario.");
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            ConsumeConfiguredItem();
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            ConsumeFirstCachedItem();
        }
        else if (keyboard.digit4Key.wasPressedThisFrame)
        {
            Debug.Log($"[{Title}] Itens em cache: {cachedItems.Count}");
        }
    }

    private void ConsumeConfiguredItem()
    {
        if (string.IsNullOrWhiteSpace(fallbackItemInstanceId))
        {
            Debug.LogWarning($"[{Title}] Nenhum ItemInstanceId foi configurado no Inspector.");
            return;
        }

        InventoryService.Instance.ConsumeItem(fallbackItemInstanceId, 1);
        Debug.Log($"[{Title}] Solicitado consumo do item configurado.");
    }

    private void ConsumeFirstCachedItem()
    {
        if (cachedItems.Count == 0)
        {
            Debug.LogWarning($"[{Title}] Nenhum item em cache para consumo.");
            return;
        }

        InventoryService.Instance.ConsumeItem(cachedItems[0].ItemInstanceId, 1);
        Debug.Log($"[{Title}] Solicitado consumo do primeiro item em cache.");
    }

    private void HandleInventoryLoaded(List<ItemInstance> items)
    {
        cachedItems.Clear();
        cachedItems.AddRange(items);

        Debug.Log($"[{Title}] ✅ Inventario carregado com {cachedItems.Count} itens.");

        foreach (var item in cachedItems)
        {
            Debug.Log($"[{Title}] Item: {item.ItemId} | InstanceId: {item.ItemInstanceId} | Uses: {item.RemainingUses}");
        }
    }

    private void HandleItemConsumed(string itemInstanceId)
    {
        Debug.Log($"[{Title}] ✅ Item consumido: {itemInstanceId}");
    }

    private void HandleError(PlayFab.PlayFabError error)
    {
        Debug.LogError($"[{Title}] ❌ Erro PlayFab: {error.GenerateErrorReport()}");
    }
}