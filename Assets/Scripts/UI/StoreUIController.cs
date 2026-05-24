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

    [Tooltip("Card template used for deck entries. It will be cloned for each deck item and should stay inactive or hidden.")]
    [SerializeField] private GameObject sceneDeckTemplate;

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
    private string lastLegacyPurchaseItemId;
    private string lastLegacyPurchaseStoreId;
    private HashSet<string> ownedDeckIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        BuildIconMap();
        AutoFindRoots();
        TryLoadDefaultPrefabsFromResources();
        AutoAssignSceneTemplates();
    }

    private void OnEnable()
    {
        PlayFabService.OnLoginSuccess += HandlePlayFabLoginSuccess;
        StoreService.OnPurchaseCompleted += HandlePurchaseCompleted;
        StoreService.OnPurchaseCompletedSecure += HandlePurchaseCompletedSecure;
        StoreService.OnPurchaseFailed += HandlePurchaseFailed;
        InventoryService.OnInventoryLoaded += HandleInventoryLoaded;
    }

    private void OnDisable()
    {
        PlayFabService.OnLoginSuccess -= HandlePlayFabLoginSuccess;
        StoreService.OnPurchaseCompleted -= HandlePurchaseCompleted;
        StoreService.OnPurchaseCompletedSecure -= HandlePurchaseCompletedSecure;
        StoreService.OnPurchaseFailed -= HandlePurchaseFailed;
        InventoryService.OnInventoryLoaded -= HandleInventoryLoaded;
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

        // Trigger inventory load so we can filter owned decks
        if (InventoryService.Instance != null)
        {
            InventoryService.Instance.LoadInventory();
        }

        ClearChildren(itemsRoot,
            sceneItemTemplate != null ? sceneItemTemplate.transform : null,
            sceneDeckTemplate != null ? sceneDeckTemplate.transform : null);
        ClearChildren(decksRoot,
            sceneItemTemplate != null ? sceneItemTemplate.transform : null,
            sceneDeckTemplate != null ? sceneDeckTemplate.transform : null);

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
            // Skip decks already owned by the player
            var isDeckStore = string.Equals(sourceStoreId, decksStoreId, StringComparison.OrdinalIgnoreCase);
            if (isDeckStore && !string.IsNullOrWhiteSpace(item.itemId) && ownedDeckIds.Contains(item.itemId))
            {
                continue;
            }
            var visualTemplate = ResolveVisualTemplate(item, sourceStoreId);

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

    private GameObject ResolveVisualTemplate(StoreItemData item, string sourceStoreId)
    {
        var isDeckStore = string.Equals(sourceStoreId, decksStoreId, StringComparison.OrdinalIgnoreCase);
        var primaryTemplate = isDeckStore ? sceneDeckTemplate : sceneItemTemplate;
        var secondaryTemplate = isDeckStore ? sceneItemTemplate : sceneDeckTemplate;

        if (primaryTemplate != null)
        {
            return primaryTemplate;
        }

        if (secondaryTemplate != null)
        {
            return secondaryTemplate;
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
            lastLegacyPurchaseItemId = item.itemId;
            lastLegacyPurchaseStoreId = item.storeId;
            StoreService.Instance.PurchaseItem(item.itemId, item.virtualCurrency, item.price, catalogVersion, item.storeId);
            return;
        }

        // Mostrar modal de confirmação
        purchaseConfirmationPanel.Show(
            item,
            onConfirm: OnPurchaseConfirmed,
            onCancel: () => { }
        );
    }

    private void OnPurchaseConfirmed(StoreItemData item)
    {
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
                if (string.Equals(item.storeId, decksStoreId, StringComparison.OrdinalIgnoreCase))
                {
                    ownedDeckIds.Add(item.itemId);
                    RemovePurchasedDeckCard(item.itemId);
                }

                UpdatePlayerBalance(item.virtualCurrency, result.NewBalance);
                RequestEconomyBalanceRefresh(item.virtualCurrency);
            }
            else
            {
            }
        });
    }

    private void HandlePurchaseCompleted(PurchaseItemResult result)
    {
        if (string.IsNullOrWhiteSpace(lastLegacyPurchaseCurrency))
        {
            return;
        }

        if (string.Equals(lastLegacyPurchaseStoreId, decksStoreId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(lastLegacyPurchaseItemId))
        {
            ownedDeckIds.Add(lastLegacyPurchaseItemId);
            RemovePurchasedDeckCard(lastLegacyPurchaseItemId);
        }

        RequestEconomyBalanceRefresh(lastLegacyPurchaseCurrency);
        lastLegacyPurchaseCurrency = null;
        lastLegacyPurchaseItemId = null;
        lastLegacyPurchaseStoreId = null;
    }

    private void HandlePurchaseCompletedSecure(PurchaseResult result)
    {
        if (result.Success)
        {
            // If this purchase was a deck, remove it from the UI immediately
            if (!string.IsNullOrWhiteSpace(result.ItemId))
            {
                RemovePurchasedDeckCard(result.ItemId);
            }
            UpdatePlayerBalance(result.CurrencyCode, result.NewBalance);
            RequestEconomyBalanceRefresh(result.CurrencyCode);
        }
    }

    private void HandlePurchaseFailed(string errorMessage)
    {
    }

    private void HandleInventoryLoaded(List<ItemInstance> items)
    {
        if (items == null)
        {
            return;
        }

        ownedDeckIds.Clear();
        foreach (var it in items)
        {
            if (it == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(it.ItemId)) continue;
            ownedDeckIds.Add(it.ItemId);
        }

        // Remove any deck cards already present in the UI
        FilterExistingDeckCards();
    }

    private void FilterExistingDeckCards()
    {
        if (decksRoot == null) return;

        for (int i = decksRoot.childCount - 1; i >= 0; i--)
        {
            var child = decksRoot.GetChild(i);

            // Skip templates or non-active
            if (child == null) continue;

            var binder = child.GetComponent<StoreItemBinder>();
            if (binder == null) continue;

            var bound = binder.BoundData;
            if (bound == null || string.IsNullOrWhiteSpace(bound.itemId)) continue;

            if (ownedDeckIds.Contains(bound.itemId))
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void RemovePurchasedDeckCard(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || decksRoot == null)
        {
            return;
        }

        ownedDeckIds.Add(itemId);

        for (int i = decksRoot.childCount - 1; i >= 0; i--)
        {
            var child = decksRoot.GetChild(i);
            if (child == null) continue;
            var binder = child.GetComponent<StoreItemBinder>();
            if (binder == null) continue;
            var bound = binder.BoundData;
            if (bound != null && string.Equals(bound.itemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                Destroy(child.gameObject);
            }
        }
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

    private void AutoAssignSceneTemplates()
    {
        if (sceneItemTemplate == null)
        {
            sceneItemTemplate = FindTemplateByName(itemsRoot, "CardItemTemplate") ?? FindTemplateByName(decksRoot, "CardItemTemplate");
        }

        if (sceneDeckTemplate == null)
        {
            sceneDeckTemplate = FindTemplateByName(itemsRoot, "CardDeckTemplate") ?? FindTemplateByName(decksRoot, "CardDeckTemplate");
        }
    }

    private static GameObject FindTemplateByName(Transform root, string templateName)
    {
        if (root == null || string.IsNullOrWhiteSpace(templateName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (string.Equals(child.name, templateName, StringComparison.OrdinalIgnoreCase))
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static void ClearChildren(Transform root, params Transform[] preservedChildren)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (preservedChildren != null)
            {
                var shouldPreserve = false;
                for (int preservedIndex = 0; preservedIndex < preservedChildren.Length; preservedIndex++)
                {
                    if (preservedChildren[preservedIndex] != null && child == preservedChildren[preservedIndex])
                    {
                        shouldPreserve = true;
                        break;
                    }
                }

                if (shouldPreserve)
                {
                    continue;
                }
            }

            Destroy(child.gameObject);
        }
    }
}
