using System;
using PlayFab;
using PlayFab.CloudScriptModels;
using UnityEngine;

namespace BrainDuel.Match.Core
{
    public static class CloudScriptClient
    {
        public static void Call(string functionName, object args,
            Action<object> onSuccess = null,
            Action<string> onError   = null)
        {
            PlayFabCloudScriptAPI.ExecuteFunction(
                new ExecuteFunctionRequest
                {
                    FunctionName      = functionName,
                    FunctionParameter = args
                },
                result =>
                {
                    if (result.Error != null)
                    {
                        Debug.LogError($"[CloudScript] {functionName} error: {result.Error.Message}");
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
