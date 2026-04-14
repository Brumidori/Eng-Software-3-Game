using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class StoreService : MonoBehaviour
{
    public static StoreService Instance { get; private set; }

    public static event Action<List<StoreItemData>> OnCatalogLoaded;
    public static event Action<PurchaseItemResult> OnPurchaseCompleted;
    public static event Action<PlayFabError> OnStoreFailed;

    private readonly List<StoreItemData> cachedCatalog = new List<StoreItemData>();

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

    public IReadOnlyList<StoreItemData> CachedCatalog => cachedCatalog;

    public void LoadCatalog(string catalogVersion = null, string storeId = null)
    {
        if (!ValidateAuth())
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(storeId))
        {
            PlayFabService.Client.GetStoreItems(new GetStoreItemsRequest
            {
                CatalogVersion = catalogVersion,
                StoreId = storeId
            }, OnLoadStoreSuccess, OnError);
            return;
        }

        PlayFabService.Client.GetCatalogItems(new GetCatalogItemsRequest
        {
            CatalogVersion = catalogVersion
        }, OnLoadCatalogSuccess, OnError);
    }

    public void PurchaseItem(string itemId, string virtualCurrency, int price, string catalogVersion = null, string storeId = null)
    {
        if (!ValidateAuth())
        {
            return;
        }

        var request = new PurchaseItemRequest
        {
            ItemId = itemId,
            VirtualCurrency = virtualCurrency,
            Price = price,
            CatalogVersion = catalogVersion,
            StoreId = storeId
        };

        PlayFabService.Client.PurchaseItem(request, OnPurchaseSuccess, OnError);
    }

    private void OnLoadCatalogSuccess(GetCatalogItemsResult result)
    {
        cachedCatalog.Clear();

        if (result.Catalog != null)
        {
            foreach (var item in result.Catalog)
            {
                cachedCatalog.Add(new StoreItemData
                {
                    itemId = item.ItemId,
                    displayName = item.DisplayName,
                    description = item.Description,
                    price = item.VirtualCurrencyPrices != null && item.VirtualCurrencyPrices.Count > 0
                        ? ExtractFirstPrice(item.VirtualCurrencyPrices)
                        : 0,
                    virtualCurrency = ExtractFirstCurrency(item.VirtualCurrencyPrices),
                    tags = item.Tags != null ? new List<string>(item.Tags) : new List<string>()
                });
            }
        }

        Debug.Log($"[StoreService] ✅ Catálogo carregado com {cachedCatalog.Count} itens.");
        OnCatalogLoaded?.Invoke(new List<StoreItemData>(cachedCatalog));
    }

    private void OnLoadStoreSuccess(GetStoreItemsResult result)
    {
        cachedCatalog.Clear();

        if (result.Store != null)
        {
            foreach (var item in result.Store)
            {
                cachedCatalog.Add(new StoreItemData
                {
                    itemId = item.ItemId,
                    displayName = item.ItemId,
                    description = string.Empty,
                    price = item.VirtualCurrencyPrices != null && item.VirtualCurrencyPrices.Count > 0
                        ? ExtractFirstPrice(item.VirtualCurrencyPrices)
                        : 0,
                    virtualCurrency = ExtractFirstCurrency(item.VirtualCurrencyPrices),
                    tags = new List<string>()
                });
            }
        }

        Debug.Log($"[StoreService] ✅ Loja carregada com {cachedCatalog.Count} itens.");
        OnCatalogLoaded?.Invoke(new List<StoreItemData>(cachedCatalog));
    }

    private void OnPurchaseSuccess(PurchaseItemResult result)
    {
        Debug.Log($"[StoreService] ✅ Compra concluída com {result.Items.Count} item(ns).");
        OnPurchaseCompleted?.Invoke(result);
    }

    private static int ExtractFirstPrice(Dictionary<string, uint> prices)
    {
        foreach (var pair in prices)
        {
            return (int)pair.Value;
        }

        return 0;
    }

    private static string ExtractFirstCurrency(Dictionary<string, uint> prices)
    {
        foreach (var pair in prices)
        {
            return pair.Key;
        }

        return string.Empty;
    }

    private bool ValidateAuth()
    {
        if (PlayFabService.Instance == null || !PlayFabService.Instance.IsLoggedIn())
        {
            Debug.LogWarning("[StoreService] Login PlayFab ainda nao foi concluido.");
            return false;
        }

        return true;
    }

    private void OnError(PlayFabError error)
    {
        Debug.LogError($"[StoreService] ❌ Erro PlayFab: {error.GenerateErrorReport()}");
        OnStoreFailed?.Invoke(error);
    }
}