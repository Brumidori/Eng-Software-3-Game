using System.Collections.Generic;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.InputSystem;

public class StoreTester : PlayFabTerminalTester
{
    private const string Title = "StoreTester";

    [SerializeField] private string fallbackStoreId = "loja_teste";
    [SerializeField] private string fallbackItemId = "One";
    [SerializeField] private string fallbackCurrency = "DA";
    [SerializeField] private int fallbackPrice = 0;
    [SerializeField] private string fallbackCatalogVersion = string.Empty;

    private readonly List<StoreItemData> cachedCatalog = new List<StoreItemData>();

    protected override void Start()
    {
        base.Start();
        EnsureService<StoreService>();
        StoreService.OnCatalogLoaded += HandleCatalogLoaded;
        StoreService.OnPurchaseCompleted += HandlePurchaseCompleted;
        StoreService.OnStoreFailed += HandleError;
        PrintReadyMessage(Title, $"1=carregar loja, 2=comprar item configurado, 3=comprar primeiro item carregado, 4=listar itens em cache | StoreId='{fallbackStoreId}' ItemId='{fallbackItemId}'");
    }

    private void OnDestroy()
    {
        StoreService.OnCatalogLoaded -= HandleCatalogLoaded;
        StoreService.OnPurchaseCompleted -= HandlePurchaseCompleted;
        StoreService.OnStoreFailed -= HandleError;
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
            StoreService.Instance.LoadCatalog(fallbackCatalogVersion, fallbackStoreId);
            Debug.Log($"[{Title}] Solicitado carregamento da loja '{fallbackStoreId}'.");
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            PurchaseConfiguredItem();
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            PurchaseFirstCachedItem();
        }
        else if (keyboard.digit4Key.wasPressedThisFrame)
        {
            Debug.Log($"[{Title}] Itens em cache: {cachedCatalog.Count}");
        }
    }

    private void PurchaseConfiguredItem()
    {
        if (string.IsNullOrWhiteSpace(fallbackItemId))
        {
            Debug.LogWarning($"[{Title}] Nenhum ItemId configurado no Inspector.");
            return;
        }

        var selectedCurrency = fallbackCurrency;
        var selectedPrice = fallbackPrice;
        var loadedItem = cachedCatalog.Find(i => i.itemId == fallbackItemId);

        if (loadedItem != null)
        {
            selectedCurrency = string.IsNullOrWhiteSpace(loadedItem.virtualCurrency) ? fallbackCurrency : loadedItem.virtualCurrency;
            selectedPrice = loadedItem.price;
            Debug.Log($"[{Title}] Compra configurada usando dados da loja carregada. Item='{fallbackItemId}' Price={selectedPrice} Currency='{selectedCurrency}'.");
        }
        else
        {
            Debug.LogWarning($"[{Title}] Item '{fallbackItemId}' nao encontrado no cache da loja. Usando fallback Price={selectedPrice} Currency='{selectedCurrency}'. Pressione 1 para carregar a loja antes da compra.");
        }

        StoreService.Instance.PurchaseItem(fallbackItemId, selectedCurrency, selectedPrice, fallbackCatalogVersion, fallbackStoreId);
        Debug.Log($"[{Title}] Solicitada compra do item '{fallbackItemId}' na loja '{fallbackStoreId}' com Price={selectedPrice} Currency='{selectedCurrency}'.");
    }

    private void PurchaseFirstCachedItem()
    {
        if (cachedCatalog.Count == 0)
        {
            Debug.LogWarning($"[{Title}] Nenhum item em cache para compra.");
            return;
        }

        var item = cachedCatalog[0];
        StoreService.Instance.PurchaseItem(item.itemId, string.IsNullOrWhiteSpace(item.virtualCurrency) ? fallbackCurrency : item.virtualCurrency, item.price, fallbackCatalogVersion, fallbackStoreId);
        Debug.Log($"[{Title}] Solicitada compra do primeiro item carregado da loja '{fallbackStoreId}'.");
    }

    private void HandleCatalogLoaded(List<StoreItemData> items)
    {
        cachedCatalog.Clear();
        cachedCatalog.AddRange(items);

        Debug.Log($"[{Title}] ✅ Catalogo carregado com {cachedCatalog.Count} itens.");

        foreach (var item in cachedCatalog)
        {
            Debug.Log($"[{Title}] Item: {item.displayName} | Id: {item.itemId} | Price: {item.price} {item.virtualCurrency}");
        }
    }

    private void HandlePurchaseCompleted(PurchaseItemResult result)
    {
        Debug.Log($"[{Title}] ✅ Compra concluida com {result.Items.Count} item(ns).");
    }

    private void HandleError(PlayFab.PlayFabError error)
    {
        Debug.LogError($"[{Title}] ❌ Erro PlayFab: {error.GenerateErrorReport()}");
    }
}