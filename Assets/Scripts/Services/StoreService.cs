using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

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
    /// Realiza compra via CloudScript (server-authoritative) para maior segurança.
    /// Valida saldo suficiente e existência do item no servidor.
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

        var parameters = new Dictionary<string, object>
        {
            { "itemId", item.itemId },
            { "virtualCurrency", item.virtualCurrency },
            { "price", item.price },
            { "storeId", item.storeId }
        };

        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "PurchaseItemSecure",
            FunctionParameter = parameters,
            GeneratePlayStreamEvent = true
        };

        PlayFabService.Client.ExecuteCloudScript(request,
            result => OnCloudScriptPurchaseSuccess(result, item, onComplete),
            error => OnCloudScriptPurchaseError(error, item, onComplete));

        Debug.Log($"[StoreService] Iniciando compra segura: ItemId={item.itemId}, Preço={item.price} {item.virtualCurrency}");
    }

    private void OnCloudScriptPurchaseSuccess(ExecuteCloudScriptResult result, StoreItemData item, Action<PurchaseResult> onComplete)
    {
        if (result.Error != null)
        {
            var errorMsg = result.Error.Message ?? "Erro desconhecido no CloudScript";
            Debug.LogError($"[StoreService] ❌ CloudScript retornou erro: {errorMsg}");
            
            var purchaseResult = new PurchaseResult
            {
                Success = false,
                ItemId = item.itemId,
                Error = errorMsg
            };
            
            onComplete?.Invoke(purchaseResult);
            OnPurchaseFailed?.Invoke(errorMsg);
            return;
        }

        var resultData = TryExtractResultData(result.FunctionResult);
        if (resultData == null)
        {
            Debug.LogError($"[StoreService] ❌ Resposta inesperada do CloudScript");
            
            var purchaseResult = new PurchaseResult
            {
                Success = false,
                ItemId = item.itemId,
                Error = "Resposta inválida do servidor"
            };
            
            onComplete?.Invoke(purchaseResult);
            OnPurchaseFailed?.Invoke("Resposta inválida do servidor");
            return;
        }

        var success = resultData.TryGetValue("success", out var successObj) && 
                     (successObj is bool successBool) && successBool;

        if (!success)
        {
            resultData.TryGetValue("error", out var errorObj);
            var errorMessage = errorObj?.ToString() ?? "Compra recusada";
            
            // Mapeamento de erros para mensagens amigáveis
            if (errorMessage == "insufficient_balance")
                errorMessage = "Saldo insuficiente";
            else if (errorMessage == "item_not_found")
                errorMessage = "Item não encontrado";

            Debug.LogWarning($"[StoreService] Compra rejeitada: {errorMessage}");

            resultData.TryGetValue("currentBalance", out var balanceObj);
            
            var purchaseResult = new PurchaseResult
            {
                Success = false,
                ItemId = item.itemId,
                CurrencyCode = item.virtualCurrency,
                Error = errorMessage,
                NewBalance = balanceObj is int balance ? balance : 0
            };
            
            onComplete?.Invoke(purchaseResult);
            OnPurchaseFailed?.Invoke(errorMessage);
            return;
        }

        // Sucesso na compra
        resultData.TryGetValue("newBalance", out var newBalanceObj);
        var newBalance = newBalanceObj is int nb ? nb : 0;

        Debug.Log($"[StoreService] ✅ Compra concluída com sucesso! ItemId={item.itemId}, Novo saldo={newBalance}");

        var successResult = new PurchaseResult
        {
            Success = true,
            ItemId = item.itemId,
            CurrencyCode = item.virtualCurrency,
            PriceDeducted = item.price,
            NewBalance = newBalance
        };

        onComplete?.Invoke(successResult);
        OnPurchaseCompletedSecure?.Invoke(successResult);
    }

    private void OnCloudScriptPurchaseError(PlayFabError error, StoreItemData item, Action<PurchaseResult> onComplete)
    {
        var errorMsg = error?.GenerateErrorReport() ?? "Erro ao comunicar com o servidor";
        Debug.LogError($"[StoreService] ❌ Erro na compra CloudScript: {errorMsg}");

        var purchaseResult = new PurchaseResult
        {
            Success = false,
            ItemId = item.itemId,
            Error = "Falha ao processar compra"
        };

        onComplete?.Invoke(purchaseResult);
        OnPurchaseFailed?.Invoke("Falha ao processar compra");
    }

    private Dictionary<string, object> TryExtractResultData(object functionResult)
    {
        if (functionResult == null)
            return null;

        // Se for Dictionary, retorna diretamente
        if (functionResult is Dictionary<string, object> dict)
            return dict;

        // Se for string (JSON), tenta parsear
        if (functionResult is string json)
        {
            try
            {
                var parsed = JsonUtility.FromJson<PurchaseResultJson>(json);
                return new Dictionary<string, object>
                {
                    { "success", parsed.success },
                    { "error", parsed.error },
                    { "itemId", parsed.itemId },
                    { "currencyCode", parsed.currencyCode },
                    { "newBalance", parsed.newBalance },
                    { "currentBalance", parsed.currentBalance }
                };
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[StoreService] Erro ao parsear JSON: {ex.Message}");
            }
        }

        return null;
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
/// Classe helper para parsear resposta JSON do CloudScript PurchaseItemSecure
/// </summary>
[System.Serializable]
public class PurchaseResultJson
{
    public bool success;
    public string error;
    public string itemId;
    public string currencyCode;
    public int priceDeducted;
    public int newBalance;
    public int currentBalance;
}