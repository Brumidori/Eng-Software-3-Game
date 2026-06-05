using System;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace BrainDuel.Match.Core
{
    // Chama handlers do Legacy CloudScript V1 via PlayFabClientAPI.ExecuteCloudScript.
    // NÃO usar PlayFabCloudScriptAPI.ExecuteFunction — essa API é para CloudScript V2 (Azure Functions).
    public static class CloudScriptClient
    {
        public static void Call(string functionName, object args,
            Action<object> onSuccess = null,
            Action<string> onError   = null)
        {
            PlayFabClientAPI.ExecuteCloudScript(
                new ExecuteCloudScriptRequest
                {
                    FunctionName           = functionName,
                    FunctionParameter      = args,
                    GeneratePlayStreamEvent = false
                },
                result =>
                {
                    if (result.Error != null)
                    {
                        Debug.LogError($"[CloudScript] {functionName} erro no script: {result.Error.Message}");
                        onError?.Invoke(result.Error.Message);
                        return;
                    }
                    onSuccess?.Invoke(result.FunctionResult);
                },
                error =>
                {
                    Debug.LogError($"[CloudScript] {functionName} falhou: {error.ErrorMessage}");
                    onError?.Invoke(error.ErrorMessage);
                });
        }
    }
}
