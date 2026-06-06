using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using BrainDuel.Match;

// Metadados visuais para itens de deck — mapeados pelo ItemId do catálogo PlayFab.
// Suporta tanto snake_case (deck_historia) quanto camelCase (deckHistoria).
internal static class DeckItemMeta
{
    private static readonly Dictionary<string, Entry> Map =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["deck_historia"]    = new("HISTÓRIA",    "#8B2020", "history"),
        ["deckHistoria"]     = new("HISTÓRIA",    "#8B2020", "history"),
        ["deck_geografia"]   = new("GEOGRAFIA",   "#1E5D9A", "geography"),
        ["deckGeografia"]    = new("GEOGRAFIA",   "#1E5D9A", "geography"),
        ["deck_ciencias"]    = new("CIÊNCIA",     "#2E8B57", "science"),
        ["deckCiencias"]     = new("CIÊNCIA",     "#2E8B57", "science"),
        ["deckCiencia"]      = new("CIÊNCIA",     "#2E8B57", "science"),
        ["deck_cultura_pop"] = new("CULTURA POP", "#8B5A2B", "culture"),
        ["deckCulturaPop"]   = new("CULTURA POP", "#8B5A2B", "culture"),
        ["deck_tecnologia"]  = new("TECNOLOGIA",  "#6B4C9A", "technology"),
        ["deckTecnologia"]   = new("TECNOLOGIA",  "#6B4C9A", "technology"),
        ["deck_literatura"]  = new("LITERATURA",  "#C47A1E", "literature"),
        ["deckLiteratura"]   = new("LITERATURA",  "#C47A1E", "literature"),
    };

    internal readonly struct Entry
    {
        internal readonly string Category;
        internal readonly string ColorHex;
        internal readonly string IconName;
        internal Entry(string cat, string color, string icon) { Category = cat; ColorHex = color; IconName = icon; }
    }

    internal static bool TryGet(string itemId, out Entry entry) => Map.TryGetValue(itemId, out entry);

    // Fallback: "deck_cultura_pop" → "CULTURA POP", "deckHistoria" → "HISTORIA"
    internal static string CategoryFallback(string itemId)
    {
        var suffix = itemId.StartsWith("deck_", StringComparison.OrdinalIgnoreCase) ? itemId[5..]
                   : itemId.StartsWith("deck",  StringComparison.OrdinalIgnoreCase) ? itemId[4..]
                   : itemId;
        return suffix.Replace("_", " ").ToUpper();
    }
}

public class PlayerDataService : MonoBehaviour
{
    private const string PlayerProfileKey = "player_profile";

    public static PlayerDataService Instance { get; private set; }

    public static event Action<PlayerProfileData> OnPlayerDataLoaded;
    public static event Action<PlayerProfileData> OnPlayerDataSaved;
    public static event Action<PlayFabError> OnPlayerDataFailed;

    private PlayerProfileData cachedProfile;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (PlayFabService.Instance != null && PlayFabService.Instance.IsLoggedIn())
        {
            LoadPlayerData();
        }
        else
        {
            PlayFabService.OnLoginSuccess += LoadPlayerData;
        }
    }

    private void OnDestroy()
    {
        PlayFabService.OnLoginSuccess -= LoadPlayerData;
    }

    public PlayerProfileData CurrentProfile => cachedProfile;

    public void LoadPlayerData()
    {
        if (PlayFabService.Instance == null || !PlayFabService.Instance.IsLoggedIn())
        {
            Debug.LogWarning("[PlayerDataService] Login PlayFab ainda nao foi concluido.");
            return;
        }

        PlayFabService.Client.GetUserData(
            new GetUserDataRequest { Keys = new List<string> { PlayerProfileKey } },
            OnGetUserDataSuccess,
            OnError
        );

        Debug.Log("[PlayerDataService] Carregando dados do jogador...");
    }

    public void SaveProgress(int level, int currentXp, PlayerBasicSettings settings = null)
    {
        cachedProfile ??= PlayerProfileData.CreateDefault();
        cachedProfile.level = level;
        cachedProfile.currentXp = currentXp;
        cachedProfile.settings = settings ?? cachedProfile.settings ?? new PlayerBasicSettings();

        string json = JsonUtility.ToJson(cachedProfile);
        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                { PlayerProfileKey, json }
            }
        };

        PlayFabService.Client.UpdateUserData(request, OnSaveSuccess, OnError);
    }

    public void ResetForTests()
    {
        cachedProfile = PlayerProfileData.CreateDefault();
        SaveProgress(cachedProfile.level, cachedProfile.currentXp, cachedProfile.settings);
    }

    /// <summary>
    /// Persiste o power-up equipado pelo jogador no PlayFab User Data.
    /// </summary>
    public void EquipPowerUp(PowerUpType type)
    {
        cachedProfile = PlayerProfileData.CreateDefault();
        if (!string.IsNullOrWhiteSpace(displayName))
            cachedProfile.displayName = displayName;
        cachedProfile.avatarId = "skinDefault";
        cachedProfile.equippedDeckId = "deckHistoria";

        string json = JsonUtility.ToJson(cachedProfile);
        PlayFabService.Client.UpdateUserData(
            new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string> { { PlayerProfileKey, json } }
            },
            _ => Debug.Log($"[PlayerDataService] Power-up equipado salvo: {type}"),
            e => Debug.LogError($"[PlayerDataService] Falha ao salvar power-up: {e.GenerateErrorReport()}")
        );
    }

    /// <summary>
    /// Cria e persiste o perfil inicial de um novo jogador no PlayFab.
    /// Deve ser chamado uma única vez, logo após o registro + grant de decks iniciais.
    /// deckItemIds: ItemIds do catálogo retornados por GrantStarterDecks (fonte da verdade).
    /// </summary>
    public void InitializeForNewPlayer(string displayName, IEnumerable<string> deckItemIds = null)
    {
        cachedProfile = PlayerProfileData.CreateDefault();
        if (!string.IsNullOrWhiteSpace(displayName))
            cachedProfile.displayName = displayName;

        // Constrói a lista de decks usando os IDs reais do catálogo PlayFab
        cachedProfile.decks = BuildDeckList(deckItemIds);
        cachedProfile.equippedDeckId = cachedProfile.decks.Count > 0 ? cachedProfile.decks[0].id : string.Empty;

        PersistProfile(cachedProfile, "[PlayerDataService] Perfil inicial salvo.");
    }

    /// <summary>
    /// Adiciona um deck ao perfil após uma compra na loja.
    /// Usa o ItemId do catálogo diretamente para manter consistência.
    /// </summary>
    public void AddDeckToProfile(string catalogItemId)
    {
        if (cachedProfile == null || string.IsNullOrWhiteSpace(catalogItemId)) return;

        cachedProfile.decks ??= new List<PlayerDeckData>();
        if (cachedProfile.decks.Exists(d => string.Equals(d.id, catalogItemId, StringComparison.OrdinalIgnoreCase)))
            return;

        cachedProfile.decks.Add(MakeDeckEntry(catalogItemId, isEquipped: false));
        PersistProfile(cachedProfile, $"[PlayerDataService] Deck '{catalogItemId}' adicionado ao perfil.");
    }

    // --- Helpers de deck ---

    private static List<PlayerDeckData> BuildDeckList(IEnumerable<string> itemIds)
    {
        var list = new List<PlayerDeckData>();
        if (itemIds == null) return list;

        foreach (var id in itemIds)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!id.StartsWith("deck", StringComparison.OrdinalIgnoreCase)) continue;
            list.Add(MakeDeckEntry(id, isEquipped: list.Count == 0));
        }
        return list;
    }

    private static PlayerDeckData MakeDeckEntry(string itemId, bool isEquipped)
    {
        DeckItemMeta.TryGet(itemId, out var meta);
        return new PlayerDeckData
        {
            id         = itemId,
            category   = string.IsNullOrEmpty(meta.Category) ? DeckItemMeta.CategoryFallback(itemId) : meta.Category,
            colorHex   = string.IsNullOrEmpty(meta.ColorHex) ? "#666666" : meta.ColorHex,
            iconName   = string.IsNullOrEmpty(meta.IconName) ? "default"  : meta.IconName,
            isOwned    = true,
            isEquipped = isEquipped,
        };
    }

    private void PersistProfile(PlayerProfileData profile, string successMsg)
    {
        var json = JsonUtility.ToJson(profile);
        PlayFabService.Client.UpdateUserData(
            new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string>
                {
                    { PlayerProfileKey,  json },
                    { "equippedDeckId", profile.equippedDeckId ?? string.Empty }
                }
            },
            _ => Debug.Log(successMsg),
            e => Debug.LogError($"[PlayerDataService] Falha ao persistir perfil: {e.GenerateErrorReport()}")
        );
    }

    private void OnGetUserDataSuccess(GetUserDataResult result)
    {
        if (result.Data != null && result.Data.TryGetValue(PlayerProfileKey, out UserDataRecord record) && !string.IsNullOrWhiteSpace(record.Value))
        {
            cachedProfile = JsonUtility.FromJson<PlayerProfileData>(record.Value);
        }

        cachedProfile ??= PlayerProfileData.CreateDefault();

        Debug.Log($"[PlayerDataService] ✅ Dados carregados. Level={cachedProfile.level}, XP={cachedProfile.currentXp}");
        OnPlayerDataLoaded?.Invoke(cachedProfile);
    }

    private void OnSaveSuccess(UpdateUserDataResult result)
    {
        Debug.Log("[PlayerDataService] ✅ Dados do jogador salvos com sucesso.");
        OnPlayerDataSaved?.Invoke(cachedProfile);
    }

    private void OnError(PlayFabError error)
    {
        Debug.LogError($"[PlayerDataService] ❌ Erro PlayFab: {error.GenerateErrorReport()}");
        OnPlayerDataFailed?.Invoke(error);
    }
}
