using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDataTester : PlayFabTerminalTester
{
    private const string Title = "PlayerDataTester";

    protected override void Start()
    {
        base.Start();
        EnsureService<PlayerDataService>();
        PlayerDataService.OnPlayerDataLoaded += HandleLoaded;
        PlayerDataService.OnPlayerDataSaved += HandleSaved;
        PlayerDataService.OnPlayerDataFailed += HandleError;
        PrintReadyMessage(Title, "1=carregar dados, 2=salvar profile exemplo, 3=resetar dados de teste, 4=adicionar 100 XP");
    }

    private void OnDestroy()
    {
        PlayerDataService.OnPlayerDataLoaded -= HandleLoaded;
        PlayerDataService.OnPlayerDataSaved -= HandleSaved;
        PlayerDataService.OnPlayerDataFailed -= HandleError;
    }

    private void Update()
    {
        if (!HasKeyboard())
        {
            return;
        }

        var keyboard = Keyboard.current;

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            PlayerDataService.Instance.LoadPlayerData();
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            PlayerDataService.Instance.SaveProgress(3, 1200, new PlayerBasicSettings
            {
                soundEnabled = true,
                masterVolume = 0.8f,
                language = "pt-BR"
            });
            Debug.Log($"[{Title}] Solicitado salvamento de profile exemplo.");
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            PlayerDataService.Instance.ResetForTests();
            Debug.Log($"[{Title}] Solicitado reset de dados de teste.");
        }
        else if (keyboard.digit4Key.wasPressedThisFrame)
        {
            var profile = PlayerDataService.Instance.CurrentProfile ?? PlayerProfileData.CreateDefault();
            PlayerDataService.Instance.SaveProgress(profile.level, profile.currentXp + 100, profile.settings);
            Debug.Log($"[{Title}] Solicitado incremento de 100 XP.");
        }
    }

    private void HandleLoaded(PlayerProfileData profile)
    {
        Debug.Log($"[{Title}] ✅ Profile carregado: Level={profile.level}, XP={profile.currentXp}, Lang={profile.settings.language}");
    }

    private void HandleSaved(PlayerProfileData profile)
    {
        Debug.Log($"[{Title}] ✅ Profile salvo: Level={profile.level}, XP={profile.currentXp}");
    }

    private void HandleError(PlayFab.PlayFabError error)
    {
        Debug.LogError($"[{Title}] ❌ Erro PlayFab: {error.GenerateErrorReport()}");
    }
}