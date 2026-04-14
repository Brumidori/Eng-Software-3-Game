#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor script que mantém apenas a inicialização básica do PlayFabService.
/// </summary>
[CustomEditor(typeof(DeckManager))]
public class DeckManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DeckManager deckManager = (DeckManager)target;

        GUILayout.Space(20);

        GUILayout.Label("🚀 Inicialização", EditorStyles.boldLabel);
        if (GUILayout.Button("Initialize Services", GUILayout.Height(40)))
        {
            deckManager.InitializeServices();
        }

        GUILayout.Space(15);

        GUILayout.Space(15);

        EditorGUILayout.HelpBox(
            "💡 O bootstrap do PlayFab agora é focado em autenticacao e servicos base.\n" +
            "As operacoes de upload e validacao de decks foram descontinuadas.",
            MessageType.Info
        );
    }
}

#endif
