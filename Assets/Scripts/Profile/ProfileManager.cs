using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.Serialization;

public class ProfileManager : MonoBehaviour
{
    private static readonly string[] StoreDeckIds =
    {
        "deckAnimes",
        "deckAstronomia",
        "deckDireito",
        "deckGastronomia",
        "deckLiteratura",
        "deckMedicina",
        "deckMitologia",
        "deckTecnologia"
    };

    [SerializeField, FormerlySerializedAs("uiBinder")]
    private ProfileUIBinder profileUIBinder;

    private PlayerProfileData currentProfile;
    private HashSet<string> ownedDeckIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private string equippedDeckId = string.Empty;
    private bool inventoryLoaded;
    private bool equippedLoaded;
    private bool statsLoaded;
    private bool profileDataLoaded;
    private bool profileLoadStarted;

    private void OnEnable()
    {
        PlayFabService.OnLoginSuccess += HandlePlayFabLoginSuccess;
        InventoryService.OnInventoryLoaded += HandleInventoryLoaded;
        InventoryService.OnInventoryFailed += HandleInventoryFailed;
    }

    private void OnDisable()
    {
        PlayFabService.OnLoginSuccess -= HandlePlayFabLoginSuccess;
        InventoryService.OnInventoryLoaded -= HandleInventoryLoaded;
        InventoryService.OnInventoryFailed -= HandleInventoryFailed;
    }

    private void Awake()
    {
        if (profileUIBinder == null)
        {
            profileUIBinder = GetComponent<ProfileUIBinder>();
        }
    }

    private void Start()
    {
        EnsurePlayFabService();
        EnsureInventoryService();

        if (PlayFabService.Instance != null)
        {
            PlayFabService.Instance.Initialize();
        }

        if (PlayFabService.Instance != null && PlayFabService.Instance.IsLoggedIn())
        {
            BeginProfileLoad();
            return;
        }

        Debug.Log("[ProfileManager] Aguardando login do PlayFab para carregar os decks do perfil.");
    }

    private void HandlePlayFabLoginSuccess()
    {
        BeginProfileLoad();
    }

    private void BeginProfileLoad()
    {
        if (profileLoadStarted)
        {
            return;
        }

        if (PlayFabService.Instance == null || !PlayFabService.Instance.IsLoggedIn())
        {
            return;
        }

        profileLoadStarted = true;
        LoadProfile();
    }

    private void LoadProfile()
    {
        currentProfile = PlayerProfileData.CreateDefault();
        inventoryLoaded = false;
        equippedLoaded  = false;
        statsLoaded     = false;
        profileDataLoaded = false;
        ownedDeckIds.Clear();
        equippedDeckId = string.Empty;

        RequestInventory();
        RequestEquippedDeck();
        RequestPlayerStats();
        RequestPlayerProfile();
    }

    private void RequestInventory()
    {
        if (InventoryService.Instance == null)
        {
            EnsureInventoryService();
        }

        if (InventoryService.Instance == null)
        {
            Debug.LogWarning("[ProfileManager] InventoryService indisponivel para carregar os decks do perfil.");
            ownedDeckIds.Clear();
            inventoryLoaded = true;
            TryBuildProfile();
            return;
        }

        InventoryService.Instance.LoadInventory();
    }

    private void HandleInventoryLoaded(List<ItemInstance> items)
    {
        ownedDeckIds.Clear();

        if (items != null)
        {
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                {
                    continue;
                }

                ownedDeckIds.Add(item.ItemId);
            }
        }

        inventoryLoaded = true;
        TryBuildProfile();
    }

    private void HandleInventoryFailed(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());
        ownedDeckIds.Clear();
        inventoryLoaded = true;
        TryBuildProfile();
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

    private void RequestPlayerProfile()
    {
        PlayFabClientAPI.GetUserData(
            new GetUserDataRequest { Keys = new List<string> { "player_profile" } },
            result =>
            {
                if (result?.Data != null
                    && result.Data.TryGetValue("player_profile", out var record)
                    && !string.IsNullOrWhiteSpace(record.Value))
                {
                    var profile = JsonUtility.FromJson<PlayerProfileData>(record.Value);
                    if (profile != null)
                    {
                        if (!string.IsNullOrWhiteSpace(profile.displayName))
                        {
                            currentProfile.displayName = profile.displayName;
                        }

                        currentProfile.avatarId = profile.avatarId;
                        currentProfile.avatarUrl = profile.avatarUrl;
                    }
                }

                profileDataLoaded = true;
                TryBuildProfile();
            },
            HandlePlayFabError);
    }

    private void TryBuildProfile()
    {
        if (!inventoryLoaded || !equippedLoaded || !statsLoaded || !profileDataLoaded || currentProfile == null)
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

        for (int i = 0; i < StoreDeckIds.Length; i++)
        {
            var deckId = StoreDeckIds[i];

            if (!ownedDeckIds.Contains(deckId))
            {
                continue;
            }

            decks.Add(new PlayerDeckData
            {
                id = deckId,
                category = FormatDeckCategory(deckId),
                iconName = deckId,
                isOwned = true,
                isEquipped = string.Equals(deckId, equippedDeckId, StringComparison.OrdinalIgnoreCase)
            });
        }

        return decks;
    }

    private static string FormatDeckCategory(string deckId)
    {
        if (string.IsNullOrWhiteSpace(deckId))
        {
            return "DECK";
        }

        var label = deckId.Trim();
        if (label.StartsWith("deck", StringComparison.OrdinalIgnoreCase))
        {
            label = label.Substring(4);
        }

        label = label.Replace("_", string.Empty).Trim();
        return string.IsNullOrWhiteSpace(label) ? "DECK" : label.ToUpperInvariant();
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

    public void ApplyAvatarId(string avatarId)
    {
        if (currentProfile == null || string.IsNullOrWhiteSpace(avatarId))
        {
            return;
        }

        currentProfile.avatarId = avatarId;
        profileUIBinder?.Bind(currentProfile);

        if (PlayerDataService.Instance != null)
        {
            PlayerDataService.Instance.LoadPlayerData();
        }
    }

    public string GetEquippedAvatarId()
    {
        return currentProfile != null ? currentProfile.avatarId : string.Empty;
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

    private static void EnsurePlayFabService()
    {
        if (PlayFabService.Instance != null)
        {
            return;
        }

        var playFabServiceGO = new GameObject("PlayFabService");
        playFabServiceGO.AddComponent<PlayFabService>();
    }

    private static void EnsureInventoryService()
    {
        if (InventoryService.Instance != null)
        {
            return;
        }

        var inventoryServiceGO = new GameObject("InventoryService");
        inventoryServiceGO.AddComponent<InventoryService>();
    }
}
