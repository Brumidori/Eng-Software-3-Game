using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System;

/// <summary>
/// Serviço de gerenciamento de decks
/// Centraliza acesso aos decks do PlayFab com cache local
/// Depende de PlayFabService estar inicializado
/// </summary>
public class DeckService : MonoBehaviour
{
    public static DeckService Instance { get; private set; }

    private Dictionary<string, List<Carta>> decksCache = new Dictionary<string, List<Carta>>();
    private DeckIndex deckIndex;
    private bool isInitialized = false;

    public static event Action OnDecksLoaded;
    public static event Action<PlayFabError> OnDecksLoadFailed;

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

    private void Start()
    {
        // Aguardar login do PlayFab antes de carregar decks
        if (PlayFabService.Instance != null && PlayFabService.Instance.IsLoggedIn())
        {
            Initialize();
        }
        else
        {
            PlayFabService.OnLoginSuccess += Initialize;
        }
    }

    /// <summary>
    /// Inicializa o serviço e carrega o índice de decks
    /// </summary>
    public void Initialize()
    {
        if (isInitialized) return;

        Debug.Log("[DeckService] Inicializando...");
        LoadDeckIndex();
        isInitialized = true;
    }

    /// <summary>
    /// Carrega o índice de decks disponíveis
    /// </summary>
    private void LoadDeckIndex()
    {
        PlayFabClientAPI.GetTitleData(
            new GetTitleDataRequest
            {
                Keys = new List<string> { "deck_index" }
            },
            OnDeckIndexLoaded,
            OnError
        );

        Debug.Log("[DeckService] Carregando índice de decks...");
    }

    private void OnDeckIndexLoaded(GetTitleDataResult result)
    {
        if (result.Data.ContainsKey("deck_index"))
        {
            string json = result.Data["deck_index"];
            deckIndex = JsonUtility.FromJson<DeckIndex>(json);

            if (deckIndex == null || deckIndex.categorias == null)
            {
                Debug.LogError("[DeckService] ❌ Falha ao desserializar DeckIndex!");
                OnDecksLoadFailed?.Invoke(null);
                return;
            }

            Debug.Log($"[DeckService] ✅ Índice carregado com {deckIndex.categorias.Count} categorias");
            OnDecksLoaded?.Invoke();
        }
        else
        {
            Debug.LogError("[DeckService] ❌ Índice de decks não encontrado no Title Data!");
            OnDecksLoadFailed?.Invoke(null);
        }
    }

    /// <summary>
    /// Carrega um deck específico por categoria
    /// </summary>
    public void LoadDeck(string categoria)
    {
        if (deckIndex == null || deckIndex.categorias == null)
        {
            Debug.LogError("[DeckService] ❌ DeckIndex não carregado ainda!");
            return;
        }

        var cat = deckIndex.categorias.Find(c => c.nome == categoria);

        if (cat == null)
        {
            Debug.LogError($"[DeckService] ❌ Categoria '{categoria}' não encontrada!");
            return;
        }

        // Retornar se já está em cache
        if (decksCache.ContainsKey(categoria))
        {
            Debug.Log($"[DeckService] Deck '{categoria}' já em cache ({decksCache[categoria].Count} cartas)");
            return;
        }

        PlayFabClientAPI.GetTitleData(
            new GetTitleDataRequest
            {
                Keys = new List<string> { cat.key }
            },
            result => OnDeckLoaded(result, categoria),
            OnError
        );

        Debug.Log($"[DeckService] Carregando deck: {categoria}...");
    }

    private void OnDeckLoaded(GetTitleDataResult result, string categoria)
    {
        var cat = deckIndex.categorias.Find(c => c.nome == categoria);

        if (result.Data.ContainsKey(cat.key))
        {
            string json = result.Data[cat.key];
            DeckWrapper wrapper = JsonUtility.FromJson<DeckWrapper>(json);

            if (wrapper != null && wrapper.deck != null)
            {
                decksCache[categoria] = wrapper.deck;
                Debug.Log($"[DeckService] ✅ Deck '{categoria}' carregado com {wrapper.deck.Count} cartas");
            }
            else
            {
                Debug.LogError($"[DeckService] ❌ Falha ao desserializar deck de '{categoria}'");
            }
        }
        else
        {
            Debug.LogError($"[DeckService] ❌ Dados do deck '{categoria}' não encontrados!");
        }
    }

    /// <summary>
    /// Retorna uma carta aleatória de um deck específico
    /// </summary>
    public Carta GetRandomCarta(string categoria)
    {
        if (!decksCache.ContainsKey(categoria))
        {
            Debug.LogError($"[DeckService] ❌ Deck '{categoria}' não carregado!");
            return null;
        }

        var deck = decksCache[categoria];

        if (deck == null || deck.Count == 0)
        {
            Debug.LogError($"[DeckService] ❌ Deck '{categoria}' vazio!");
            return null;
        }

        int index = UnityEngine.Random.Range(0, deck.Count);
        return deck[index];
    }

    /// <summary>
    /// Retorna todas as cartas de um deck
    /// </summary>
    public List<Carta> GetDeck(string categoria)
    {
        if (!decksCache.ContainsKey(categoria))
        {
            Debug.LogError($"[DeckService] ❌ Deck '{categoria}' não carregado!");
            return null;
        }

        return decksCache[categoria];
    }

    /// <summary>
    /// Retorna o índice de categorias disponíveis
    /// </summary>
    public DeckIndex GetDeckIndex()
    {
        return deckIndex;
    }

    /// <summary>
    /// Retorna todas as categorias disponíveis
    /// </summary>
    public List<string> GetAvailableCategories()
    {
        if (deckIndex == null || deckIndex.categorias == null)
            return new List<string>();

        var categories = new List<string>();
        foreach (var cat in deckIndex.categorias)
        {
            categories.Add(cat.nome);
        }
        return categories;
    }

    /// <summary>
    /// Limpa o cache de decks (útil para testes/debug)
    /// </summary>
    public void ClearCache()
    {
        decksCache.Clear();
        Debug.Log("[DeckService] Cache limpo");
    }

    private void OnError(PlayFabError error)
    {
        Debug.LogError($"[DeckService] ❌ Erro PlayFab: {error.GenerateErrorReport()}");
        OnDecksLoadFailed?.Invoke(error);
    }
}
