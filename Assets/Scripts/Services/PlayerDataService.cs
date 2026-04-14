using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

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