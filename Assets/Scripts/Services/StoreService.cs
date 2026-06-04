using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using BrainDuel.Match;

public class PurchaseResult
{
    public bool Success { get; set; }
    public string ItemId { get; set; }
    public string CurrencyCode { get; set; }
    public int PriceDeducted { get; set; }
    public int NewBalance { get; set; }
    public string Error { get; set; }
}

public class StoreService : MonoBehaviour
{
    public static StoreService Instance { get; private set; }

    public static event Action<List<StoreItemData>> OnCatalogLoaded;
    public static event Action<PurchaseItemResult> OnPurchaseCompleted;
    public static event Action<PurchaseResult> OnPurchaseCompletedSecure;
    public static event Action<PlayFabError> OnStoreFailed;
    public static event Action<string> OnPurchaseFailed;

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

    public void LoadCatalog(string catalogVersion = null, string storeId = null, Action<List<StoreItemData>> onSuccess = null)
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
            }, result => OnLoadStoreSuccess(result, storeId, onSuccess), OnError);
            return;
        }

        PlayFabService.Client.GetCatalogItems(new GetCatalogItemsRequest
        {
            CatalogVersion = catalogVersion
        }, result => OnLoadCatalogSuccess(result, catalogVersion, onSuccess), OnError);
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

    /// <summary>
    /// Realiza compra via API direta do PlayFab (dedução + grant atômicos no servidor).
    /// </summary>
    public void PurchaseItemViaCloudScript(StoreItemData item, Action<PurchaseResult> onComplete = null)
    {
        if (!ValidateAuth())
        {
            var result = new PurchaseResult { Success = false, Error = "Not authenticated" };
            onComplete?.Invoke(result);
            OnPurchaseFailed?.Invoke("Sessão PlayFab não autenticada");
            return;
        }

        if (item == null)
        {
            var result = new PurchaseResult { Success = false, Error = "Item data is null" };
            onComplete?.Invoke(result);
            OnPurchaseFailed?.Invoke("Dados do item inválidos");
            return;
        }

        var request = new PurchaseItemRequest
        {
            ItemId          = item.itemId,
            VirtualCurrency = item.virtualCurrency,
            Price           = item.price,
            StoreId         = string.IsNullOrWhiteSpace(item.storeId) ? null : item.storeId
        };

        PlayFabService.Client.PurchaseItem(request,
            result => OnDirectPurchaseSuccess(result, item, onComplete),
            error  => OnCloudScriptPurchaseError(error, item, onComplete));

        Debug.Log($"[StoreService] Iniciando compra: ItemId={item.itemId}, Preço={item.price} {item.virtualCurrency}");
    }

    private void OnDirectPurchaseSuccess(PurchaseItemResult result, StoreItemData item, Action<PurchaseResult> onComplete)
    {
        Debug.Log($"[StoreService] ✅ Compra concluída: ItemId={item.itemId}");

        if (item.itemId.StartsWith("deck", StringComparison.OrdinalIgnoreCase))
        {
            // Deck: adiciona ao player_profile usando o ItemId exato do catálogo
            if (PlayerDataService.Instance != null)
                PlayerDataService.Instance.AddDeckToProfile(item.itemId);
            else
                Debug.LogWarning("[StoreService] PlayerDataService não encontrado para atualizar deck no perfil.");
        }
        else
        {
            // Power-up ou outro item
            var resolvedPowerUp = ResolvePowerUpFromItemId(item.itemId);
            if (resolvedPowerUp != PowerUpType.None)
            {
                if (PlayerDataService.Instance != null)
                    PlayerDataService.Instance.EquipPowerUp(resolvedPowerUp);
                else
                    Debug.LogWarning("[StoreService] PlayerDataService não encontrado para equipar power-up.");
            }
        }

        var successResult = new PurchaseResult
        {
            Success       = true,
            ItemId        = item.itemId,
            CurrencyCode  = item.virtualCurrency,
            PriceDeducted = item.price,
            NewBalance    = 0
        };

        onComplete?.Invoke(successResult);
        OnPurchaseCompletedSecure?.Invoke(successResult);
    }

    private void OnCloudScriptPurchaseError(PlayFabError error, StoreItemData item, Action<PurchaseResult> onComplete)
    {
        var errorMsg = error?.GenerateErrorReport() ?? "Erro ao processar compra";
        Debug.LogError($"[StoreService] ❌ Erro na compra: {errorMsg}");

        string friendly = "Falha ao processar compra";
        if (error?.Error == PlayFabErrorCode.InsufficientFunds)
            friendly = "Saldo insuficiente";
        else if (error?.Error == PlayFabErrorCode.ItemNotFound)
            friendly = "Item não encontrado";

        var purchaseResult = new PurchaseResult { Success = false, ItemId = item.itemId, Error = friendly };
        onComplete?.Invoke(purchaseResult);
        OnPurchaseFailed?.Invoke(friendly);
    }

    private void OnLoadCatalogSuccess(GetCatalogItemsResult result, string catalogVersion, Action<List<StoreItemData>> onSuccess)
    {
        var catalogItems = new List<StoreItemData>();

        if (result.Catalog != null)
        {
            foreach (var item in result.Catalog)
            {
                catalogItems.Add(new StoreItemData
                {
                    itemId = item.ItemId,
                    displayName = item.DisplayName,
                    description = item.Description,
                    iconKey = item.ItemId,
                    iconUrl = item.ItemImageUrl,
                    price = item.VirtualCurrencyPrices != null && item.VirtualCurrencyPrices.Count > 0
                        ? ExtractFirstPrice(item.VirtualCurrencyPrices)
                        : 0,
                    virtualCurrency = ExtractFirstCurrency(item.VirtualCurrencyPrices),
                    storeId = string.Empty,
                    tags = item.Tags != null ? new List<string>(item.Tags) : new List<string>()
                });
            }
        }

        // Apenas atualiza cache global quando carregando catálogo geral (sem storeId)
        cachedCatalog.Clear();
        cachedCatalog.AddRange(catalogItems);

        Debug.Log($"[StoreService] ✅ Catálogo carregado com {catalogItems.Count} itens.");
        OnCatalogLoaded?.Invoke(catalogItems);
        onSuccess?.Invoke(catalogItems);
    }

    private void OnLoadStoreSuccess(GetStoreItemsResult result, string storeId, Action<List<StoreItemData>> onSuccess)
    {
        // Usar lista local em vez de cachedCatalog para evitar conflitos entre lojas
        var storeItems = new List<StoreItemData>();

        if (result.Store != null)
        {
            foreach (var item in result.Store)
            {
                storeItems.Add(new StoreItemData
                {
                    itemId = item.ItemId,
                    displayName = item.ItemId,
                    description = string.Empty,
                    iconKey = item.ItemId,
                    iconUrl = string.Empty,
                    price = item.VirtualCurrencyPrices != null && item.VirtualCurrencyPrices.Count > 0
                        ? ExtractFirstPrice(item.VirtualCurrencyPrices)
                        : 0,
                    virtualCurrency = ExtractFirstCurrency(item.VirtualCurrencyPrices),
                    storeId = storeId,
                    tags = new List<string>()
                });
            }
        }

        Debug.Log($"[StoreService] ✅ Loja '{storeId}' carregada com {storeItems.Count} itens. Buscando detalhes do catálogo...");
        
        // GetStoreItems não retorna DisplayName, Description, ItemImageUrl
        // Precisamos carregar esses dados do catálogo
        PlayFabService.Client.GetCatalogItems(new GetCatalogItemsRequest
        {
            CatalogVersion = result.CatalogVersion ?? "mainCatalog"
        }, catalogResult => EnrichStoreItemsWithCatalogData(catalogResult, storeItems, onSuccess), OnError);
    }

    private void EnrichStoreItemsWithCatalogData(GetCatalogItemsResult catalogResult, List<StoreItemData> storeItems, Action<List<StoreItemData>> onSuccess)
    {
        if (catalogResult.Catalog != null)
        {
            var catalogMap = new Dictionary<string, CatalogItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var catalogItem in catalogResult.Catalog)
            {
                if (!catalogMap.ContainsKey(catalogItem.ItemId))
                {
                    catalogMap.Add(catalogItem.ItemId, catalogItem);
                }
            }

            // Atualizar os items da loja com dados do catálogo
            foreach (var storeItem in storeItems)
            {
                if (catalogMap.TryGetValue(storeItem.itemId, out var catalogItem))
                {
                    storeItem.displayName = catalogItem.DisplayName;
                    storeItem.description = catalogItem.Description;
                    storeItem.iconUrl = catalogItem.ItemImageUrl;
                    if (catalogItem.Tags != null)
                    {
                        storeItem.tags = new List<string>(catalogItem.Tags);
                    }
                }
            }
        }

        Debug.Log($"[StoreService] ✅ Dados de catálogo enriquecidos. {storeItems.Count} itens disponíveis.");
        onSuccess?.Invoke(storeItems);
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

    private static PowerUpType ResolvePowerUpFromItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return PowerUpType.None;
        switch (itemId.ToLowerInvariant())
        {
            case "itemescudosimples": return PowerUpType.SimpleShield;
            case "itemescudoduplo":  return PowerUpType.DoubleShield;
            case "itemeliminardois": return PowerUpType.EliminateTwo;
            case "itemaposta":       return PowerUpType.Bet;
            case "itemroubo":        return PowerUpType.Steal;
            default:                 return PowerUpType.None;
        }
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

/// <summary>
