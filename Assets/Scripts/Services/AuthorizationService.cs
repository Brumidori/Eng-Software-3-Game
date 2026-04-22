using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

/// <summary>
/// Resolve e valida o role do jogador apos autenticacao.
/// A fonte de verdade e o retorno do CloudScript no servidor.
/// </summary>
public class AuthorizationService : MonoBehaviour
{
    private const string DefaultRoleFunctionName = "ValidatePlayerRole";

    public static AuthorizationService Instance { get; private set; }

    public static event Action<UserRole> OnRoleValidated;
    public static event Action<string> OnRoleValidationFailed;

    public UserRole CurrentRole { get; private set; } = UserRole.User;
    public bool HasValidatedRole { get; private set; }

    [SerializeField] private string roleValidationFunctionName = DefaultRoleFunctionName;

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

    public void ResetRoleState()
    {
        HasValidatedRole = false;
        CurrentRole = UserRole.User;
    }

    public void ValidatePlayerRole()
    {
        if (PlayFabService.Instance == null || !PlayFabService.Instance.IsLoggedIn())
        {
            NotifyRoleValidationFailed("Sessao PlayFab nao autenticada para validar perfil.");
            return;
        }

        if (string.IsNullOrWhiteSpace(roleValidationFunctionName))
        {
            NotifyRoleValidationFailed("Funcao de validacao de perfil nao configurada.");
            return;
        }

        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = roleValidationFunctionName,
            FunctionParameter = new Dictionary<string, object>(),
            GeneratePlayStreamEvent = false
        };

        PlayFabService.Client.ExecuteCloudScript(request, HandleRoleValidationSuccess, HandleRoleValidationError);
    }

    private void HandleRoleValidationSuccess(ExecuteCloudScriptResult result)
    {
        if (result.Error != null)
        {
            NotifyRoleValidationFailed($"Erro no CloudScript: {result.Error.Message}");
            return;
        }

        var roleValue = TryExtractRoleValue(result.FunctionResult);
        CurrentRole = UserRoleParser.ParseOrDefault(roleValue);
        HasValidatedRole = true;

        Debug.Log($"[AuthorizationService] Role validado: {CurrentRole}");
        OnRoleValidated?.Invoke(CurrentRole);
    }

    private void HandleRoleValidationError(PlayFabError error)
    {
        var reason = error == null
            ? "Falha ao validar perfil de acesso."
            : $"Falha ao validar perfil: {error.GenerateErrorReport()}";

        NotifyRoleValidationFailed(reason);
    }

    private void NotifyRoleValidationFailed(string reason)
    {
        Debug.LogError($"[AuthorizationService] {reason}");
        OnRoleValidationFailed?.Invoke(reason);
    }

    private static string TryExtractRoleValue(object functionResult)
    {
        if (functionResult == null)
        {
            return string.Empty;
        }

        if (functionResult is string value)
        {
            return value;
        }

        if (functionResult is IDictionary<string, object> dict)
        {
            if (TryReadRoleFromDictionary(dict, out var role))
            {
                return role;
            }
        }

        return string.Empty;
    }

    private static bool TryReadRoleFromDictionary(IDictionary<string, object> dict, out string role)
    {
        role = string.Empty;

        if (TryGetString(dict, "role", out role) || TryGetString(dict, "Role", out role))
        {
            return true;
        }

        if (dict.TryGetValue("data", out var dataObj) && dataObj is IDictionary<string, object> nested)
        {
            return TryGetString(nested, "role", out role) || TryGetString(nested, "Role", out role);
        }

        return false;
    }

    private static bool TryGetString(IDictionary<string, object> dict, string key, out string value)
    {
        value = string.Empty;

        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        value = raw.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }
}
