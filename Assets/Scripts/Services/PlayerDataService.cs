using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using BrainDuel.Match;

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
        cachedProfile ??= PlayerProfileData.CreateDefault();
        cachedProfile.equippedPowerUp = type.ToString();

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
    /// Deve ser chamado uma única vez, logo após o registro bem-sucedido.
    /// </summary>
    public void InitializeForNewPlayer(string displayName)
    {
        cachedProfile = PlayerProfileData.CreateDefault();
        if (!string.IsNullOrWhiteSpace(displayName))
            cachedProfile.displayName = displayName;

        cachedProfile.equippedDeckId = "deckHistoria";
        cachedProfile.decks = new List<PlayerDeckData>
        {
            new PlayerDeckData { id = "deckHistoria",  category = "HISTÓRIA",  isOwned = true, isEquipped = true,  colorHex = "#8B2020", iconName = "history" },
            new PlayerDeckData { id = "deckGeografia", category = "GEOGRAFIA", isOwned = true, isEquipped = false, colorHex = "#1E5D9A", iconName = "geography" },
            new PlayerDeckData { id = "deckCiencia",   category = "CIÊNCIA",   isOwned = true, isEquipped = false, colorHex = "#2E8B57", iconName = "science" },
        };

        string json = JsonUtility.ToJson(cachedProfile);

        // Salva player_profile (JSON completo) e equippedDeckId (chave direta lida pelo ProfileManager)
        PlayFabService.Client.UpdateUserData(
            new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string>
                {
                    { PlayerProfileKey, json },
                    { "equippedDeckId",  "deckHistoria" }
                }
            },
            _ => Debug.Log("[PlayerDataService] Perfil inicial salvo com decks iniciais."),
            e => Debug.LogError($"[PlayerDataService] Falha ao salvar perfil inicial: {e.GenerateErrorReport()}")
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