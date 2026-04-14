using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.InputSystem;

public class AuthTester : PlayFabTerminalTester
{
    private const string Title = "AuthTester";

    protected override void Start()
    {
        base.Start();
        PrintReadyMessage(Title, "1=login persistente, 2=resetar ID e relogar, 3=login invalido, 4=mostrar CustomId atual");
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
            LoginWithPersistentCustomId();
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            PlayFabConfig.ResetTestUserId();
            Debug.Log($"[{Title}] ID local resetado.");
            LoginWithPersistentCustomId();
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            LoginWithInvalidCustomId();
        }
        else if (keyboard.digit4Key.wasPressedThisFrame)
        {
            string currentId = PlayFabService.Instance != null ? PlayFabService.Instance.CurrentCustomId : "<sem instancia>";
            Debug.Log($"[{Title}] CustomId atual: {currentId}");
        }
    }

    private void LoginWithPersistentCustomId()
    {
        string customId = PlayFabConfig.GetTestUserId();

        PlayFabService.Client.LoginWithCustomID(new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = true
        },
        result => Debug.Log($"[{Title}] ✅ Login persistente concluido para {customId}. PlayFabId={result.PlayFabId}"),
        error => Debug.LogError($"[{Title}] ❌ Falha no login persistente: {error.GenerateErrorReport()}")
        );

        Debug.Log($"[{Title}] Solicitado login persistente com CustomId={customId}");
    }

    private void LoginWithInvalidCustomId()
    {
        string customId = $"invalid_{System.Guid.NewGuid():N}";

        PlayFabService.Client.LoginWithCustomID(new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = false
        },
        result => Debug.LogWarning($"[{Title}] Login nao deveria ter criado conta. PlayFabId={result.PlayFabId}"),
        error => Debug.Log($"[{Title}] ✅ Erro esperado retornado para credencial invalida: {error.GenerateErrorReport()}")
        );

        Debug.Log($"[{Title}] Solicitado login invalido com CustomId={customId}");
    }
}