using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class ProfileManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ProfileUIBinder uiBinder;

    [Header("PlayFab Requests")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool requestPlayFabProfile = true;
    [SerializeField] private bool requestStatistics = true;
    [SerializeField] private bool requestCurrency = true;
    [SerializeField] private string currencyCode = "BC";
    [SerializeField] private bool buildDecksFromIndex = true;

    [Header("Deck Visual Presets")]
    [SerializeField] private List<DeckVisualPreset> deckPresets = new List<DeckVisualPreset>();

    private PlayerProfileData runtimeData;

    private void Awake()
    {
        if (uiBinder == null)
        {
            uiBinder = GetComponent<ProfileUIBinder>();
        }
    }

    private void OnEnable()
    {
        PlayerDataService.OnPlayerDataLoaded += HandlePlayerDataLoaded;
        EconomyService.OnCurrencyChanged += HandleCurrencyChanged;
        DeckService.OnDecksLoaded += HandleDecksLoaded;
        DeckService.OnDecksLoadFailed += HandleDecksFailed;
    }

    private void OnDisable()
    {
        PlayerDataService.OnPlayerDataLoaded -= HandlePlayerDataLoaded;
        EconomyService.OnCurrencyChanged -= HandleCurrencyChanged;
        DeckService.OnDecksLoaded -= HandleDecksLoaded;
        DeckService.OnDecksLoadFailed -= HandleDecksFailed;
    }

    private void Start()
    {
        if (loadOnStart)
        {
            LoadProfile();
        }
    }

    public void LoadProfile()
    {
        runtimeData = PlayerProfileData.CreateDefault();

        var cachedProfile = PlayerDataService.Instance != null ? PlayerDataService.Instance.CurrentProfile : null;
        if (cachedProfile != null)
        {
            MergeProfile(runtimeData, cachedProfile);
        }

        uiBinder?.Bind(runtimeData);

        PlayerDataService.Instance?.LoadPlayerData();

        if (requestPlayFabProfile)
        {
            RequestPlayFabProfile();
        }

        if (requestStatistics)
        {
            RequestStatistics();
        }

        if (requestCurrency)
        {
            EconomyService.Instance?.GetBalance(currencyCode);
        }

        if (buildDecksFromIndex)
        {
            DeckService.Instance?.Initialize();
            TryBuildDecksFromIndex();
        }
    }

    private void HandlePlayerDataLoaded(PlayerProfileData profile)
    {
        MergeProfile(runtimeData, profile);
        uiBinder?.Bind(runtimeData);
    }

    private void HandleCurrencyChanged(string code, int balance)
    {
        if (!string.Equals(code, currencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        runtimeData.brainCoins = balance;
        uiBinder?.Bind(runtimeData);
    }

    private void HandleDecksLoaded()
    {
        if (!buildDecksFromIndex)
        {
            return;
        }

        TryBuildDecksFromIndex();
    }

    private void HandleDecksFailed(PlayFabError error)
    {
        Debug.LogWarning("[ProfileManager] Falha ao carregar decks para a tela de perfil.");
    }

    private void TryBuildDecksFromIndex()
    {
        if (DeckService.Instance == null)
        {
            return;
        }

        var index = DeckService.Instance.GetDeckIndex();
        if (index == null || index.categorias == null)
        {
            return;
        }

        var decks = new List<PlayerDeckData>();
        foreach (var categoria in index.categorias)
        {
            if (categoria == null || !categoria.ativo)
            {
                continue;
            }

            var preset = FindPreset(categoria.nome);

            decks.Add(new PlayerDeckData
            {
                id = categoria.nome,
                category = categoria.nome,
                colorHex = preset != null ? preset.colorHex : string.Empty,
                iconName = preset != null ? preset.iconName : string.Empty,
                isEquipped = !string.IsNullOrWhiteSpace(runtimeData.equippedDeckId)
                    && string.Equals(runtimeData.equippedDeckId, categoria.nome, StringComparison.OrdinalIgnoreCase)
            });
        }

        runtimeData.decks = decks;
        uiBinder?.Bind(runtimeData);
    }

    private DeckVisualPreset FindPreset(string category)
    {
        if (deckPresets == null)
        {
            return null;
        }

        for (int i = 0; i < deckPresets.Count; i++)
        {
            if (deckPresets[i] != null && deckPresets[i].IsMatch(category))
            {
                return deckPresets[i];
            }
        }

        return null;
    }

    private void RequestPlayFabProfile()
    {
        if (PlayFabService.Instance == null || !PlayFabService.Instance.IsLoggedIn())
        {
            return;
        }

        var request = new GetPlayerProfileRequest
        {
            ProfileConstraints = new PlayerProfileViewConstraints
            {
                ShowDisplayName = true,
                ShowAvatarUrl = true
            }
        };

        PlayFabClientAPI.GetPlayerProfile(request, HandlePlayFabProfileLoaded, HandlePlayFabError);
    }

    private void HandlePlayFabProfileLoaded(GetPlayerProfileResult result)
    {
        if (result == null || result.PlayerProfile == null)
        {
            return;
        }

        runtimeData.displayName = result.PlayerProfile.DisplayName;
        runtimeData.avatarUrl = result.PlayerProfile.AvatarUrl;
        uiBinder?.Bind(runtimeData);
    }

    private void RequestStatistics()
    {
        if (PlayFabService.Instance == null || !PlayFabService.Instance.IsLoggedIn())
        {
            return;
        }

        PlayFabClientAPI.GetPlayerStatistics(new GetPlayerStatisticsRequest(), HandleStatisticsLoaded, HandlePlayFabError);
    }

    private void HandleStatisticsLoaded(GetPlayerStatisticsResult result)
    {
        if (result == null || result.Statistics == null)
        {
            return;
        }

        foreach (var stat in result.Statistics)
        {
            if (stat == null)
            {
                continue;
            }

            if (string.Equals(stat.StatisticName, "wins", StringComparison.OrdinalIgnoreCase))
            {
                runtimeData.wins = stat.Value;
            }
            else if (string.Equals(stat.StatisticName, "losses", StringComparison.OrdinalIgnoreCase))
            {
                runtimeData.losses = stat.Value;
            }
        }

        int totalMatches = runtimeData.wins + runtimeData.losses;
        if (totalMatches > 0)
        {
            runtimeData.accuracy = Mathf.Clamp01(runtimeData.wins / (float)totalMatches) * 100f;
        }

        uiBinder?.Bind(runtimeData);
    }

    private void HandlePlayFabError(PlayFabError error)
    {
        Debug.LogWarning($"[ProfileManager] PlayFab error: {error.GenerateErrorReport()}");
    }

    private void MergeProfile(PlayerProfileData target, PlayerProfileData source)
    {
        if (target == null || source == null)
        {
            return;
        }

        target.displayName = string.IsNullOrWhiteSpace(source.displayName) ? target.displayName : source.displayName;
        target.avatarId = string.IsNullOrWhiteSpace(source.avatarId) ? target.avatarId : source.avatarId;
        target.avatarUrl = string.IsNullOrWhiteSpace(source.avatarUrl) ? target.avatarUrl : source.avatarUrl;
        target.level = source.level;
        target.currentXp = source.currentXp;
        target.xpToNextLevel = source.xpToNextLevel > 0 ? source.xpToNextLevel : target.xpToNextLevel;
        target.wins = source.wins;
        target.losses = source.losses;
        target.accuracy = source.accuracy;
        target.title = string.IsNullOrWhiteSpace(source.title) ? target.title : source.title;
        target.brainCoins = source.brainCoins;
        target.equippedDeckId = string.IsNullOrWhiteSpace(source.equippedDeckId) ? target.equippedDeckId : source.equippedDeckId;
        target.decks = source.decks ?? target.decks;
        target.settings = source.settings ?? target.settings;
    }

#if UNITY_EDITOR
    [ContextMenu("Load Test Data")]
    private void LoadTestData()
    {
        runtimeData = PlayerProfileData.CreatePlaceholder();
        uiBinder?.Bind(runtimeData);
    }
#endif
}

[System.Serializable]
public class DeckVisualPreset
{
    public string category;
    public string colorHex;
    public string iconName;

    public bool IsMatch(string candidate)
    {
        return !string.IsNullOrWhiteSpace(category)
               && !string.IsNullOrWhiteSpace(candidate)
               && string.Equals(category.Trim(), candidate.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
