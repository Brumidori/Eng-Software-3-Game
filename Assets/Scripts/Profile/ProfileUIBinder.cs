using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProfileUIBinder : MonoBehaviour
{
    [Header("Player Info")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text xpText;
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TMP_Text winsText;
    [SerializeField] private TMP_Text lossesText;
    [SerializeField] private TMP_Text accuracyText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text brainCoinsText;
    [SerializeField] private Image avatarImage;

    [Header("Avatar Sprites")]
    [SerializeField] private List<NamedSprite> avatarSprites = new List<NamedSprite>();

    [Header("Decks")]
    [SerializeField] private bool populateDecks = true;
    [SerializeField] private bool clearExistingDecks = true;
    [SerializeField] private Transform decksContainer;
    [SerializeField] private DeckCardUI deckCardPrefab;

    public void Bind(PlayerProfileData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[ProfileUIBinder] Dados do perfil nulos.");
            return;
        }

        if (playerNameText != null)
        {
            playerNameText.text = string.IsNullOrWhiteSpace(data.displayName) ? "JOGADOR" : data.displayName;
        }

        if (levelText != null)
        {
            levelText.text = data.level.ToString();
        }

        if (xpText != null)
        {
            int targetXp = Mathf.Max(1, data.xpToNextLevel);
            xpText.text = $"{data.currentXp:N0}/{targetXp:N0} XP";
        }

        if (xpSlider != null)
        {
            float targetXp = Mathf.Max(1f, data.xpToNextLevel);
            xpSlider.value = Mathf.Clamp01(data.currentXp / targetXp);
        }

        if (winsText != null)
        {
            winsText.text = data.wins.ToString();
        }

        if (lossesText != null)
        {
            lossesText.text = data.losses.ToString();
        }

        if (accuracyText != null)
        {
            accuracyText.text = $"{data.accuracy:0}%";
        }

        if (titleText != null)
        {
            titleText.text = string.IsNullOrWhiteSpace(data.title) ? "NOVATO" : data.title;
        }

        if (brainCoinsText != null)
        {
            brainCoinsText.text = $"{data.brainCoins:N0} BC";
        }

        BindAvatar(data);

        if (populateDecks)
        {
            BindDecks(data.decks);
        }
    }

    private void BindAvatar(PlayerProfileData data)
    {
        if (avatarImage == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(data.avatarId))
        {
            var sprite = ResolveSprite(avatarSprites, data.avatarId);
            if (sprite != null)
            {
                avatarImage.sprite = sprite;
            }
        }
    }

    private void BindDecks(List<PlayerDeckData> decks)
    {
        if (decksContainer == null || deckCardPrefab == null)
        {
            return;
        }

        if (clearExistingDecks)
        {
            for (int i = decksContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(decksContainer.GetChild(i).gameObject);
            }
        }

        if (decks == null)
        {
            return;
        }

        foreach (var deck in decks)
        {
            var card = Instantiate(deckCardPrefab, decksContainer);
            card.Setup(deck);
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

[System.Serializable]
public class NamedSprite
{
    public string key;
    public Sprite sprite;

    public bool IsMatch(string candidate)
    {
        return !string.IsNullOrWhiteSpace(key)
               && !string.IsNullOrWhiteSpace(candidate)
               && string.Equals(key.Trim(), candidate.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
}
