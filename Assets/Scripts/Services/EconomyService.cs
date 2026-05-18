using System;
using System.Collections.Generic;
using System.Collections;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

/// <summary>
/// Economy service baseado em CloudScript para evitar fraude no client.
/// Este service nao usa Economy V2 e nao chama APIs de currency legadas no client.
/// </summary>
public class EconomyService : MonoBehaviour
{
    [Header("CloudScript Functions")]
    [SerializeField] private string addCurrencyFunction = "EconomyAddCurrency";
    [SerializeField] private string subtractCurrencyFunction = "EconomySubtractCurrency";
    [SerializeField] private string getBalanceFunction = "EconomyGetBalance";
    [SerializeField] private bool generatePlayStreamEvent = true;
    [SerializeField] private bool useLatestRevision = true;

    public static EconomyService Instance { get; private set; }

    public static event Action<string, int> OnCurrencyChanged;
    public static event Action<PlayFabError> OnEconomyFailed;

    private readonly HashSet<string> pendingBalanceRequests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool waitingForLoginToFetchBalance;

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

    private void OnDestroy()
    {
        PlayFabService.OnLoginSuccess -= HandlePlayFabLoginSuccess;
    }

    public void AddCurrency(string virtualCurrencyCode, int amount)
    {
        if (!ValidateRequest(virtualCurrencyCode, amount))
        {
            return;
        }

        ExecuteEconomyFunction(
            addCurrencyFunction,
            new Dictionary<string, object>
            {
                { "currencyCode", virtualCurrencyCode },
                { "amount", amount }
            },
            virtualCurrencyCode,
            "adicao"
        );
    }

    public void SubtractCurrency(string virtualCurrencyCode, int amount)
    {
        if (!ValidateRequest(virtualCurrencyCode, amount))
        {
            return;
        }

        ExecuteEconomyFunction(
            subtractCurrencyFunction,
            new Dictionary<string, object>
            {
                { "currencyCode", virtualCurrencyCode },
                { "amount", amount }
            },
            virtualCurrencyCode,
            "subtracao"
        );
    }

    public void GetBalance(string virtualCurrencyCode)
    {
        if (string.IsNullOrWhiteSpace(virtualCurrencyCode))
        {
            Debug.LogError("[EconomyService] Moeda virtual nao pode ser vazia.");
            return;
        }

        if (!ValidateAuth(false))
        {
            QueueBalanceRequestUntilLogin(virtualCurrencyCode);
            return;
        }

        RequestBalanceFromCloudScript(virtualCurrencyCode);
    }

    private void RequestBalanceFromCloudScript(string virtualCurrencyCode)
    {
        if (!ValidateAuth())
        {
            return;
        }

        ExecuteEconomyFunction(
            getBalanceFunction,
            new Dictionary<string, object>
            {
                { "currencyCode", virtualCurrencyCode }
            },
            virtualCurrencyCode,
            "consulta"
        );
    }

    private void QueueBalanceRequestUntilLogin(string virtualCurrencyCode)
    {
        pendingBalanceRequests.Add(virtualCurrencyCode);

        if (waitingForLoginToFetchBalance)
        {
            return;
        }

        waitingForLoginToFetchBalance = true;
        PlayFabService.OnLoginSuccess += HandlePlayFabLoginSuccess;
        Debug.Log("[EconomyService] Aguardando login PlayFab para consultar saldo pendente.");
    }

    private void HandlePlayFabLoginSuccess()
    {
        PlayFabService.OnLoginSuccess -= HandlePlayFabLoginSuccess;
        waitingForLoginToFetchBalance = false;

        if (pendingBalanceRequests.Count == 0)
        {
            return;
        }

        var currenciesToRefresh = new List<string>(pendingBalanceRequests);
        pendingBalanceRequests.Clear();

        foreach (string currencyCode in currenciesToRefresh)
        {
            RequestBalanceFromCloudScript(currencyCode);
        }
    }

    private void ExecuteEconomyFunction(
        string functionName,
        Dictionary<string, object> parameters,
        string virtualCurrencyCode,
        string operationLabel)
    {
        if (string.IsNullOrWhiteSpace(functionName))
        {
            Debug.LogError($"[EconomyService] Função CloudScript para {operationLabel} nao foi configurada.");
            return;
        }

        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = functionName,
            FunctionParameter = parameters,
            GeneratePlayStreamEvent = generatePlayStreamEvent,
            RevisionSelection = useLatestRevision ? CloudScriptRevisionOption.Latest : CloudScriptRevisionOption.Live
        };

        PlayFabService.Client.ExecuteCloudScript(request,
            result => OnCloudScriptSuccess(result, virtualCurrencyCode, functionName),
            OnError);

        string titleId = PlayFabSettings.staticSettings != null ? PlayFabSettings.staticSettings.TitleId : "<desconhecido>";
        string revision = useLatestRevision ? "Latest" : "Live";
        Debug.Log($"[EconomyService] CloudScript '{functionName}' acionado para moeda {virtualCurrencyCode}. TitleId={titleId}, Revision={revision}");
    }

    private void OnCloudScriptSuccess(ExecuteCloudScriptResult result, string virtualCurrencyCode, string functionName)
    {
        if (result.Error != null)
        {
            Debug.LogError($"[EconomyService] ❌ CloudScript '{functionName}' retornou erro: {result.Error.Message}");

            if (!string.IsNullOrWhiteSpace(result.Error.Message) && result.Error.Message.Contains("No function named"))
            {
                Debug.LogError(
                    "[EconomyService] A funcao CloudScript nao existe no PlayFab. " +
                    "Publique as funcoes EconomyAddCurrency, EconomySubtractCurrency e EconomyGetBalance no Automation/CloudScript do mesmo TitleId em uso. " +
                    "Se estiver usando nomes diferentes, ajuste os campos addCurrencyFunction, subtractCurrencyFunction e getBalanceFunction no Inspector do EconomyService. " +
                    "Se a funcao foi publicada agora, teste com useLatestRevision=true para evitar depender da revisao Live."
                );
            }

            return;
        }

        if (!TryGetPayload(result.FunctionResult, out Dictionary<string, object> payload))
        {
            string rawType = result.FunctionResult != null ? result.FunctionResult.GetType().FullName : "<null>";
            string rawValue = result.FunctionResult != null ? result.FunctionResult.ToString() : "<null>";
            Debug.LogError($"[EconomyService] ❌ CloudScript '{functionName}' retornou payload invalido. Type={rawType} Value={rawValue}");
            return;
        }

        if (payload.TryGetValue("success", out object successObj) && successObj is bool success && !success)
        {
            string message = payload.TryGetValue("error", out object errorObj) ? errorObj?.ToString() : "erro nao informado";
            Debug.LogError($"[EconomyService] ❌ Operacao de economia rejeitada no servidor: {message}");
            return;
        }

        if (!TryExtractBalance(payload, out int balance))
        {
            Debug.LogWarning($"[EconomyService] CloudScript '{functionName}' executado sem campo 'balance'.");
            return;
        }

        Debug.Log($"[EconomyService] ✅ Saldo de {virtualCurrencyCode} atualizado para {balance} via CloudScript.");
        OnCurrencyChanged?.Invoke(virtualCurrencyCode, balance);
    }

    private static bool TryExtractBalance(Dictionary<string, object> payload, out int balance)
    {
        balance = 0;

        if (!payload.TryGetValue("balance", out object balanceObj) || balanceObj == null)
        {
            return false;
        }

        if (balanceObj is int intValue)
        {
            balance = intValue;
            return true;
        }

        if (balanceObj is long longValue)
        {
            balance = (int)longValue;
            return true;
        }

        if (balanceObj is double doubleValue)
        {
            balance = (int)doubleValue;
            return true;
        }

        return int.TryParse(balanceObj.ToString(), out balance);
    }

    private static bool TryGetPayload(object functionResult, out Dictionary<string, object> payload)
    {
        payload = null;

        if (functionResult == null)
        {
            return false;
        }

        if (functionResult is Dictionary<string, object> dictionaryPayload)
        {
            payload = dictionaryPayload;
            return true;
        }

        if (functionResult is IDictionary<string, object> genericDictionary)
        {
            payload = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> pair in genericDictionary)
            {
                payload[pair.Key] = pair.Value;
            }
            return true;
        }

        if (functionResult is IDictionary nonGenericDictionary)
        {
            payload = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in nonGenericDictionary)
            {
                if (entry.Key != null)
                {
                    payload[entry.Key.ToString()] = entry.Value;
                }
            }
            return payload.Count > 0;
        }

        return false;
    }

    private bool ValidateRequest(string virtualCurrency, int amount)
    {
        if (!ValidateAuth())
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(virtualCurrency))
        {
            Debug.LogError("[EconomyService] Moeda virtual nao pode ser vazia.");
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogError("[EconomyService] Quantidade deve ser maior que zero.");
            return false;
        }

        return true;
    }

    private bool ValidateAuth(bool logWarning = true)
    {
        if (PlayFabService.Instance == null || !PlayFabService.Instance.IsLoggedIn())
        {
            if (logWarning)
            {
                Debug.LogWarning("[EconomyService] Login PlayFab ainda nao foi concluido.");
            }
            return false;
        }

        return true;
    }

    private void OnError(PlayFabError error)
    {
        Debug.LogError($"[EconomyService] ❌ Erro PlayFab: {error.GenerateErrorReport()}");
        OnEconomyFailed?.Invoke(error);
    }
}