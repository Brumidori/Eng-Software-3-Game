using UnityEngine;
using UnityEngine.UI;

public class CoinDisplayHUD : MonoBehaviour
{
    private const string CurrencyCode = "BC";

    [SerializeField] private Text coinText;

    private void OnEnable()
    {
        EconomyService.OnCurrencyChanged += HandleCurrencyChanged;

        if (EconomyService.Instance != null)
            EconomyService.Instance.GetBalance(CurrencyCode);
    }

    private void OnDisable()
    {
        EconomyService.OnCurrencyChanged -= HandleCurrencyChanged;
    }

    public void SetAmount(int amount)
    {
        if (coinText != null)
            coinText.text = amount.ToString("N0").Replace(",", ".");
    }

    private void HandleCurrencyChanged(string code, int amount)
    {
        if (code == CurrencyCode)
            SetAmount(amount);
    }
}
