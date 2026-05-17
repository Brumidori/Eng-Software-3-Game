using System;
using System.Collections.Generic;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.UI;

public class StoreUIController : MonoBehaviour
{
    [Serializable]
    private class ItemIconMapping
    {
        public string itemId;
        public Sprite icon;
    }

    [Header("Roots")]
    [SerializeField] private Transform itemsRoot;
    [SerializeField] private Transform decksRoot;

    [Header("Purchase UI")]
    [SerializeField] private PurchaseConfirmationPanel purchaseConfirmationPanel;

    [Header("Scene Template")]
    [Tooltip("Card template already placed in the Scene. It will be cloned for each store item and should stay inactive or hidden.")]
    [SerializeField] private GameObject sceneItemTemplate;

    [Header("Optional Fallback Prefabs")]
    [Tooltip("Optional prefabs kept only as fallback. The card template from the Scene has priority.")]
    [SerializeField] private List<GameObject> availablePrefabs = new List<GameObject>();

    [Header("Item Icons")]
    [Tooltip("Mapeie o ItemId do PlayFab para o Sprite correspondente. Use este campo para os sprites da pasta Sprites/Store/ItemsIcons.")]
    [SerializeField] private List<ItemIconMapping> itemIcons = new List<ItemIconMapping>();

    [Header("PlayFab Sources")]
    [SerializeField] private string catalogVersion = "mainCatalog";
    [SerializeField] private string itemsStoreId = "itensStore";
    [SerializeField] private string decksStoreId = "decksStore";

    private Dictionary<string, int> currentBalances = new Dictionary<string, int>();
    private Dictionary<string, Sprite> iconMap = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private bool storeLoadsStarted;
    private string lastLegacyPurchaseCurrency;

    private void Awake()
    {
        BuildIconMap();
        AutoFindRoots();
        TryLoadDefaultPrefabsFromResources();
        AutoAssignSceneTemplate();
    }

    private void OnEnable()
    {
        PlayFabService.OnLoginSuccess += HandlePlayFabLoginSuccess;
        StoreService.OnPurchaseCompleted += HandlePurchaseCompleted;
        StoreService.OnPurchaseCompletedSecure += HandlePurchaseCompletedSecure;
        StoreService.OnPurchaseFailed += HandlePurchaseFailed;
    }

    private void OnDisable()
    {
        PlayFabService.OnLoginSuccess -= HandlePlayFabLoginSuccess;
        StoreService.OnPurchaseCompleted -= HandlePurchaseCompleted;
        StoreService.OnPurchaseCompletedSecure -= HandlePurchaseCompletedSecure;
        StoreService.OnPurchaseFailed -= HandlePurchaseFailed;
    }

    private void AutoFindRoots()
    {
        var canvas = FindFirstObjectByType<Canvas>();

        if (itemsRoot == null)
        {
            var itemsGo = GameObject.Find("CardMainStore");
            if (itemsGo == null && canvas != null)
            {
                itemsGo = new GameObject("CardMainStore", typeof(RectTransform));
                itemsGo.transform.SetParent(canvas.transform, false);
            }

            if (itemsGo != null) itemsRoot = itemsGo.transform;
        }

        if (decksRoot == null)
        {
            var decksGo = GameObject.Find("DecksRoot");
            if (decksGo == null && canvas != null)
            {
                decksGo = new GameObject("DecksRoot", typeof(RectTransform));
                decksGo.transform.SetParent(canvas.transform, false);
            }

            if (decksGo != null)
            {
                decksRoot = decksGo.transform;
            }
            else
            {
                decksRoot = itemsRoot;
            }
        }

        EnsureLayout(itemsRoot);
        EnsureLayout(decksRoot);
    }

    private void EnsureLayout(Transform root)
    {
        if (root == null) return;
        var lg = root.GetComponent<UnityEngine.UI.LayoutGroup>();
        if (lg == null)
        {
            var grid = root.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(200, 300);
            grid.spacing = new Vector2(10, 10);
        }
    }

    private void TryLoadDefaultPrefabsFromResources()
    {
        if (availablePrefabs == null) availablePrefabs = new List<GameObject>();
        if (availablePrefabs.Count == 0)
        {
            var res = Resources.Load<GameObject>("Prefabs/StoreItemCard");
            if (res != null)
            {
                availablePrefabs.Add(res);
            }
        }
    }

    private void Start()
    {
        if (StoreService.Instance == null)
        {
            Debug.LogWarning("[StoreUIController] StoreService instance not found. Ensure StoreService exists in the scene.");
            return;
        }

        TryLoadStoreContent();
    }

    private void HandlePlayFabLoginSuccess()
    {
        TryLoadStoreContent();
    }

    private void TryLoadStoreContent()
    {
        if (storeLoadsStarted)
        {
            return;
        }

        if (StoreService.Instance == null)
        {
            Debug.LogWarning("[StoreUIController] StoreService instance not found. Ensure StoreService exists in the scene.");
            return;
        }

        if (PlayFabService.Instance == null || !PlayFabService.Instance.IsLoggedIn())
        {
            Debug.Log("[StoreUIController] Aguardando login do PlayFab para carregar a loja.");
            return;
        }

        storeLoadsStarted = true;

        ClearChildren(itemsRoot, sceneItemTemplate != null ? sceneItemTemplate.transform : null);
        ClearChildren(decksRoot, null);

        StoreService.Instance.LoadCatalog(catalogVersion, itemsStoreId, HandleItemsStoreLoaded);
        StoreService.Instance.LoadCatalog(catalogVersion, decksStoreId, HandleDecksStoreLoaded);
    }

    private void HandleItemsStoreLoaded(List<StoreItemData> items)
    {
        PopulateRoot(itemsRoot, items, itemsStoreId);
    }

    private void HandleDecksStoreLoaded(List<StoreItemData> items)
    {
        PopulateRoot(decksRoot, items, decksStoreId);
    }

    private void PopulateRoot(Transform root, List<StoreItemData> items, string sourceStoreId)
    {
        if (root == null || items == null)
        {
            return;
        }

        foreach (var item in items)
        {
            var visualTemplate = ResolveVisualTemplate(item);

            if (visualTemplate != null)
            {
                var go = Instantiate(visualTemplate, root);
                var binder = go.GetComponent<StoreItemBinder>();

                if (binder == null)
                {
                    binder = go.AddComponent<StoreItemBinder>();

                    var nameText = go.transform.Find("NameText")?.GetComponent<Text>();
                    var priceText = go.transform.Find("PriceText")?.GetComponent<Text>();
                    var thumb = go.GetComponent<Image>();
                    var buyButton = go.transform.Find("BuyButton")?.GetComponent<Button>();

                    binder.ConfigureReferences(nameText, priceText, thumb, buyButton);
                }

                if (binder != null)
                {
                    item.storeId = sourceStoreId;
                    binder.Bind(item, OnBuyRequested);
                    binder.SetIcon(ResolveIcon(item));
                }
                else
                {
                    Debug.LogWarning($"[StoreUIController] Template '{visualTemplate.name}' does not contain StoreItemBinder and auto-wire failed.");
                }
            }
            else
            {
                Debug.LogWarning($"[StoreUIController] Nenhum prefab encontrado para ItemId '{item.itemId}'. Ignorando.");
            }
        }
    }

    private GameObject ResolveVisualTemplate(StoreItemData item)
    {
        if (sceneItemTemplate != null)
        {
            return sceneItemTemplate;
        }

        if (item != null && !string.IsNullOrWhiteSpace(item.itemId))
        {
            foreach (var prefab in availablePrefabs)
            {
                if (prefab != null && string.Equals(prefab.name, item.itemId, StringComparison.OrdinalIgnoreCase))
                {
                    return prefab;
                }
            }
        }

        return null;
    }

    private void BuildIconMap()
    {
        iconMap.Clear();

        if (itemIcons == null)
        {
            return;
        }

        foreach (var entry in itemIcons)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemId) || entry.icon == null)
            {
                continue;
            }

            if (!iconMap.ContainsKey(entry.itemId))
            {
                iconMap.Add(entry.itemId, entry.icon);
            }
        }
    }

    private Sprite ResolveIcon(StoreItemData item)
    {
        if (item == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(item.iconKey) && iconMap.TryGetValue(item.iconKey, out var iconFromKey))
        {
            return iconFromKey;
        }

        if (!string.IsNullOrWhiteSpace(item.itemId) && iconMap.TryGetValue(item.itemId, out var iconFromItemId))
        {
            return iconFromItemId;
        }

        return null;
    }

    private void OnBuyRequested(StoreItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("[StoreUIController] Item data is null");
            return;
        }

        if (purchaseConfirmationPanel == null)
        {
            Debug.LogWarning("[StoreUIController] PurchaseConfirmationPanel not found. Falling back to direct purchase.");
            lastLegacyPurchaseCurrency = item.virtualCurrency;
            StoreService.Instance.PurchaseItem(item.itemId, item.virtualCurrency, item.price, catalogVersion, item.storeId);
            return;
        }

        // Obter saldo atual do jogador para a moeda deste item
        var currentBalance = GetPlayerBalance(item.virtualCurrency);

        // Mostrar modal de confirmação
        purchaseConfirmationPanel.Show(
            item,
            currentBalance,
            onConfirm: OnPurchaseConfirmed,
            onCancel: () => Debug.Log($"[StoreUIController] Compra de {item.itemId} cancelada pelo usuário")
        );
    }

    private void OnPurchaseConfirmed(StoreItemData item)
    {
        Debug.Log($"[StoreUIController] Iniciando compra segura de {item.itemId}");

        // Mostrar progress/loading
        if (PurchaseProgressManager.Instance != null)
        {
            PurchaseProgressManager.Instance.StartPurchaseProgress(item.displayName);
        }

        // Executar compra via CloudScript
        StoreService.Instance.PurchaseItemViaCloudScript(item, result =>
        {
            if (result.Success)
            {
                Debug.Log($"[StoreUIController] ✅ Compra bem-sucedida!");
                UpdatePlayerBalance(item.virtualCurrency, result.NewBalance);
                RequestEconomyBalanceRefresh(item.virtualCurrency);
            }
            else
            {
                Debug.LogWarning($"[StoreUIController] ❌ Compra falhou: {result.Error}");
            }
        });
    }

    private void HandlePurchaseCompleted(PurchaseItemResult result)
    {
        if (string.IsNullOrWhiteSpace(lastLegacyPurchaseCurrency))
        {
            return;
        }

        RequestEconomyBalanceRefresh(lastLegacyPurchaseCurrency);
        lastLegacyPurchaseCurrency = null;
    }

    private void HandlePurchaseCompletedSecure(PurchaseResult result)
    {
        if (result.Success)
        {
            Debug.Log($"[StoreUIController] Compra concluída com sucesso! Novo saldo: {result.NewBalance} {result.CurrencyCode}");
            UpdatePlayerBalance(result.CurrencyCode, result.NewBalance);
            RequestEconomyBalanceRefresh(result.CurrencyCode);
        }
    }

    private void HandlePurchaseFailed(string errorMessage)
    {
        Debug.LogError($"[StoreUIController] Compra falhou: {errorMessage}");
    }

    private int GetPlayerBalance(string currencyCode)
    {
        if (currentBalances.TryGetValue(currencyCode, out var balance))
            return balance;

        return 0;
    }

    private void UpdatePlayerBalance(string currencyCode, int newBalance)
    {
        currentBalances[currencyCode] = newBalance;
        Debug.Log($"[StoreUIController] Saldo atualizado: {currencyCode} = {newBalance}");
    }

    private void RequestEconomyBalanceRefresh(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return;
        }

        if (EconomyService.Instance == null)
        {
            Debug.LogWarning("[StoreUIController] EconomyService nao encontrado para refresh de saldo.");
            return;
        }

        EconomyService.Instance.GetBalance(currencyCode);
    }

    private void AutoAssignSceneTemplate()
    {
        if (sceneItemTemplate != null)
        {
            return;
        }

        if (itemsRoot == null)
        {
            return;
        }

        for (int i = 0; i < itemsRoot.childCount; i++)
        {
            var child = itemsRoot.GetChild(i);
            if (child.GetComponent<StoreItemBinder>() != null)
            {
                sceneItemTemplate = child.gameObject;
                return;
            }
        }
    }

    private static void ClearChildren(Transform root, Transform preservedChild)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (preservedChild != null && child == preservedChild)
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }
}
