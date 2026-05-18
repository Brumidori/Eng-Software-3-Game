using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal de confirmação antes de comprar um item da loja.
/// Exibe informações do item e permite ao jogador confirmar ou cancelar.
/// </summary>
public class PurchaseConfirmationPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text itemNameText;
    [SerializeField] private Text itemDescriptionText;
    [SerializeField] private Text priceText;
    [SerializeField] private Text currentBalanceText;
    [SerializeField] private Text warningText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private CanvasGroup canvasGroup;

    private StoreItemData currentItem;
    private int currentBalance;
    private Action<StoreItemData> onConfirmCallback;
    private Action onCancelCallback;
    private bool isVisible;

    private void Awake()
    {
        // Configurar listeners se não estiverem no Inspector
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClick);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClick);

        // Canvas group para show/hide
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        // Esconder por padrão
        SetVisible(false);
    }

    private void OnEnable()
    {
        StoreService.OnPurchaseFailed += HandlePurchaseFailed;
    }

    private void OnDisable()
    {
        StoreService.OnPurchaseFailed -= HandlePurchaseFailed;
    }

    private void Update()
    {
        // ESC para cancelar se modal está visível
        if (isVisible && Input.GetKeyDown(KeyCode.Escape))
        {
            OnCancelClick();
        }

        // Enter para confirmar se modal está visível
        if (isVisible && Input.GetKeyDown(KeyCode.Return))
        {
            OnConfirmClick();
        }
    }

    /// <summary>
    /// Mostra o modal com dados do item a ser comprado
    /// </summary>
    public void Show(StoreItemData item, int currentBalance, Action<StoreItemData> onConfirm, Action onCancel)
    {
        if (item == null)
        {
            Debug.LogError("[PurchaseConfirmationPanel] Item data é nulo");
            return;
        }

        currentItem = item;
        this.currentBalance = currentBalance;
        onConfirmCallback = onConfirm;
        onCancelCallback = onCancel;

        UpdateUIWithItemData();
        SetVisible(true);
    }

    /// <summary>
    /// Esconde o modal
    /// </summary>
    public void Hide()
    {
        SetVisible(false);
    }

    private void UpdateUIWithItemData()
    {
        if (itemNameText != null)
            itemNameText.text = string.IsNullOrEmpty(currentItem.displayName) 
                ? currentItem.itemId 
                : currentItem.displayName;

        if (itemDescriptionText != null)
            itemDescriptionText.text = currentItem.description ?? string.Empty;

        if (priceText != null)
            priceText.text = $"{currentItem.price} {currentItem.virtualCurrency}";

        if (currentBalanceText != null)
            currentBalanceText.text = $"Saldo: {currentBalance} {currentItem.virtualCurrency}";

        // Verificar se tem saldo suficiente
        var hasSufficientBalance = currentBalance >= currentItem.price;
        
        if (warningText != null)
        {
            if (!hasSufficientBalance)
            {
                warningText.text = $"⚠️ Saldo insuficiente! Faltam {currentItem.price - currentBalance} {currentItem.virtualCurrency}";
                warningText.color = new Color(1f, 0.5f, 0f); // Orange
            }
            else
            {
                warningText.text = string.Empty;
            }
        }

        // Desabilitar botão de confirmação se saldo insuficiente
        if (confirmButton != null)
            confirmButton.interactable = hasSufficientBalance;
    }

    private void OnConfirmClick()
    {
        if (currentItem == null)
            return;

        Debug.Log($"[PurchaseConfirmationPanel] Compra confirmada: {currentItem.itemId}");
        
        Hide();
        onConfirmCallback?.Invoke(currentItem);
    }

    private void OnCancelClick()
    {
        Debug.Log("[PurchaseConfirmationPanel] Compra cancelada pelo usuário");
        
        Hide();
        onCancelCallback?.Invoke();
    }

    private void HandlePurchaseFailed(string errorMessage)
    {
        // Se compra falhar, exibir erro no modal
        if (warningText != null && isVisible)
        {
            warningText.text = $"❌ Erro: {errorMessage}";
            warningText.color = new Color(1f, 0f, 0f); // Red
        }
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }
}
