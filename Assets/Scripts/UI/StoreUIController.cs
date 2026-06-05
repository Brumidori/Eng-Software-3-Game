using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.UI;

public class StoreUIController : MonoBehaviour
{
    private const float SixteenByTenAspect = 16f / 10f;
    private const float AspectTolerance = 0.035f;
    private const float BaseAspect = 16f / 9f;

    private enum CoinBundleSlot
    {
        Small,
        Medium,
        Big
    }

    [Serializable]
    private class ItemIconMapping
    {
        public string itemId;
        public Sprite icon;
    }

    [Header("Roots")]
    [SerializeField] private Transform itemsRoot;
    [SerializeField] private Transform decksRoot;
    [SerializeField] private Transform cosmeticsRoot;

    [Header("Purchase UI")]
    [SerializeField] private PurchaseConfirmationPanel purchaseConfirmationPanel;

    [Header("Scene Template")]
    [Tooltip("Card template already placed in the Scene. It will be cloned for each store item and should stay inactive or hidden.")]
    [SerializeField] private GameObject sceneItemTemplate;

    [Tooltip("Card template used for deck entries. It will be cloned for each deck item and should stay inactive or hidden.")]
    [SerializeField] private GameObject sceneDeckTemplate;

    [Tooltip("Card template used for cosmetic entries. It will be cloned for each cosmetic item and should stay inactive or hidden.")]
    [SerializeField] private GameObject sceneCosmeticTemplate;

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
    [SerializeField] private string cosmeticsStoreId = "cosmeticStore";
    [SerializeField] private string coinStoreId = "coinStore";
    [SerializeField] private string fallbackCoinCurrencyCode = "BC";

    [Header("Coin Store Modal")]
    [SerializeField] private CanvasGroup coinStoreModalGroup;
    [SerializeField] private Button coinStoreOpenButton;
    [SerializeField] private Button coinStoreCancelButton;
    [SerializeField] private Text coinSmallValueText;
    [SerializeField] private Text coinMediumValueText;
    [SerializeField] private Text coinBigValueText;
    [SerializeField] private Button coinSmallBuyButton;
    [SerializeField] private Button coinMediumBuyButton;
    [SerializeField] private Button coinBigBuyButton;

    private Dictionary<string, int> currentBalances = new Dictionary<string, int>();
    private Dictionary<string, Sprite> iconMap = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<CoinBundleSlot, StoreItemData> coinBundleMap = new Dictionary<CoinBundleSlot, StoreItemData>();
    private bool storeLoadsStarted;
    private bool coinStoreLoadStarted;
    private string lastLegacyPurchaseCurrency;
    private string lastLegacyPurchaseItemId;
    private string lastLegacyPurchaseStoreId;
    private HashSet<string> ownedDeckIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> ownedCosmeticIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool hasAppliedLayout;
    private bool lastAppliedCompactLayout;
    private float lastAppliedAspectRatio = -1f;
    private bool coinPurchasePending;
    private string pendingCoinCurrencyCode;

    private void Awake()
    {
        BuildIconMap();
        AutoFindRoots();
        TryLoadDefaultPrefabsFromResources();
        AutoAssignSceneTemplates();
        AutoFindCoinStoreUI();
        SetCoinStoreModalVisible(false);
        WireCoinStoreButtons();
    }

    private void OnEnable()
    {
        PlayFabService.OnLoginSuccess += HandlePlayFabLoginSuccess;
        StoreService.OnPurchaseCompleted += HandlePurchaseCompleted;
        StoreService.OnPurchaseCompletedSecure += HandlePurchaseCompletedSecure;
        StoreService.OnPurchaseFailed += HandlePurchaseFailed;
        InventoryService.OnInventoryLoaded += HandleInventoryLoaded;
        EconomyService.OnCurrencyChanged += HandleCurrencyChanged;
        EconomyService.OnEconomyFailed += HandleEconomyFailed;
    }

    private void OnDisable()
    {
        PlayFabService.OnLoginSuccess -= HandlePlayFabLoginSuccess;
        StoreService.OnPurchaseCompleted -= HandlePurchaseCompleted;
        StoreService.OnPurchaseCompletedSecure -= HandlePurchaseCompletedSecure;
        StoreService.OnPurchaseFailed -= HandlePurchaseFailed;
        InventoryService.OnInventoryLoaded -= HandleInventoryLoaded;
        EconomyService.OnCurrencyChanged -= HandleCurrencyChanged;
        EconomyService.OnEconomyFailed -= HandleEconomyFailed;
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

        if (cosmeticsRoot == null)
        {
            var cosmeticsGo = GameObject.Find("GridCosmeticStore");
            if (cosmeticsGo == null)
            {
                var cosmeticPanel = GameObject.Find("CardCosmeticStore");
                if (cosmeticPanel != null)
                {
                    cosmeticsGo = new GameObject("GridCosmeticStore", typeof(RectTransform));
                    cosmeticsGo.transform.SetParent(cosmeticPanel.transform, false);
                }
                else if (canvas != null)
                {
                    cosmeticsGo = new GameObject("GridCosmeticStore", typeof(RectTransform));
                    cosmeticsGo.transform.SetParent(canvas.transform, false);
                }
            }

            if (cosmeticsGo != null)
            {
                cosmeticsRoot = cosmeticsGo.transform;
            }
        }

        EnsureLayout(itemsRoot);
        EnsureLayout(decksRoot);
        EnsureLayout(cosmeticsRoot);
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

        ApplyResponsiveLayouts();
        TryLoadStoreContent();
        TryLoadCoinStoreContent();
    }

    private void Update()
    {
        ApplyResponsiveLayouts();
    }

    private void HandlePlayFabLoginSuccess()
    {
        TryLoadStoreContent();
        TryLoadCoinStoreContent();
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
        ClearChildren(cosmeticsRoot,
            sceneItemTemplate != null ? sceneItemTemplate.transform : null,
            sceneDeckTemplate != null ? sceneDeckTemplate.transform : null,
            sceneCosmeticTemplate != null ? sceneCosmeticTemplate.transform : null);

        ApplyResponsiveLayouts();

        StoreService.Instance.LoadCatalog(catalogVersion, itemsStoreId, HandleItemsStoreLoaded);
        StoreService.Instance.LoadCatalog(catalogVersion, decksStoreId, HandleDecksStoreLoaded);
        StoreService.Instance.LoadCatalog(catalogVersion, cosmeticsStoreId, HandleCosmeticsStoreLoaded);
    }

    private void TryLoadCoinStoreContent()
    {
        if (coinStoreLoadStarted)
        {
            return;
        }

        if (StoreService.Instance == null)
        {
            Debug.LogWarning("[StoreUIController] StoreService instance not found for coinStore.");
            return;
        }

        if (PlayFabService.Instance == null || !PlayFabService.Instance.IsLoggedIn())
        {
            Debug.Log("[StoreUIController] Aguardando login do PlayFab para carregar coinStore.");
            return;
        }

        coinStoreLoadStarted = true;
        StoreService.Instance.LoadCatalog(catalogVersion, coinStoreId, HandleCoinStoreLoaded);
    }

    private void HandleCoinStoreLoaded(List<StoreItemData> items)
    {
        coinBundleMap.Clear();

        if (items == null || items.Count == 0)
        {
            Debug.LogWarning("[StoreUIController] coinStore sem itens para renderizar no modal.");
            RenderCoinBundleTexts();
            UpdateCoinBuyButtonsInteractable();
            return;
        }

        StoreItemData small;
        StoreItemData medium;
        StoreItemData big;
        MapCoinBundles(items, out small, out medium, out big);

        if (small != null) coinBundleMap[CoinBundleSlot.Small] = small;
        if (medium != null) coinBundleMap[CoinBundleSlot.Medium] = medium;
        if (big != null) coinBundleMap[CoinBundleSlot.Big] = big;

        RenderCoinBundleTexts();
        UpdateCoinBuyButtonsInteractable();
    }

    private static string BuildCoinSearchSource(StoreItemData item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        return string.Format(
            "{0} {1} {2}",
            item.itemId ?? string.Empty,
            item.displayName ?? string.Empty,
            item.description ?? string.Empty
        ).ToLowerInvariant();
    }

    private static StoreItemData FindBundleByKeywords(List<StoreItemData> items, params string[] keywords)
    {
        if (items == null || keywords == null || keywords.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var haystack = BuildCoinSearchSource(item);

            for (int keywordIndex = 0; keywordIndex < keywords.Length; keywordIndex++)
            {
                var keyword = keywords[keywordIndex];
                if (!string.IsNullOrWhiteSpace(keyword) && haystack.Contains(keyword))
                {
                    return item;
                }
            }
        }

        return null;
    }

    private static bool ContainsReference(List<StoreItemData> items, StoreItemData candidate)
    {
        if (items == null || candidate == null)
        {
            return false;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddIfMissing(List<StoreItemData> items, StoreItemData candidate)
    {
        if (items == null || candidate == null)
        {
            return;
        }

        if (!ContainsReference(items, candidate))
        {
            items.Add(candidate);
        }
    }

    private static void MapCoinBundles(List<StoreItemData> items, out StoreItemData small, out StoreItemData medium, out StoreItemData big)
    {
        small = FindBundleByKeywords(items, "small", "pequeno", "mini", "basic");
        medium = FindBundleByKeywords(items, "medium", "medio", "médio", "standard", "normal");
        big = FindBundleByKeywords(items, "big", "large", "grande", "premium", "mega");

        var rankedByPrice = new List<StoreItemData>(items);
        rankedByPrice.Sort((a, b) =>
        {
            var left = a != null ? a.price : 0;
            var right = b != null ? b.price : 0;
            return left.CompareTo(right);
        });

        if (small == null && rankedByPrice.Count > 0)
        {
            small = rankedByPrice[0];
        }

        if (big == null && rankedByPrice.Count > 0)
        {
            big = rankedByPrice[rankedByPrice.Count - 1];
        }

        if (medium == null)
        {
            var candidates = new List<StoreItemData>();
            AddIfMissing(candidates, small);
            AddIfMissing(candidates, big);

            for (int i = 0; i < rankedByPrice.Count; i++)
            {
                var candidate = rankedByPrice[i];
                if (!ContainsReference(candidates, candidate))
                {
                    medium = candidate;
                    break;
                }
            }

            if (medium == null)
            {
                medium = small ?? big;
            }
        }
    }

    private void RenderCoinBundleTexts()
    {
        SetCoinBundleValueText(coinSmallValueText, CoinBundleSlot.Small);
        SetCoinBundleValueText(coinMediumValueText, CoinBundleSlot.Medium);
        SetCoinBundleValueText(coinBigValueText, CoinBundleSlot.Big);
    }

    private void SetCoinBundleValueText(Text target, CoinBundleSlot slot)
    {
        if (target == null)
        {
            return;
        }

        if (!coinBundleMap.TryGetValue(slot, out var item) || item == null)
        {
            target.text = "-";
            return;
        }

        var currency = string.IsNullOrWhiteSpace(item.virtualCurrency) ? fallbackCoinCurrencyCode : item.virtualCurrency;
        target.text = string.Format("{0} {1}", item.price, currency);
    }

    private void UpdateCoinBuyButtonsInteractable()
    {
        if (coinSmallBuyButton != null)
        {
            coinSmallBuyButton.interactable = coinBundleMap.ContainsKey(CoinBundleSlot.Small);
        }

        if (coinMediumBuyButton != null)
        {
            coinMediumBuyButton.interactable = coinBundleMap.ContainsKey(CoinBundleSlot.Medium);
        }

        if (coinBigBuyButton != null)
        {
            coinBigBuyButton.interactable = coinBundleMap.ContainsKey(CoinBundleSlot.Big);
        }
    }

    private void HandleCoinBundlePurchase(CoinBundleSlot slot)
    {
        if (!coinBundleMap.TryGetValue(slot, out var item) || item == null)
        {
            Debug.LogWarning($"[StoreUIController] Coin bundle '{slot}' não está mapeado.");
            return;
        }

        if (EconomyService.Instance == null)
        {
            Debug.LogWarning("[StoreUIController] EconomyService instance not found for coin purchase.");
            return;
        }

        var currencyCode = string.IsNullOrWhiteSpace(item.virtualCurrency) ? fallbackCoinCurrencyCode : item.virtualCurrency;
        var amount = Mathf.Max(0, item.price);
        if (amount <= 0)
        {
            Debug.LogWarning($"[StoreUIController] Valor inválido para pacote de moedas: {item.itemId}");
            return;
        }

        coinPurchasePending = true;
        pendingCoinCurrencyCode = currencyCode;

        if (PurchaseProgressManager.Instance != null)
        {
            var displayName = string.IsNullOrWhiteSpace(item.displayName) ? item.itemId : item.displayName;
            PurchaseProgressManager.Instance.StartPurchaseProgress(displayName);
        }

        EconomyService.Instance.AddCurrency(currencyCode, amount);
    }

    private void HandleCoinSmallBuyClicked()
    {
        HandleCoinBundlePurchase(CoinBundleSlot.Small);
    }

    private void HandleCoinMediumBuyClicked()
    {
        HandleCoinBundlePurchase(CoinBundleSlot.Medium);
    }

    private void HandleCoinBigBuyClicked()
    {
        HandleCoinBundlePurchase(CoinBundleSlot.Big);
    }

    private void HandleCurrencyChanged(string currencyCode, int newBalance)
    {
        UpdatePlayerBalance(currencyCode, newBalance);

        if (!coinPurchasePending)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(pendingCoinCurrencyCode) && !string.Equals(currencyCode, pendingCoinCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        coinPurchasePending = false;
        pendingCoinCurrencyCode = null;
        SetCoinStoreModalVisible(false);

        if (PurchaseProgressManager.Instance != null)
        {
            PurchaseProgressManager.Instance.EndPurchaseProgress("Moedas adicionadas!");
        }
    }

    private void HandleEconomyFailed(PlayFabError error)
    {
        if (!coinPurchasePending)
        {
            return;
        }

        coinPurchasePending = false;
        pendingCoinCurrencyCode = null;

        if (PurchaseProgressManager.Instance != null)
        {
            PurchaseProgressManager.Instance.EndPurchaseProgressWithError("Falha ao adicionar moedas");
        }
    }

    private void AutoFindCoinStoreUI()
    {
        if (coinStoreModalGroup == null)
        {
            coinStoreModalGroup = GameObject.Find("ModalCoinStore")?.GetComponent<CanvasGroup>();
        }

        if (coinStoreOpenButton == null)
        {
            coinStoreOpenButton = GameObject.Find("CoinStore")?.GetComponent<Button>();
        }

        if (coinStoreCancelButton == null)
        {
            coinStoreCancelButton = GameObject.Find("btnCancelarCompraCoin")?.GetComponent<Button>();
        }

        if (coinSmallValueText == null)
        {
            coinSmallValueText = GameObject.Find("txtValorItemCoinSmall")?.GetComponent<Text>();
        }

        if (coinMediumValueText == null)
        {
            coinMediumValueText = GameObject.Find("txtValorItemCoinMedium")?.GetComponent<Text>();
        }

        if (coinBigValueText == null)
        {
            coinBigValueText = GameObject.Find("txtValorItemCoinBig")?.GetComponent<Text>();
        }

        if (coinSmallBuyButton == null)
        {
            coinSmallBuyButton = GameObject.Find("btnComprarCoinSmall")?.GetComponent<Button>();
        }

        if (coinMediumBuyButton == null)
        {
            coinMediumBuyButton = GameObject.Find("btnComprarCoinMedium")?.GetComponent<Button>();
        }

        if (coinBigBuyButton == null)
        {
            coinBigBuyButton = GameObject.Find("btnComprarCoinBig")?.GetComponent<Button>();
        }
    }

    private void WireCoinStoreButtons()
    {
        if (coinStoreOpenButton != null)
        {
            coinStoreOpenButton.onClick.RemoveListener(OpenCoinStoreModal);
            coinStoreOpenButton.onClick.AddListener(OpenCoinStoreModal);
        }

        if (coinStoreCancelButton != null)
        {
            coinStoreCancelButton.onClick.RemoveListener(CloseCoinStoreModal);
            coinStoreCancelButton.onClick.AddListener(CloseCoinStoreModal);
        }

        if (coinSmallBuyButton != null)
        {
            coinSmallBuyButton.onClick.RemoveListener(HandleCoinSmallBuyClicked);
            coinSmallBuyButton.onClick.AddListener(HandleCoinSmallBuyClicked);
        }

        if (coinMediumBuyButton != null)
        {
            coinMediumBuyButton.onClick.RemoveListener(HandleCoinMediumBuyClicked);
            coinMediumBuyButton.onClick.AddListener(HandleCoinMediumBuyClicked);
        }

        if (coinBigBuyButton != null)
        {
            coinBigBuyButton.onClick.RemoveListener(HandleCoinBigBuyClicked);
            coinBigBuyButton.onClick.AddListener(HandleCoinBigBuyClicked);
        }
    }

    private void OpenCoinStoreModal()
    {
        SetCoinStoreModalVisible(true);

        if (!coinStoreLoadStarted)
        {
            TryLoadCoinStoreContent();
        }
    }

    private void CloseCoinStoreModal()
    {
        SetCoinStoreModalVisible(false);
    }

    private void SetCoinStoreModalVisible(bool visible)
    {
        if (coinStoreModalGroup == null)
        {
            return;
        }

        coinStoreModalGroup.alpha = visible ? 1f : 0f;
        coinStoreModalGroup.interactable = visible;
        coinStoreModalGroup.blocksRaycasts = visible;
    }

    private void HandleItemsStoreLoaded(List<StoreItemData> items)
    {
        PopulateRoot(itemsRoot, items, itemsStoreId);
    }

    private void HandleDecksStoreLoaded(List<StoreItemData> items)
    {
        PopulateRoot(decksRoot, items, decksStoreId);
    }

    private void HandleCosmeticsStoreLoaded(List<StoreItemData> items)
    {
        PopulateRoot(cosmeticsRoot, items, cosmeticsStoreId);
    }

    private void PopulateRoot(Transform root, List<StoreItemData> items, string sourceStoreId)
    {
        if (root == null || items == null)
        {
            return;
        }

        foreach (var item in items)
        {
            var isDeckStore = string.Equals(sourceStoreId, decksStoreId, StringComparison.OrdinalIgnoreCase);
            var isCosmeticStore = string.Equals(sourceStoreId, cosmeticsStoreId, StringComparison.OrdinalIgnoreCase);

            if ((isDeckStore || isCosmeticStore) && !string.IsNullOrWhiteSpace(item.itemId) && IsOwnedItem(sourceStoreId, item.itemId))
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
        var isCosmeticStore = string.Equals(sourceStoreId, cosmeticsStoreId, StringComparison.OrdinalIgnoreCase);
        var isDeckStore = string.Equals(sourceStoreId, decksStoreId, StringComparison.OrdinalIgnoreCase);
        var primaryTemplate = isCosmeticStore
            ? sceneCosmeticTemplate
            : (isDeckStore ? sceneDeckTemplate : sceneItemTemplate);
        var secondaryTemplate = isCosmeticStore
            ? sceneItemTemplate ?? sceneDeckTemplate
            : (isDeckStore ? sceneItemTemplate : sceneDeckTemplate);

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
                else if (string.Equals(item.storeId, cosmeticsStoreId, StringComparison.OrdinalIgnoreCase))
                {
                    ownedCosmeticIds.Add(item.itemId);
                    RemovePurchasedCosmeticCard(item.itemId);
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
        else if (string.Equals(lastLegacyPurchaseStoreId, cosmeticsStoreId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(lastLegacyPurchaseItemId))
        {
            ownedCosmeticIds.Add(lastLegacyPurchaseItemId);
            RemovePurchasedCosmeticCard(lastLegacyPurchaseItemId);
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
            if (!string.IsNullOrWhiteSpace(result.ItemId))
            {
                RemovePurchasedDeckCard(result.ItemId);
                RemovePurchasedCosmeticCard(result.ItemId);
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
        ownedCosmeticIds.Clear();
        foreach (var it in items)
        {
            if (it == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(it.ItemId)) continue;
            ownedDeckIds.Add(it.ItemId);
            ownedCosmeticIds.Add(it.ItemId);
        }

        FilterExistingOwnedCards(decksRoot, ownedDeckIds);
        FilterExistingOwnedCards(cosmeticsRoot, ownedCosmeticIds);
    }

    private void FilterExistingOwnedCards(Transform root, HashSet<string> ownedIds)
    {
        if (root == null || ownedIds == null) return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);

            // Skip templates or non-active
            if (child == null) continue;

            var binder = child.GetComponent<StoreItemBinder>();
            if (binder == null) continue;

            var bound = binder.BoundData;
            if (bound == null || string.IsNullOrWhiteSpace(bound.itemId)) continue;

            if (ownedIds.Contains(bound.itemId))
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void RemovePurchasedDeckCard(string itemId)
    {
        RemovePurchasedStoreCard(itemId, decksRoot, ownedDeckIds);
    }

    private void RemovePurchasedCosmeticCard(string itemId)
    {
        RemovePurchasedStoreCard(itemId, cosmeticsRoot, ownedCosmeticIds);
    }

    private static void RemovePurchasedStoreCard(string itemId, Transform root, HashSet<string> ownedIds)
    {
        if (string.IsNullOrWhiteSpace(itemId) || root == null || ownedIds == null)
        {
            return;
        }

        ownedIds.Add(itemId);

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (child == null) continue;
            var binder = child.GetComponent<StoreItemBinder>();
            if (binder == null) continue;
            var bound = binder.BoundData;
            if (bound != null && string.Equals(bound.itemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }

    private bool IsOwnedItem(string sourceStoreId, string itemId)
    {
        if (string.IsNullOrWhiteSpace(sourceStoreId) || string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (string.Equals(sourceStoreId, decksStoreId, StringComparison.OrdinalIgnoreCase))
        {
            return ownedDeckIds.Contains(itemId);
        }

        if (string.Equals(sourceStoreId, cosmeticsStoreId, StringComparison.OrdinalIgnoreCase))
        {
            return ownedCosmeticIds.Contains(itemId);
        }

        return false;
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

    private void ApplyResponsiveLayouts()
    {
        var currentAspectRatio = GetCurrentAspectRatio();
        var compactLayout = IsSixteenByTenLayout();
        if (hasAppliedLayout && compactLayout == lastAppliedCompactLayout && Mathf.Abs(currentAspectRatio - lastAppliedAspectRatio) <= 0.001f)
        {
            return;
        }

        hasAppliedLayout = true;
        lastAppliedCompactLayout = compactLayout;
        lastAppliedAspectRatio = currentAspectRatio;

        ConfigureLayout(itemsRoot, compactLayout, isDeckGrid: false, isCosmeticGrid: false);

        if (decksRoot != itemsRoot)
        {
            ConfigureLayout(decksRoot, compactLayout, isDeckGrid: true, isCosmeticGrid: false);
        }

        if (cosmeticsRoot != null && cosmeticsRoot != itemsRoot && cosmeticsRoot != decksRoot)
        {
            ConfigureLayout(cosmeticsRoot, compactLayout, isDeckGrid: true, isCosmeticGrid: true);
        }

        ApplyAspectSpacing(compactLayout, currentAspectRatio);
    }

    private float GetCurrentAspectRatio()
    {
        if (Screen.height <= 0)
        {
            return BaseAspect;
        }

        return (float)Screen.width / Screen.height;
    }

    private bool IsSixteenByTenLayout()
    {
        if (Screen.height <= 0)
        {
            return false;
        }

        var aspectRatio = (float)Screen.width / Screen.height;
        return Mathf.Abs(aspectRatio - SixteenByTenAspect) <= AspectTolerance;
    }

    private static void ConfigureLayout(Transform root, bool compactLayout, bool isDeckGrid, bool isCosmeticGrid)
    {
        if (root == null)
        {
            return;
        }

        var grid = root.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = root.gameObject.AddComponent<GridLayoutGroup>();
        }

        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = isCosmeticGrid ? TextAnchor.UpperLeft : TextAnchor.UpperCenter;
        if (isCosmeticGrid)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            if (root.GetComponent<RectMask2D>() == null)
            {
                root.gameObject.AddComponent<RectMask2D>();
            }
        }
        else
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 1;
        }

        if (compactLayout)
        {
            if (isCosmeticGrid)
            {
                grid.cellSize = new Vector2(292f, 299f);
                grid.spacing = new Vector2(10f, 10f);
                grid.padding = new RectOffset(0, 0, 0, 0);
            }
            else
            {
                grid.cellSize = isDeckGrid ? new Vector2(160f, 225f) : new Vector2(165f, 230f);
                grid.spacing = new Vector2(8f, 8f);
                grid.padding = new RectOffset(4, 4, 4, 4);
            }
        }
        else
        {
            if (isCosmeticGrid)
            {
                grid.cellSize = new Vector2(292f, 299f);
                grid.spacing = new Vector2(10f, 10f);
                grid.padding = new RectOffset(0, 0, 0, 0);
            }
            else
            {
                grid.cellSize = new Vector2(180f, 250f);
                grid.spacing = new Vector2(10f, 10f);
                grid.padding = new RectOffset(0, 0, 0, 0);
            }
        }

        if (root is RectTransform rootRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }
    }

    private void ApplyAspectSpacing(bool compactLayout, float aspectRatio)
    {
        ApplySceneRect("textDecks", compactLayout ? -38.865f : -44.865f, compactLayout ? 53.7505f : 53.7505f, compactLayout ? 286.998f : 284.998f);
        ApplySceneRect("GridIDecks", compactLayout ? -26.406982f : -8.406982f, compactLayout ? 4.814087f : 16.814087f, 0f);

        var cosmeticTitle = GameObject.Find("textCosmeticStore")?.GetComponent<RectTransform>();
        if (cosmeticTitle != null)
        {
            cosmeticTitle.anchoredPosition = new Vector2(compactLayout ? 0f : 0.81359863f, compactLayout ? 310.89612f : 316.89612f);
            cosmeticTitle.sizeDelta = new Vector2(compactLayout ? -54f : -46.3728f, compactLayout ? 44f : 49.3349f);
        }

        var cosmeticLabel = GameObject.Find("textCosmeticStore")?.GetComponent<UnityEngine.UI.Text>();
        if (cosmeticLabel != null)
        {
            cosmeticLabel.fontSize = compactLayout ? 32 : 40;
            cosmeticLabel.resizeTextForBestFit = compactLayout;
            cosmeticLabel.resizeTextMinSize = compactLayout ? 16 : 0;
            cosmeticLabel.horizontalOverflow = compactLayout ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        }

        var cosmeticGrid = GameObject.Find("GridCosmeticStore")?.GetComponent<RectTransform>();
        if (cosmeticGrid != null)
        {
            cosmeticGrid.anchorMin = new Vector2(0f, 0f);
            cosmeticGrid.anchorMax = new Vector2(1f, 1f);
            cosmeticGrid.pivot = new Vector2(0.5f, 1f);
            cosmeticGrid.offsetMin = new Vector2(8f, 0f);
            cosmeticGrid.offsetMax = new Vector2(-8f, compactLayout ? -104f : -112f);
        }
    }

    private static void ApplySceneRect(string objectName, float anchoredY, float height, float width)
    {
        var target = GameObject.Find(objectName);
        if (target == null)
        {
            return;
        }

        var rectTransform = target.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, anchoredY);
        rectTransform.sizeDelta = new Vector2(width, height);
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

        if (sceneCosmeticTemplate == null)
        {
            sceneCosmeticTemplate = FindTemplateByName(cosmeticsRoot, "CardCosmeticTemplate")
                ?? FindTemplateByName(itemsRoot, "CardCosmeticTemplate")
                ?? FindTemplateByName(decksRoot, "CardCosmeticTemplate");
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
