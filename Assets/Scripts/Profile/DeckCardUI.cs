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

    [Header("Icon Sprites")]
    [SerializeField] private List<NamedSprite> iconSprites = new List<NamedSprite>();

    public void Setup(PlayerDeckData deck)
    {
        if (deck == null)
        {
            return;
        }

        if (deckNameText != null)
        {
            deckNameText.text = string.IsNullOrWhiteSpace(deck.category) ? "DECK" : deck.category;
        }

        if (equippedIndicator != null)
        {
            equippedIndicator.SetActive(deck.isEquipped);
        }

        if (backgroundImage != null && !string.IsNullOrWhiteSpace(deck.colorHex))
        {
            if (ColorUtility.TryParseHtmlString(deck.colorHex, out var color))
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
