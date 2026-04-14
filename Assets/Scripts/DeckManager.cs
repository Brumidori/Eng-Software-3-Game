using UnityEngine;

/// <summary>
/// Gerenciador legado de decks mantido apenas para compatibilidade temporária.
/// As operações de upload e validação foram descontinuadas.
/// </summary>
public class DeckManager : MonoBehaviour
{
    [SerializeField] private bool autoInitialize = true;

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

    public void UploadAllDecks()
    {
        Debug.LogWarning("[DeckManager] Upload de decks foi descontinuado e não será executado.");
    }

    public void UploadDeckIndex()
    {
        Debug.LogWarning("[DeckManager] Upload do deck_index foi descontinuado e não será executado.");
    }

    public void UploadDeck(string categoria)
    {
        if (string.IsNullOrEmpty(categoria))
        {
            Debug.LogError("[DeckManager] Categoria não pode estar vazia!");
            return;
        }

        Debug.LogWarning($"[DeckManager] Upload do deck '{categoria}' foi descontinuado e não será executado.");
    }

    public void ValidateAllDecks()
    {
        Debug.LogWarning("[DeckManager] Validação de decks foi descontinuada e não será executada.");
    }

    public void ValidateDeck(string categoria)
    {
        if (string.IsNullOrEmpty(categoria))
        {
            Debug.LogError("[DeckManager] Categoria não pode estar vazia!");
            return;
        }

        Debug.LogWarning($"[DeckManager] Validação do deck '{categoria}' foi descontinuada e não será executada.");
    }
}
