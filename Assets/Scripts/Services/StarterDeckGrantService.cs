using System;
using System.Collections;
using System.Collections.Generic;
using PlayFab;
using UnityEngine;
using BrainDuel.Match.Core;

public class StarterDeckGrantResult
{
    public bool Success { get; set; }
    public bool AlreadyGranted { get; set; }
    public string CatalogVersion { get; set; }
    public List<string> GrantedItemIds { get; set; } = new List<string>();
    public string Error { get; set; }
}

public class StarterDeckGrantService : MonoBehaviour
{
    private const string DefaultFunctionName = "GrantStarterDecks";

    public static StarterDeckGrantService Instance { get; private set; }

    [SerializeField] private string grantFunctionName = DefaultFunctionName;
    [SerializeField] private string catalogVersion = "mainCatalog";

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

    public void GrantStarterDecks(Action<StarterDeckGrantResult> onComplete = null)
    {
        if (!ValidateAuth())
        {
            onComplete?.Invoke(new StarterDeckGrantResult { Success = false, Error = "Sessao PlayFab nao autenticada." });
            return;
        }

        // Usa CloudScriptClient que chama Legacy CloudScript V1 via ExecuteCloudScript
        CloudScriptClient.Call(
            grantFunctionName,
            new { catalogVersion = catalogVersion },
            onSuccess: result =>
            {
                if (TryExtractResult(result, out var parsed))
                    onComplete?.Invoke(parsed);
                else
                    onComplete?.Invoke(new StarterDeckGrantResult { Success = false, Error = "Resposta inesperada do servidor." });
            },
            onError: err => onComplete?.Invoke(new StarterDeckGrantResult { Success = false, Error = err })
        );
    }

    private static bool TryExtractResult(object functionResult, out StarterDeckGrantResult parsedResult)
    {
        parsedResult = null;

        if (functionResult == null)
        {
            return false;
        }

        if (functionResult is IDictionary<string, object> typedDict)
        {
            parsedResult = ParseFromDictionary(typedDict);
            return parsedResult != null;
        }

        if (functionResult is string json)
        {
            try
            {
                var dto = JsonUtility.FromJson<StarterDeckGrantResultJson>(json);
                if (dto == null)
                {
                    return false;
                }

                parsedResult = new StarterDeckGrantResult
                {
                    Success = dto.success,
                    AlreadyGranted = dto.alreadyGranted,
                    CatalogVersion = dto.catalogVersion,
                    GrantedItemIds = dto.grantedItemIds != null ? new List<string>(dto.grantedItemIds) : new List<string>(),
                    Error = dto.error
                };

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StarterDeckGrantService] Erro ao parsear JSON do CloudScript: {ex.Message}");
            }
        }

        return false;
    }

    private static StarterDeckGrantResult ParseFromDictionary(IDictionary<string, object> dict)
    {
        if (dict == null)
        {
            return null;
        }

        var result = new StarterDeckGrantResult
        {
            Success = TryGetBool(dict, "success", out var success) && success,
            AlreadyGranted = TryGetBool(dict, "alreadyGranted", out var alreadyGranted) && alreadyGranted,
            CatalogVersion = TryGetString(dict, "catalogVersion")
        };

        if (dict.TryGetValue("grantedItemIds", out var grantedObj))
        {
            result.GrantedItemIds = ExtractStringList(grantedObj);
        }
        else if (dict.TryGetValue("eligibleItemIds", out var eligibleObj))
        {
            result.GrantedItemIds = ExtractStringList(eligibleObj);
        }

        if (TryGetString(dict, "error", out var error))
        {
            result.Error = error;
        }

        if (!result.Success && string.IsNullOrWhiteSpace(result.Error))
        {
            result.Error = "Falha ao conceder decks iniciais.";
        }

        return result;
    }

    private static List<string> ExtractStringList(object rawValue)
    {
        var items = new List<string>();

        if (rawValue is IEnumerable enumerable)
        {
            foreach (var entry in enumerable)
            {
                if (entry == null)
                {
                    continue;
                }

                var value = entry.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    items.Add(value);
                }
            }
        }

        return items;
    }

    private static bool TryGetBool(IDictionary<string, object> dict, string key, out bool value)
    {
        value = false;

        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        if (raw is bool boolValue)
        {
            value = boolValue;
            return true;
        }

        return bool.TryParse(raw.ToString(), out value);
    }

    private static string TryGetString(IDictionary<string, object> dict, string key)
    {
        return TryGetString(dict, key, out var value) ? value : string.Empty;
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

    private bool ValidateAuth()
    {
        return PlayFabService.Instance != null && PlayFabService.Instance.IsLoggedIn();
    }

    [Serializable]
    private class StarterDeckGrantResultJson
    {
        public bool success;
        public bool alreadyGranted;
        public string catalogVersion;
        public string error;
        public string[] grantedItemIds;
    }
}
