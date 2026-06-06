using System;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectSkinCard : MonoBehaviour
{
    [SerializeField] private Image skinImage;
    [SerializeField] private TMP_Text skinNameText;
    [SerializeField] private Button equipButton;

    private ItemInstance currentSkin;
    private Action<string> onEquipRequested;

    private void Awake()
    {
        if (equipButton != null)
        {
            equipButton.onClick.AddListener(HandleEquipClicked);
        }
    }

    public void Setup(ItemInstance skin, Action<string> equipRequested, Sprite resolvedSprite = null)
    {
        currentSkin = skin;
        onEquipRequested = equipRequested;

        if (skinNameText != null)
        {
            skinNameText.text = skin != null && !string.IsNullOrWhiteSpace(skin.DisplayName)
                ? skin.DisplayName
                : skin?.ItemId ?? string.Empty;
        }

        if (skinImage != null && skin != null && !string.IsNullOrWhiteSpace(skin.ItemId))
        {
            var sprite = resolvedSprite != null
                ? resolvedSprite
                : Resources.Load<Sprite>($"AvatarImages/{skin.ItemId}");
            if (sprite == null)
            {
                sprite = ResolveSpriteByKey(skin.ItemId);
            }

            if (sprite != null)
            {
                skinImage.sprite = sprite;
            }
        }
    }

    private void HandleEquipClicked()
    {
        if (currentSkin == null || string.IsNullOrWhiteSpace(currentSkin.ItemId))
        {
            return;
        }

        onEquipRequested?.Invoke(currentSkin.ItemId);
    }

    private static Sprite ResolveSpriteByKey(string key)
    {
        var existing = Resources.Load<Sprite>($"AvatarImages/{key}");
        return existing;
    }
}
