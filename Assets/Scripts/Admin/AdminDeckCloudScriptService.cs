using System;
using System.Collections;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class AdminDeckCloudScriptService : MonoBehaviour
{
    private const string FnListCatalog = "DeckAdminListCatalog";
    private const string FnGetDeck = "DeckAdminGetDeck";
    private const string FnCreateDeck = "DeckAdminCreateDeck";
    private const string FnUpdateDeck = "DeckAdminUpdateDeck";
    private const string FnDeleteDeck = "DeckAdminDeleteDeck";
    private const string FnValidateDeckPayload = "DeckAdminValidateDeckPayload";
    private const string FnToggleDeck = "DeckAdminToggleDeck";

    public static AdminDeckCloudScriptService Instance { get; private set; }

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

    public void ListCatalog(Action<CloudScriptEnvelope> callback)
    {
        Execute(FnListCatalog, new Dictionary<string, object>(), callback);
    }

    public void ToggleDeck(string key, Action<CloudScriptEnvelope> callback)
    {
        Execute(FnToggleDeck, new Dictionary<string, object> { { "key", key } }, callback);
    }

    public void GetDeck(string key, Action<CloudScriptEnvelope> callback)
    {
        Execute(FnGetDeck, new Dictionary<string, object>
        {
            { "key", key }
        }, callback);
    }

    public void ValidateDeckPayload(AdminDeckRequestDto request, Action<CloudScriptEnvelope> callback)
    {
        Execute(FnValidateDeckPayload, BuildDeckRequest(request), callback);
    }

    public void CreateDeck(AdminDeckRequestDto request, Action<CloudScriptEnvelope> callback)
    {
        Execute(FnCreateDeck, BuildDeckRequest(request), callback);
    }

    public void UpdateDeck(AdminDeckRequestDto request, Action<CloudScriptEnvelope> callback)
    {
        Execute(FnUpdateDeck, BuildDeckRequest(request), callback);
    }

    public void DeleteDeck(string key, bool clearDeckContent, Action<CloudScriptEnvelope> callback)
    {
        Execute(FnDeleteDeck, new Dictionary<string, object>
        {
            { "key", key },
            { "clearDeckContent", clearDeckContent }
        }, callback);
    }

    private static Dictionary<string, object> BuildDeckRequest(AdminDeckRequestDto request)
    {
        var envelope = new Dictionary<string, object>
        {
            { "nome", request.nome },
            { "key", request.key }
        };

        if (request.deck != null)
        {
            envelope["deck"] = BuildDeckObject(request.deck);
        }

        return envelope;
    }

    private static Dictionary<string, object> BuildDeckObject(DeckSchemaV2 deck)
    {
        var questions = new List<object>();

        if (deck.questions != null)
        {
            foreach (var q in deck.questions)
            {
                var options = new List<object>();
                if (q.options != null)
                {
                    foreach (var opt in q.options)
                    {
                        options.Add(new Dictionary<string, object>
                        {
                            { "text", opt.text },
                            { "is_correct", opt.is_correct }
                        });
                    }
                }

                questions.Add(new Dictionary<string, object>
                {
                    { "id", q.id },
                    { "text", q.text },
                    { "options", options },
                    { "time_limit", q.time_limit }
                });
            }
        }

        return new Dictionary<string, object>
        {
            { "deck_id", deck.deck_id },
            { "theme", deck.theme },
            { "questions", questions }
        };
    }

    private static void Execute(string functionName, Dictionary<string, object> parameters, Action<CloudScriptEnvelope> callback)
    {
        if (PlayFabService.Instance == null || !PlayFabService.Instance.IsLoggedIn())
        {
            callback?.Invoke(new CloudScriptEnvelope
            {
                success = false,
                error = "Sessao PlayFab nao autenticada."
            });
            return;
        }

        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = functionName,
            FunctionParameter = parameters,
            GeneratePlayStreamEvent = true
        };

        PlayFabService.Client.ExecuteCloudScript(
            request,
            result => HandleSuccess(functionName, result, callback),
            error => callback?.Invoke(new CloudScriptEnvelope
            {
                success = false,
                error = error == null ? "Falha ao executar CloudScript." : error.GenerateErrorReport()
            }));
    }

    private static void HandleSuccess(string functionName, ExecuteCloudScriptResult result, Action<CloudScriptEnvelope> callback)
    {
        if (result.Error != null)
        {
            callback?.Invoke(new CloudScriptEnvelope
            {
                success = false,
                error = result.Error.Message
            });
            return;
        }

        if (!TryGetFunctionResultMap(result.FunctionResult, out var resultMap))
        {
            callback?.Invoke(new CloudScriptEnvelope
            {
                success = false,
                error = "Resposta inesperada do CloudScript."
            });

            var typeName = result.FunctionResult == null ? "null" : result.FunctionResult.GetType().FullName;
            Debug.LogWarning($"[AdminDeckCloudScriptService] {functionName} retornou tipo inesperado em FunctionResult: {typeName}");
            return;
        }

        var success = TryReadSuccessFlag(resultMap);
        resultMap.TryGetValue("error", out var rawError);
        resultMap.TryGetValue("details", out var rawDetails);

        var rawForUi = new Dictionary<string, object>(resultMap);

        callback?.Invoke(new CloudScriptEnvelope
        {
            success = success,
            error = rawError == null ? string.Empty : rawError.ToString(),
            details = rawDetails,
            raw = rawForUi
        });

        if (success)
            Debug.Log($"[AdminDeckCloudScriptService] {functionName} => success=True");
        else
        {
            var errorMsg   = rawError   != null ? rawError.ToString()   : "(sem campo error)";
            var detailsMsg = rawDetails != null ? PlayFab.Json.PlayFabSimpleJson.SerializeObject(rawDetails) : "(sem details)";
            var allKeys    = string.Join(", ", resultMap.Keys);
            Debug.LogWarning($"[AdminDeckCloudScriptService] {functionName} => success=False | error='{errorMsg}' | details={detailsMsg} | chaves={allKeys}");
        }
    }

    private static bool TryGetFunctionResultMap(object functionResult, out IDictionary<string, object> resultMap)
    {
        resultMap = null;

        if (functionResult == null)
        {
            return false;
        }

        if (functionResult is IDictionary<string, object> typed)
        {
            resultMap = typed;
            return true;
        }

        if (functionResult is IDictionary raw)
        {
            var converted = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in raw)
            {
                if (entry.Key == null)
                {
                    continue;
                }

                converted[entry.Key.ToString()] = entry.Value;
            }

            resultMap = converted;
            return converted.Count > 0;
        }

        return false;
    }

    private static bool TryReadSuccessFlag(IDictionary<string, object> map)
    {
        if (!map.TryGetValue("success", out var rawSuccess) || rawSuccess == null)
        {
            return false;
        }

        if (rawSuccess is bool boolValue)
        {
            return boolValue;
        }

        if (rawSuccess is string stringValue && bool.TryParse(stringValue, out var parsed))
        {
            return parsed;
        }

        return false;
    }
}
