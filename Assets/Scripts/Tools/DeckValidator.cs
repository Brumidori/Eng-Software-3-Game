using UnityEngine;

/// <summary>
/// Componente legado mantido apenas para compatibilidade temporária de cenas.
/// A validação de decks foi descontinuada e não executa mais chamadas ao PlayFab.
/// </summary>
public class DeckValidator : MonoBehaviour
{
    public void ValidateAllDecks()
    {
        Debug.LogWarning("[DeckValidator] Validação de decks foi descontinuada e não será executada.");
    }

    public void ValidateSingleDeck(string categoria)
    {
        Debug.LogWarning($"[DeckValidator] Validação do deck '{categoria}' foi descontinuada e não será executada.");
    }
}
