using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;

/// <summary>
/// Valida se os decks foram salvos corretamente no PlayFab
/// Verifica cada categoria e conta as cartas
/// </summary>
public class DeckValidator : MonoBehaviour
{
    private int totalCategorias = 0;
    private int categoriasCarregadas = 0;
    private int totalCartas = 0;

    /// <summary>
    /// Inicia validação completa dos decks
    /// </summary>
    public void ValidateAllDecks()
    {
        Debug.Log("[DeckValidator] 🔍 Iniciando validação de decks...\n");

        if (!PlayFabSettings.staticPlayer.IsEntityLoggedIn())
        {
            Debug.LogError("[DeckValidator] ❌ Não está autenticado no PlayFab!");
            return;
        }

        // Carregar índice primeiro
        ValidateDeckIndex();
    }

    private void ValidateDeckIndex()
    {
        PlayFabClientAPI.GetTitleData(
            new GetTitleDataRequest
            {
                Keys = new List<string> { "deck_index" }
            },
            OnDeckIndexLoaded,
            OnError
        );
    }

    private void OnDeckIndexLoaded(GetTitleDataResult result)
    {
        if (!result.Data.ContainsKey("deck_index"))
        {
            Debug.LogError("[DeckValidator] ❌ deck_index não encontrado no PlayFab!");
            PrintSummary();
            return;
        }

        string json = result.Data["deck_index"];
        DeckIndex deckIndex = JsonUtility.FromJson<DeckIndex>(json);

        if (deckIndex == null || deckIndex.categorias == null)
        {
            Debug.LogError("[DeckValidator] ❌ Falha ao desserializar deck_index!");
            PrintSummary();
            return;
        }

        Debug.Log("[DeckValidator] ✅ deck_index encontrado!");
        Debug.Log($"[DeckValidator] 📋 Total de categorias: {deckIndex.categorias.Count}\n");

        totalCategorias = deckIndex.categorias.Count;

        // Validar cada categoria
        foreach (var categoria in deckIndex.categorias)
        {
            ValidateDeck(categoria);
        }
    }

    private void ValidateDeck(CategoriaInfo categoria)
    {
        PlayFabClientAPI.GetTitleData(
            new GetTitleDataRequest
            {
                Keys = new List<string> { categoria.key }
            },
            result => OnDeckLoaded(result, categoria),
            OnError
        );
    }

    private void OnDeckLoaded(GetTitleDataResult result, CategoriaInfo categoria)
    {
        Debug.Log($"[DeckValidator] 🔍 Validando: {categoria.nome}");

        if (!result.Data.ContainsKey(categoria.key))
        {
            Debug.LogError($"[DeckValidator]   ❌ {categoria.nome} NÃO encontrada! (Chave: {categoria.key})");
            categoriasCarregadas++;
            if (categoriasCarregadas == totalCategorias)
                PrintSummary();
            return;
        }

        string json = result.Data[categoria.key];
        DeckWrapper wrapper = JsonUtility.FromJson<DeckWrapper>(json);

        if (wrapper == null || wrapper.deck == null)
        {
            Debug.LogError($"[DeckValidator]   ❌ Falha ao desserializar {categoria.nome}!");
            categoriasCarregadas++;
            if (categoriasCarregadas == totalCategorias)
                PrintSummary();
            return;
        }

        int cartasCount = wrapper.deck.Count;
        totalCartas += cartasCount;

        Debug.Log($"[DeckValidator]   ✅ {categoria.nome}: {cartasCount} cartas carregadas");

        // Validar primeira carta
        if (cartasCount > 0)
        {
            Carta firstCard = wrapper.deck[0];
            Debug.Log($"[DeckValidator]      └─ Amostra: \"{firstCard.pergunta}\"");
        }

        categoriasCarregadas++;
        
        // Se todas as categorias foram carregadas, mostrar resumo
        if (categoriasCarregadas == totalCategorias)
        {
            PrintSummary();
        }
    }

    private void OnError(PlayFabError error)
    {
        Debug.LogError($"[DeckValidator] ❌ Erro PlayFab: {error.GenerateErrorReport()}");
        PrintSummary();
    }

    private void PrintSummary()
    {
        Debug.Log("\n" + 
            "╔════════════════════════════════════════════╗\n" +
            "║          RESUMO DE VALIDAÇÃO              ║\n" +
            "╚════════════════════════════════════════════╝");

        Debug.Log($"📊 Categorias encontradas: {categoriasCarregadas}/{totalCategorias}");
        Debug.Log($"📚 Total de cartas: {totalCartas}");

        if (categoriasCarregadas == totalCategorias && totalCartas > 0)
        {
            Debug.Log("\n✅ ✅ ✅ VALIDAÇÃO COMPLETA COM SUCESSO! ✅ ✅ ✅");
            Debug.Log("Todos os decks estão salvos no PlayFab corretamente!");
        }
        else if (categoriasCarregadas < totalCategorias)
        {
            Debug.LogError("\n❌ ALGUMAS CATEGORIAS ESTÃO FALTANDO!");
            Debug.LogError("Faça upload de todos os decks novamente.");
        }
        else if (totalCartas == 0)
        {
            Debug.LogError("\n❌ NENHUMA CARTA FOI ENCONTRADA!");
        }

        Debug.Log("═══════════════════════════════════════════\n");
    }

    /// <summary>
    /// Valida apenas uma categoria específica
    /// </summary>
    public void ValidateSingleDeck(string categoria)
    {
        if (!PlayFabSettings.staticPlayer.IsEntityLoggedIn())
        {
            Debug.LogError("[DeckValidator] ❌ Não está autenticado no PlayFab!");
            return;
        }

        Debug.Log($"[DeckValidator] 🔍 Validando deck de {categoria}...\n");

        string key = $"cartas_{categoria.ToLower()}";

        PlayFabClientAPI.GetTitleData(
            new GetTitleDataRequest
            {
                Keys = new List<string> { key }
            },
            result => OnSingleDeckLoaded(result, categoria, key),
            OnError
        );
    }

    private void OnSingleDeckLoaded(GetTitleDataResult result, string categoria, string key)
    {
        if (!result.Data.ContainsKey(key))
        {
            Debug.LogError($"[DeckValidator] ❌ {categoria} não encontrada! (Chave: {key})");
            return;
        }

        string json = result.Data[key];
        DeckWrapper wrapper = JsonUtility.FromJson<DeckWrapper>(json);

        if (wrapper == null || wrapper.deck == null)
        {
            Debug.LogError($"[DeckValidator] ❌ Falha ao desserializar {categoria}!");
            return;
        }

        int cartasCount = wrapper.deck.Count;
        Debug.Log($"[DeckValidator] ✅ {categoria}: {cartasCount} cartas encontradas!");

        // Listar todas as cartas
        Debug.Log($"\n[DeckValidator] 📝 Cartas de {categoria}:");
        for (int i = 0; i < wrapper.deck.Count; i++)
        {
            Carta carta = wrapper.deck[i];
            Debug.Log($"  {i + 1}. [{carta.dificuldade}] {carta.pergunta}");
            Debug.Log($"     └─ Resposta correta: {carta.alternativas[carta.respostaCorreta]}");
        }

        Debug.Log($"\n✅ Validação de {categoria} concluída!");
    }
}
