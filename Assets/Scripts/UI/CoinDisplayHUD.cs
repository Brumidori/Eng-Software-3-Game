using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Script de exibição de moeda/saldo do jogador.
/// Coloque este script no prefab CoinDisplay para que ele automaticamente busque e exiba o saldo.
/// 
/// Funciona em qualquer lugar que o prefab for instanciado.
/// </summary>
public class CoinDisplay : MonoBehaviour
{
    [SerializeField] private string currencyCode = "BC";
    [SerializeField] private Text coinText;
    [SerializeField] private Image currencyIcon;
    [SerializeField] private string currencyFormat = "N0";

    private void OnEnable()
    {
        EconomyService.OnCurrencyChanged += HandleCurrencyChanged;
        StoreService.OnPurchaseCompletedSecure += HandlePurchaseCompleted;
        PlayFabService.OnLoginSuccess += HandleLoginSuccess;

        RefreshBalance();
    }

    private void Start()
    {
        RefreshBalance();
    }

    private void OnDisable()
    {
        EconomyService.OnCurrencyChanged -= HandleCurrencyChanged;
        StoreService.OnPurchaseCompletedSecure -= HandlePurchaseCompleted;
        PlayFabService.OnLoginSuccess -= HandleLoginSuccess;
    }

    private void HandleLoginSuccess()
    {
        RefreshBalance();
    }

    private void HandlePurchaseCompleted(PurchaseResult result)
    {
        if (result == null || !result.Success)
        {
            return;
        }

        if (!string.Equals(result.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Atualiza imediatamente com o saldo retornado na compra e sincroniza no backend.
        UpdateDisplay(result.NewBalance);
        RefreshBalance();
    }

    /// <summary>
    /// Atualiza o texto com o novo saldo
    /// </summary>
    private void HandleCurrencyChanged(string code, int amount)
    {
        if (string.Equals(code, currencyCode, StringComparison.OrdinalIgnoreCase))
        {
            UpdateDisplay(amount);
        }
    }

    /// <summary>
    /// Atualiza visualmente o saldo
    /// </summary>
    private void UpdateDisplay(int amount)
    {
        if (coinText != null)
        {
            // Formata o valor: "N0" = número com separadores
            coinText.text = amount.ToString(currencyFormat).Replace(",", ".");
        }

        Debug.Log($"[CoinDisplay] Saldo atualizado: {amount} {currencyCode}");
    }

    /// <summary>
    /// Método público para atualizar o saldo manualmente (se necessário)
    /// </summary>
    public void RefreshBalance()
    {
        var economyService = EconomyService.Instance;

        if (economyService == null)
        {
            economyService = FindFirstObjectByType<EconomyService>();
        }

        if (economyService != null)
        {
            economyService.GetBalance(currencyCode);
        }
    }
}
