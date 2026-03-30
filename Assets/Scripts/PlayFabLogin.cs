using UnityEngine;

/// <summary>
/// Inicializador de PlayFab
/// Responsável apenas por iniciar o PlayFabService centralizado
/// </summary>
public class PlayFabLogin : MonoBehaviour
{
    private void Start()
    {
        // Verificar se PlayFabService existe, se não, criar um novo GameObject com o serviço
        if (PlayFabService.Instance == null)
        {
            var playFabServiceGO = new GameObject("PlayFabService");
            playFabServiceGO.AddComponent<PlayFabService>();
        }

        // Inicializar o PlayFab através do serviço centralizado
        PlayFabService.Instance.Initialize();
        Debug.Log("[PlayFabLogin] PlayFab inicializado com sucesso!");
    }
}