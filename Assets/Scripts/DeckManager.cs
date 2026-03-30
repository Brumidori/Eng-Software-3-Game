using UnityEngine;
using PlayFab;

/// <summary>
/// Gerenciador centralizado de Decks para upload e validação
/// Adicione este script em um GameObject para ter acesso via Inspector
/// </summary>
public class DeckManager : MonoBehaviour
{
    [SerializeField] private bool autoInitialize = true;

    private DeckUploader deckUploader;
    private DeckValidator deckValidator;

    private void Start()
    {
        if (autoInitialize)
        {
            InitializeServices();
        }
    }

    public void InitializeServices()
    {
        Debug.Log("[DeckManager] Inicializando serviços...");
        
        PlayFabService playFabService = FindFirstObjectByType<PlayFabService>();
        if (playFabService == null)
        {
            var playFabGO = new GameObject("PlayFabService");
            playFabService = playFabGO.AddComponent<PlayFabService>();
        }

        playFabService.Initialize();
        Debug.Log("[DeckManager] ✅ Serviços inicializados");
    }

    /// <summary>
    /// Faz upload de todos os decks
    /// </summary>
    public void UploadAllDecks()
    {
        if (!ValidateAuthentication()) return;
        
        Debug.Log("[DeckManager] 📤 Iniciando upload de todos os decks...");
        DeckUploader.UploadAllDecks();
    }

    /// <summary>
    /// Faz upload do índice
    /// </summary>
    public void UploadDeckIndex()
    {
        if (!ValidateAuthentication()) return;
        
        Debug.Log("[DeckManager] 📤 Fazendo upload do índice...");
        DeckUploader.UploadDeckIndex();
    }

    /// <summary>
    /// Faz upload de um deck específico
    /// </summary>
    public void UploadDeck(string categoria)
    {
        if (!ValidateAuthentication()) return;

        if (string.IsNullOrEmpty(categoria))
        {
            Debug.LogError("[DeckManager] Categoria não pode estar vazia!");
            return;
        }
        
        Debug.Log($"[DeckManager] 📤 Fazendo upload de {categoria}...");
        DeckUploader.UploadDeck(categoria.ToLower());
    }

    /// <summary>
    /// Valida todos os decks
    /// </summary>
    public void ValidateAllDecks()
    {
        if (!ValidateAuthentication()) return;

        if (deckValidator == null)
        {
            deckValidator = GetComponent<DeckValidator>();
            if (deckValidator == null)
            {
                deckValidator = gameObject.AddComponent<DeckValidator>();
            }
        }

        Debug.Log("[DeckManager] 🔍 Iniciando validação de todos os decks...");
        deckValidator.ValidateAllDecks();
    }

    /// <summary>
    /// Valida um deck específico
    /// </summary>
    public void ValidateDeck(string categoria)
    {
        if (!ValidateAuthentication()) return;

        if (string.IsNullOrEmpty(categoria))
        {
            Debug.LogError("[DeckManager] Categoria não pode estar vazia!");
            return;
        }

        if (deckValidator == null)
        {
            deckValidator = GetComponent<DeckValidator>();
            if (deckValidator == null)
            {
                deckValidator = gameObject.AddComponent<DeckValidator>();
            }
        }

        Debug.Log($"[DeckManager] 🔍 Validando {categoria}...");
        deckValidator.ValidateSingleDeck(categoria.ToLower());
    }

    private bool ValidateAuthentication()
    {
        if (!PlayFabSettings.staticPlayer.IsEntityLoggedIn())
        {
            Debug.LogError("[DeckManager] ❌ Não está autenticado no PlayFab! Clique em 'Initialize Services' primeiro.");
            return false;
        }
        return true;
    }
}
