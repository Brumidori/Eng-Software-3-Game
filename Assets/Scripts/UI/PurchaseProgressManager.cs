using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gerencia o estado de loading/progresso durante uma compra.
/// Exibe spinner, bloqueia interações e implementa timeout.
/// </summary>
public class PurchaseProgressManager : MonoBehaviour
{
    public static PurchaseProgressManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private CanvasGroup loadingPanel;
    [SerializeField] private Image spinnerImage;
    [SerializeField] private Text progressText;
    [SerializeField] private float spinnerRotationSpeed = 360f;
    [SerializeField] private float purchaseTimeoutSeconds = 30f;

    private float purchaseStartTime;
    private bool isPurchaseInProgress;
    private float timeoutWarningThreshold = 0.8f; // 80% do tempo de timeout

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (loadingPanel == null)
            loadingPanel = GetComponent<CanvasGroup>();

        // Esconder por padrão
        SetLoadingVisible(false);
    }

    private void OnEnable()
    {
        StoreService.OnPurchaseCompletedSecure += HandlePurchaseCompleted;
        StoreService.OnPurchaseFailed += HandlePurchaseFailed;
    }

    private void OnDisable()
    {
        StoreService.OnPurchaseCompletedSecure -= HandlePurchaseCompleted;
        StoreService.OnPurchaseFailed -= HandlePurchaseFailed;
    }

    private void Update()
    {
        if (!isPurchaseInProgress)
            return;

        // Rotacionar spinner
        if (spinnerImage != null)
        {
            spinnerImage.transform.Rotate(0, 0, -spinnerRotationSpeed * Time.deltaTime);
        }

        // Verificar timeout
        var elapsedTime = Time.time - purchaseStartTime;
        var timeoutProgress = elapsedTime / purchaseTimeoutSeconds;

        if (progressText != null)
        {
            progressText.text = $"Processando... {(int)(elapsedTime)}s";
        }

        if (timeoutProgress >= 1f)
        {
            Debug.LogWarning("[PurchaseProgressManager] Timeout na compra!");
            EndPurchaseProgress("Timeout ao processar compra");
        }
        else if (timeoutProgress >= timeoutWarningThreshold && progressText != null)
        {
            progressText.text = $"⏱️ Processando... {(int)(elapsedTime)}s";
        }
    }

    /// <summary>
    /// Inicia o estado de loading de compra
    /// </summary>
    public void StartPurchaseProgress(string itemName = "Item")
    {
        isPurchaseInProgress = true;
        purchaseStartTime = Time.time;

        SetLoadingVisible(true);

        if (progressText != null)
            progressText.text = $"Comprando {itemName}...";

        Debug.Log("[PurchaseProgressManager] ⏳ Iniciado processo de compra");
    }

    /// <summary>
    /// Finaliza o estado de loading de compra com sucesso
    /// </summary>
    public void EndPurchaseProgress(string successMessage = "Compra realizada!")
    {
        if (!isPurchaseInProgress)
            return;

        isPurchaseInProgress = false;

        if (progressText != null)
            progressText.text = $"✅ {successMessage}";

        // Manter visível por um tempo para mostrar mensagem de sucesso
        Invoke(nameof(SetLoadingInvisible), 1.5f);

        Debug.Log($"[PurchaseProgressManager] {successMessage}");
    }

    /// <summary>
    /// Finaliza o estado de loading com erro
    /// </summary>
    public void EndPurchaseProgressWithError(string errorMessage = "Erro na compra")
    {
        if (!isPurchaseInProgress)
            return;

        isPurchaseInProgress = false;

        if (progressText != null)
            progressText.text = $"❌ {errorMessage}";

        // Manter visível por um tempo para mostrar mensagem de erro
        Invoke(nameof(SetLoadingInvisible), 2f);

        Debug.LogError($"[PurchaseProgressManager] {errorMessage}");
    }

    private void HandlePurchaseCompleted(PurchaseResult result)
    {
        if (result.Success)
        {
            EndPurchaseProgress($"Compra realizada! Novo saldo: {result.NewBalance}");
        }
        else
        {
            EndPurchaseProgressWithError(result.Error ?? "Compra falhou");
        }
    }

    private void HandlePurchaseFailed(string errorMessage)
    {
        EndPurchaseProgressWithError(errorMessage);
    }

    private void SetLoadingVisible(bool visible)
    {
        if (loadingPanel != null)
        {
            loadingPanel.alpha = visible ? 1f : 0f;
            loadingPanel.interactable = visible;
            loadingPanel.blocksRaycasts = visible;
        }
    }

    private void SetLoadingInvisible()
    {
        SetLoadingVisible(false);
    }
}
