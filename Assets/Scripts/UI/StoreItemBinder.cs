using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StoreItemBinder : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text nameText;
    [SerializeField] private Text priceText;
    [SerializeField] private Image thumbnail;
    [SerializeField] private Button buyButton;

    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private StoreItemData boundData;

    public StoreItemData BoundData => boundData;

    public void ConfigureReferences(Text nameTextRef, Text priceTextRef, Image thumbnailRef, Button buyButtonRef)
    {
        nameText = nameTextRef ?? nameText;
        priceText = priceTextRef ?? priceText;
        thumbnail = thumbnailRef ?? thumbnail;
        buyButton = buyButtonRef ?? buyButton;
    }

    public void SetIcon(Sprite icon)
    {
        if (thumbnail == null)
        {
            return;
        }

        thumbnail.preserveAspect = true;
        thumbnail.sprite = icon;
        thumbnail.enabled = icon != null;
    }

    public void Bind(StoreItemData data, Action<StoreItemData> onBuy)
    {
        boundData = data;

        if (nameText != null)
        {
            nameText.text = data.displayName ?? string.Empty;
        }

        if (priceText != null)
        {
            priceText.text = string.IsNullOrEmpty(data.virtualCurrency)
                ? data.price.ToString()
                : $"{data.price} {data.virtualCurrency}";
        }

        buyButton?.onClick.RemoveAllListeners();
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(() => onBuy?.Invoke(boundData));
        }
    }

    private Sprite ResolveLocalSprite(StoreItemData data)
    {
        var cacheKey = string.IsNullOrWhiteSpace(data.iconKey) ? data.itemId : data.iconKey;
        if (!string.IsNullOrWhiteSpace(cacheKey) && spriteCache.TryGetValue(cacheKey, out var cachedSprite))
        {
            return cachedSprite;
        }

        Sprite sprite = null;

        if (!string.IsNullOrWhiteSpace(cacheKey))
        {
            sprite = Resources.Load<Sprite>($"StoreIcons/{cacheKey}") ?? Resources.Load<Sprite>(cacheKey);
        }

        if (sprite != null && !string.IsNullOrWhiteSpace(cacheKey) && !spriteCache.ContainsKey(cacheKey))
        {
            spriteCache.Add(cacheKey, sprite);
        }

        return sprite;
    }

    private void OnDestroy()
    {
        if (buyButton != null) buyButton.onClick.RemoveAllListeners();
    }
}
