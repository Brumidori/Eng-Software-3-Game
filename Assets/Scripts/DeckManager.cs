using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    private Dictionary<string, List<Carta>> decks = new Dictionary<string, List<Carta>>();
    private DeckIndex deckIndex;

    void Start()
    {
        Login();
    }

    void Login()
    {
       var request = new LoginWithCustomIDRequest
        {
            CustomId = "test_user_123",
            CreateAccount = false
        };

        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnError);
    }

    void OnLoginSuccess(LoginResult result)
    {
        GetDeckIndex();
    }

    void GetDeckIndex()
    {
        PlayFabClientAPI.GetTitleData(new GetTitleDataRequest
        {
            Keys = new List<string> { "deck_index" }

        }, result =>
        {
            if (result.Data.ContainsKey("deck_index"))
            {
                string json = result.Data["deck_index"];
                deckIndex = JsonUtility.FromJson<DeckIndex>(json);

                if (deckIndex == null || deckIndex.categorias == null)
                {
                    Debug.LogError("Falha ao desserializar DeckIndex!");
                }
            }
            else
            {
                Debug.LogError("Índice de decks não encontrado no Title Data!");
            }

        }, OnError);
    }

    public void LoadDeck(string categoria)
    {
        if (deckIndex == null || deckIndex.categorias == null)
        {
            Debug.LogError("DeckIndex não carregado ainda!");
            return;
        }

        var cat = deckIndex.categorias.Find(c => c.nome == categoria);

        if (cat == null)
        {
            Debug.LogError("Categoria não encontrada!");
            return;
        }

        if (decks.ContainsKey(categoria))
        {
            Debug.Log("Deck já em cache");
            return;
        }

        PlayFabClientAPI.GetTitleData(new GetTitleDataRequest
        {
            Keys = new List<string> { cat.key }

        }, result =>
        {
            if (result.Data.ContainsKey(cat.key))
            {
                string json = result.Data[cat.key];
                DeckWrapper wrapper = JsonUtility.FromJson<DeckWrapper>(json);

                if (wrapper != null && wrapper.deck != null)
                {
                    decks[categoria] = wrapper.deck;
                    Debug.Log($"Deck {categoria} carregado!");
                }
                else
                {
                    Debug.LogError($"Falha ao desserializar deck de {categoria}");
                }
            }
            else
            {
                Debug.LogError($"Dados do deck {categoria} não encontrados!");
            }

        }, OnError);
    }

    public Carta GetCarta(string categoria)
    {
        if (!decks.ContainsKey(categoria))
        {
            Debug.LogError("Deck não carregado!");
            return null;
        }

        var deck = decks[categoria];
        
        if (deck == null || deck.Count == 0)
        {
            Debug.LogError("Deck vazio!");
            return null;
        }

        int index = Random.Range(0, deck.Count);

        return deck[index];
    }

    void OnError(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());
    }
}