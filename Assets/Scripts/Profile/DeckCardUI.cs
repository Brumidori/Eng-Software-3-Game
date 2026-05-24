using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckCardUI : MonoBehaviour
{
    [Header("Deck")]
    [SerializeField] private TMP_Text deckNameText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject equippedIndicator;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private Button equipButton;
    [SerializeField] private ProfileManager profileManager;

    [Header("Icon Sprites")]
    [SerializeField] private List<NamedSprite> iconSprites = new List<NamedSprite>();

    public void Setup(PlayerDeckData deck, ProfileManager manager = null)
    {
        if (deck == null)
        {
            return;
        }

        if (manager != null)
        {
            profileManager = manager;
        }

        if (deckNameText != null)
        {
            deckNameText.text = string.IsNullOrWhiteSpace(deck.category) ? "DECK" : deck.category;
        }

        if (equippedIndicator != null)
        {
            equippedIndicator.SetActive(deck.isEquipped);
        }

        if (lockedOverlay != null)
        {
            lockedOverlay.SetActive(!deck.isOwned);
        }

        if (backgroundImage != null && !string.IsNullOrWhiteSpace(deck.id))
        {
            var sprite = Resources.Load<Sprite>($"DeckImages/{deck.id}");
            if (sprite != null)
            {
                backgroundImage.sprite = sprite;
            }
            else if (!string.IsNullOrWhiteSpace(deck.colorHex)
                     && ColorUtility.TryParseHtmlString(deck.colorHex, out var color))
            {
                backgroundImage.color = color;
            }
        }

        if (iconImage != null && !string.IsNullOrWhiteSpace(deck.iconName))
        {
            var sprite = ResolveSprite(iconSprites, deck.iconName);
            if (sprite != null)
            {
                iconImage.sprite = sprite;
            }
        }

        if (equipButton != null)
        {
            equipButton.onClick.RemoveAllListeners();

            if (deck.isOwned && !deck.isEquipped && profileManager != null)
            {
                equipButton.onClick.AddListener(() => profileManager.EquipDeck(deck.id));
            }

            equipButton.gameObject.SetActive(deck.isOwned && !deck.isEquipped);
        }
    }

    private static Sprite ResolveSprite(List<NamedSprite> mapping, string key)
    {
        if (mapping == null)
        {
            return null;
        }

        for (int i = 0; i < mapping.Count; i++)
        {
            if (mapping[i] != null && mapping[i].IsMatch(key))
            {
                return mapping[i].sprite;
            }
        }

        return null;
    }
}
