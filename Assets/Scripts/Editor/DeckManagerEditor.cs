#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor script que adiciona botões visuais no Inspector do DeckManager
/// </summary>
[CustomEditor(typeof(DeckManager))]
public class DeckManagerEditor : Editor
{
    private string selectedCategory = "Matemática";
    private readonly string[] categories = { "Matemática", "Geografia", "História" };

    public override void OnInspectorGUI()
    {
        // Mostrar propriedades padrão
        DrawDefaultInspector();

        DeckManager deckManager = (DeckManager)target;

        GUILayout.Space(20);
        
        // Seção de Inicialização
        GUILayout.Label("🚀 Inicialização", EditorStyles.boldLabel);
        if (GUILayout.Button("Initialize Services", GUILayout.Height(40)))
        {
            deckManager.InitializeServices();
        }

        GUILayout.Space(15);

        // Seção de Upload
        GUILayout.Label("📤 Upload de Decks", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Upload de Todos os Decks", GUILayout.Height(40)))
        {
            deckManager.UploadAllDecks();
        }

        if (GUILayout.Button("Upload do Índice", GUILayout.Height(35)))
        {
            deckManager.UploadDeckIndex();
        }

        // Seleção de categoria para upload individual
        GUILayout.Label("Categoria Específica:", EditorStyles.label);
        int selectedIndex = System.Array.IndexOf(categories, selectedCategory);
        selectedIndex = EditorGUILayout.Popup("Selecione:", selectedIndex, categories, GUILayout.Height(25));
        if (selectedIndex >= 0)
        {
            selectedCategory = categories[selectedIndex];
        }

        if (GUILayout.Button($"Upload de {selectedCategory}", GUILayout.Height(35)))
        {
            deckManager.UploadDeck(selectedCategory);
        }

        GUILayout.Space(15);

        // Seção de Validação
        GUILayout.Label("🔍 Validação de Decks", EditorStyles.boldLabel);

        if (GUILayout.Button("Validar Todos os Decks", GUILayout.Height(40)))
        {
            deckManager.ValidateAllDecks();
        }

        if (GUILayout.Button($"Validar {selectedCategory}", GUILayout.Height(35)))
        {
            deckManager.ValidateDeck(selectedCategory);
        }

        GUILayout.Space(15);

        // Info Box
        EditorGUILayout.HelpBox(
            "💡 Dicas:\n" +
            "1. Clique 'Initialize Services' para conectar no PlayFab\n" +
            "2. Use 'Upload' para enviar decks\n" +
            "3. Use 'Validar' para conferir se funcionou\n" +
            "4. Abra o Console para ver detalhes",
            MessageType.Info
        );
    }
}

#endif
