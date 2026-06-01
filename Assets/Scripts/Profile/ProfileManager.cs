using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.Serialization;

public class ProfileManager : MonoBehaviour
{
    private static readonly string[] AllDeckIds =
    {
        "deckHistoria",
        "deckGeografia",
        "deckCiencia"
    };

    private static readonly Dictionary<string, string> DeckCategories = new Dictionary<string, string>
    {
        { "deckHistoria",  "HISTÓRIA" },
        { "deckGeografia", "GEOGRAFIA" },
        { "deckCiencia",   "CIÊNCIA" }
    };

    [SerializeField, FormerlySerializedAs("uiBinder")]
    private ProfileUIBinder profileUIBinder;

    private PlayerProfileData currentProfile;
    private HashSet<string> ownedDeckIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private string equippedDeckId = string.Empty;
    private bool inventoryLoaded;
    private bool equippedLoaded;
    private bool profileDecksLoaded;
    private bool statsLoaded;
    private bool accountLoaded;

    private void Awake()
    {
        if (profileUIBinder == null)
        {
            profileUIBinder = GetComponent<ProfileUIBinder>();
        }
    }

    private void Start()
    {
        LoadProfile();
    }

    private void LoadProfile()
    {
        currentProfile = PlayerProfileData.CreateDefault();
        inventoryLoaded    = false;
        equippedLoaded     = false;
        profileDecksLoaded = false;
        statsLoaded        = false;
        accountLoaded      = false;
        ownedDeckIds.Clear();
        equippedDeckId = string.Empty;

        RequestInventory();
        RequestEquippedDeck();
        RequestProfileDecks();
        RequestPlayerStats();
        RequestAccountInfo();
    }

    private void RequestInventory()
    {
        PlayFabClientAPI.GetUserInventory(
            new GetUserInventoryRequest(),
            result =>
            {
                if (result?.Inventory != null)
                {
                    foreach (var item in result.Inventory)
                    {
                        if (item != null && !string.IsNullOrWhiteSpace(item.ItemId)
                            && item.ItemId.StartsWith("deck", StringComparison.OrdinalIgnoreCase))
                        {
                            ownedDeckIds.Add(item.ItemId);
                        }
                    }
                }

                inventoryLoaded = true;
                TryBuildProfile();
            },
            HandlePlayFabError);
    }

    private void RequestEquippedDeck()
    {
        PlayFabClientAPI.GetUserData(
            new GetUserDataRequest { Keys = new List<string> { "equippedDeckId" } },
            result =>
            {
                equippedDeckId = string.Empty;

                if (result != null && result.Data != null
                    && result.Data.TryGetValue("equippedDeckId", out var entry))
                {
                    equippedDeckId = entry.Value;
                }

                equippedLoaded = true;
                TryBuildProfile();
            },
            HandlePlayFabError);
    }

    private void RequestProfileDecks()
    {
        PlayFabClientAPI.GetUserData(
            new GetUserDataRequest { Keys = new List<string> { "player_profile" } },
            result =>
            {
                if (result?.Data != null && result.Data.TryGetValue("player_profile", out var entry)
                    && !string.IsNullOrWhiteSpace(entry.Value))
                {
                    try
                    {
                        var profile = JsonUtility.FromJson<PlayerProfileData>(entry.Value);
                        if (profile?.decks != null)
                        {
                            foreach (var deck in profile.decks)
                            {
                                if (deck != null && !string.IsNullOrWhiteSpace(deck.id) && deck.isOwned)
                                    ownedDeckIds.Add(deck.id);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ProfileManager] Erro ao ler player_profile: {ex.Message}");
                    }
                }

                profileDecksLoaded = true;
                TryBuildProfile();
            },
            _ =>
            {
                profileDecksLoaded = true;
                TryBuildProfile();
            });
    }

    private void RequestPlayerStats()
    {
        PlayFabClientAPI.GetPlayerStatistics(
            new GetPlayerStatisticsRequest
            {
                StatisticNames = new List<string> { "wins", "losses" }
            },
            result =>
            {
                if (result?.Statistics != null)
                {
                    foreach (var stat in result.Statistics)
                    {
                        if (stat.StatisticName == "wins")   currentProfile.wins   = stat.Value;
                        if (stat.StatisticName == "losses") currentProfile.losses = stat.Value;
                    }
                }

                statsLoaded = true;
                TryBuildProfile();
            },
            HandlePlayFabError);
    }

    private void RequestAccountInfo()
    {
        PlayFabClientAPI.GetAccountInfo(
            new GetAccountInfoRequest(),
            result =>
            {
                if (result?.AccountInfo?.TitleInfo != null)
                {
                    var name = result.AccountInfo.TitleInfo.DisplayName;
                    if (!string.IsNullOrWhiteSpace(name))
                        currentProfile.displayName = name;
                }

                accountLoaded = true;
                TryBuildProfile();
            },
            HandlePlayFabError);
    }

    private void TryBuildProfile()
    {
        if (!inventoryLoaded || !equippedLoaded || !profileDecksLoaded || !statsLoaded || !accountLoaded || currentProfile == null)
        {
            return;
        }

        currentProfile.equippedDeckId = equippedDeckId;
        currentProfile.decks = BuildDecks();
        profileUIBinder?.Bind(currentProfile);
    }

    private List<PlayerDeckData> BuildDecks()
    {
        var decks = new List<PlayerDeckData>();

        for (int i = 0; i < AllDeckIds.Length; i++)
        {
            var deckId = AllDeckIds[i];
            var category = DeckCategories.TryGetValue(deckId, out var name) ? name : deckId;
            bool isOwned = ownedDeckIds.Contains(deckId);

            decks.Add(new PlayerDeckData
            {
                id = deckId,
                category = category,
                iconName = string.Empty,
                isOwned = isOwned,
                isEquipped = isOwned && string.Equals(deckId, equippedDeckId, StringComparison.OrdinalIgnoreCase)
            });
        }

        return decks;
    }

    public void EquipDeck(string deckId)
    {
        if (string.IsNullOrWhiteSpace(deckId) || currentProfile == null)
        {
            return;
        }

        if (!ownedDeckIds.Contains(deckId))
        {
            Debug.LogWarning("[ProfileManager] Tentou equipar um deck nao possuido.");
            return;
        }

        PlayFabClientAPI.UpdateUserData(
            new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string> { { "equippedDeckId", deckId } }
            },
            result =>
            {
                equippedDeckId = deckId;
                currentProfile.equippedDeckId = deckId;
                UpdateEquippedFlags(deckId);
                profileUIBinder?.Bind(currentProfile);
            },
            HandlePlayFabError);
    }

    private void UpdateEquippedFlags(string deckId)
    {
        if (currentProfile == null || currentProfile.decks == null)
        {
            return;
        }

        for (int i = 0; i < currentProfile.decks.Count; i++)
        {
            var deck = currentProfile.decks[i];
            if (deck == null)
            {
                continue;
            }

            deck.isEquipped = string.Equals(deck.id, deckId, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void HandlePlayFabError(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());
    }
}
