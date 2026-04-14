using UnityEngine;
using UnityEngine.InputSystem;

public class EconomyTester : PlayFabTerminalTester
{
    private const string Title = "EconomyTester";

    [SerializeField] private string currencyCode = "DA";
    [SerializeField] private int addAmount = 25;
    [SerializeField] private int subtractAmount = 10;

    protected override void Start()
    {
        base.Start();
        EnsureService<EconomyService>();
        EconomyService.OnCurrencyChanged += HandleCurrencyChanged;
        EconomyService.OnEconomyFailed += HandleError;
        PrintReadyMessage(Title, "1=adicionar moeda, 2=remover moeda, 3=consultar saldo, 4=adicionar saldo alto");
    }

    private void OnDestroy()
    {
        EconomyService.OnCurrencyChanged -= HandleCurrencyChanged;
        EconomyService.OnEconomyFailed -= HandleError;
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
            EconomyService.Instance.AddCurrency(currencyCode, addAmount);
            Debug.Log($"[{Title}] Solicitada adicao de {addAmount} {currencyCode}.");
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            EconomyService.Instance.SubtractCurrency(currencyCode, subtractAmount);
            Debug.Log($"[{Title}] Solicitada remocao de {subtractAmount} {currencyCode}.");
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            EconomyService.Instance.GetBalance(currencyCode);
            Debug.Log($"[{Title}] Solicitada consulta de saldo.");
        }
        else if (keyboard.digit4Key.wasPressedThisFrame)
        {
            EconomyService.Instance.AddCurrency(currencyCode, 1000);
            Debug.Log($"[{Title}] Solicitada adicao de saldo alto.");
        }
    }

    private void HandleCurrencyChanged(string currency, int balance)
    {
        Debug.Log($"[{Title}] ✅ Saldo de {currency}: {balance}");
    }

    private void HandleError(PlayFab.PlayFabError error)
    {
        Debug.LogError($"[{Title}] ❌ Erro PlayFab: {error.GenerateErrorReport()}");
    }
}