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
    [SerializeField] private Text priceText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private CanvasGroup canvasGroup;

    private StoreItemData currentItem;
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
    public void Show(StoreItemData item, Action<StoreItemData> onConfirm, Action onCancel)
    {
        if (item == null)
        {
            Debug.LogError("[PurchaseConfirmationPanel] Item data é nulo");
            return;
        }

        currentItem = item;
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

        if (priceText != null)
            priceText.text = $"{currentItem.price} {currentItem.virtualCurrency}";

        // Confirmar sempre que houver item selecionado
        if (confirmButton != null)
            confirmButton.interactable = currentItem != null;
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
