using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AvatarSelectionModal : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Transform skinsContainer;
    [SerializeField] private SelectSkinCard skinCardPrefab;
    [SerializeField] private TMP_Text emptyStateText;
    [SerializeField] private TMP_Text titleText;

    [Header("Profile")]
    [SerializeField] private ProfileManager profileManager;
    [SerializeField] private ProfileUIBinder profileUIBinder;

    private readonly List<ItemInstance> ownedSkins = new List<ItemInstance>();
    private bool isVisible;
    private bool isRequestInFlight;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(Hide);
        }

        SetVisible(false);
    }

    private void OnEnable()
    {
        InventoryService.OnInventoryLoaded += HandleInventoryLoaded;
        InventoryService.OnInventoryFailed += HandleInventoryFailed;
    }

    private void OnDisable()
    {
        InventoryService.OnInventoryLoaded -= HandleInventoryLoaded;
        InventoryService.OnInventoryFailed -= HandleInventoryFailed;
    }

    public void Show()
    {
        EnsureDependencies();
        SetVisible(true);

        if (titleText != null)
        {
            titleText.text = "Selecionar skin";
        }

        LoadInventory();
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void EnsureDependencies()
    {
        if (profileManager == null)
        {
            profileManager = FindFirstObjectByType<ProfileManager>();
        }

        if (profileUIBinder == null)
        {
            profileUIBinder = profileManager != null
                ? profileManager.GetComponent<ProfileUIBinder>()
                : FindFirstObjectByType<ProfileUIBinder>();
        }

        if (skinCardPrefab == null)
        {
            Debug.LogError("[AvatarSelectionModal] skinCardPrefab não configurado.");
        }
    }

    private void LoadInventory()
    {
        if (InventoryService.Instance == null)
        {
            EnsureInventoryService();
        }

        if (InventoryService.Instance == null)
        {
            Debug.LogWarning("[AvatarSelectionModal] InventoryService indisponível.");
            PopulateSkins(Array.Empty<ItemInstance>());
            return;
        }

        isRequestInFlight = true;
        InventoryService.Instance.LoadInventory();
    }

    private void HandleInventoryLoaded(List<ItemInstance> items)
    {
        if (!isVisible || !isRequestInFlight)
        {
            return;
        }

        isRequestInFlight = false;
        ownedSkins.Clear();

        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (IsSkinItem(item))
                {
                    ownedSkins.Add(item);
                }
            }
        }

        PopulateSkins(ownedSkins);
    }

    private void HandleInventoryFailed(PlayFabError error)
    {
        if (!isVisible || !isRequestInFlight)
        {
            return;
        }

        isRequestInFlight = false;
        Debug.LogError(error.GenerateErrorReport());
        PopulateSkins(Array.Empty<ItemInstance>());
    }

    private void PopulateSkins(IEnumerable<ItemInstance> skins)
    {
        if (skinsContainer == null || skinCardPrefab == null)
        {
            return;
        }

        ClearCards();

        var hasAnySkin = false;
        foreach (var skin in skins)
        {
            if (skin == null || string.IsNullOrWhiteSpace(skin.ItemId))
            {
                continue;
            }

            hasAnySkin = true;
            var card = Instantiate(skinCardPrefab, skinsContainer);
            card.Setup(skin, HandleEquipRequested, ResolveSkinSprite(skin.ItemId));
        }

        if (emptyStateText != null)
        {
            emptyStateText.gameObject.SetActive(!hasAnySkin);
            emptyStateText.text = hasAnySkin ? string.Empty : "Nenhuma skin encontrada no inventário.";
        }
    }

    private void ClearCards()
    {
        if (skinsContainer == null)
        {
            return;
        }

        for (int i = skinsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(skinsContainer.GetChild(i).gameObject);
        }
    }

    private Sprite ResolveSkinSprite(string skinId)
    {
        if (profileUIBinder == null)
        {
            EnsureDependencies();
        }

        return profileUIBinder != null ? profileUIBinder.ResolveAvatarSprite(skinId) : null;
    }

    private void HandleEquipRequested(string skinId)
    {
        if (string.IsNullOrWhiteSpace(skinId) || isRequestInFlight)
        {
            return;
        }

        if (!IsOwnedSkin(skinId))
        {
            Debug.LogWarning($"[AvatarSelectionModal] Tentou equipar skin não possuída: {skinId}");
            return;
        }

        isRequestInFlight = true;

        PlayFabService.Client.ExecuteCloudScript(
            new ExecuteCloudScriptRequest
            {
                FunctionName = "EquipAvatarSkin",
                FunctionParameter = new Dictionary<string, object>
                {
                    { "avatarId", skinId }
                }
            },
            _ =>
            {
                isRequestInFlight = false;
                profileManager?.ApplyAvatarId(skinId);
                Hide();
            },
            error =>
            {
                isRequestInFlight = false;
                Debug.LogError($"[AvatarSelectionModal] Falha ao equipar skin: {error.GenerateErrorReport()}");
            });
    }

    private bool IsOwnedSkin(string skinId)
    {
        for (int i = 0; i < ownedSkins.Count; i++)
        {
            var item = ownedSkins[i];
            if (item != null && string.Equals(item.ItemId, skinId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSkinItem(ItemInstance item)
    {
        return item != null
               && !string.IsNullOrWhiteSpace(item.ItemId)
               && item.ItemId.StartsWith("skin", StringComparison.OrdinalIgnoreCase);
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    private static void EnsureInventoryService()
    {
        if (InventoryService.Instance != null)
        {
            return;
        }

        new GameObject("InventoryService").AddComponent<InventoryService>();
    }
}
