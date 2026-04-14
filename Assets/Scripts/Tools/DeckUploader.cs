using UnityEngine;

/// <summary>
/// Componente legado mantido apenas para compatibilidade temporária de cenas.
/// O upload de decks foi descontinuado e não executa mais operações no PlayFab.
/// </summary>
public class DeckUploader : MonoBehaviour
{
    public static void UploadDeckFile(string fileName)
    {
        Debug.LogWarning($"[DeckUploader] Upload do arquivo '{fileName}' foi descontinuado e não será executado.");
    }

    public static void UploadDeckIndex()
    {
        Debug.LogWarning("[DeckUploader] Upload do deck_index foi descontinuado e não será executado.");
    }

    public static void UploadDeck(string categoria)
    {
        Debug.LogWarning($"[DeckUploader] Upload do deck '{categoria}' foi descontinuado e não será executado.");
    }

    public static void UploadAllDecks()
    {
        Debug.LogWarning("[DeckUploader] Upload de todos os decks foi descontinuado e não será executado.");
    }

    public static void UploadDeckToPlayerData(string key, string jsonContent)
    {
        Debug.LogWarning($"[DeckUploader] Persistência de deck em Player Data para '{key}' foi descontinuada e não será executada.");
    }
}


