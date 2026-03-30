using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using System.IO;
using System.Collections;

/// <summary>
/// Utilitário para fazer upload de decks para o PlayFab via Cloud Script
/// Coloque arquivos JSON em Assets/DeckData/ e use este script para enviar
/// 
/// O Cloud Script "UploadDeck" deve estar configurado no console PlayFab
/// Estrutura esperada:
/// - deck_index.json: índice com lista de categorias
/// - cartas_[categoria].json: arquivo com lista de perguntas
/// </summary>
public class DeckUploader : MonoBehaviour
{
    private static readonly string DECK_DATA_PATH = Application.streamingAssetsPath + "/../DeckData/";
    private static DeckUploader instance;
    private Coroutine uploadCoroutine;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    /// <summary>
    /// Carrega e faz upload de um arquivo JSON específico via Cloud Script
    /// Deve ser chamado após o login PlayFab estar bem-sucedido
    /// </summary>
    public static void UploadDeckFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("[DeckUploader] ❌ Nome do arquivo não pode estar vazio!");
            return;
        }

        string filePath = Path.Combine(DECK_DATA_PATH, fileName + ".json");

        Debug.Log($"[DeckUploader] 🔍 Procurando arquivo: {filePath}");

        if (!File.Exists(filePath))
        {
            Debug.LogError($"[DeckUploader] ❌ Arquivo NÃO encontrado: {filePath}");
            ListAvailableFiles();
            return;
        }

        string jsonContent = File.ReadAllText(filePath);
        Debug.Log($"[DeckUploader] ✅ Arquivo lido com sucesso ({jsonContent.Length} caracteres)");
        
        UploadViaCloudScript(fileName, jsonContent);
    }

    /// <summary>
    /// Carrega e faz upload do índice de decks
    /// </summary>
    public static void UploadDeckIndex()
    {
        UploadDeckFile("deck_index");
    }

    /// <summary>
    /// Carrega e faz upload de um deck específico por categoria
    /// </summary>
    public static void UploadDeck(string categoria)
    {
        UploadDeckFile($"cartas_{categoria}");
    }

    /// <summary>
    /// Carrega e faz upload de TODOS os decks em uma única operação
    /// Com delay entre uploads para evitar race condition
    /// </summary>
    public static void UploadAllDecks()
    {
        if (!Directory.Exists(DECK_DATA_PATH))
        {
            Debug.LogError($"[DeckUploader] ❌ Diretório não encontrado: {DECK_DATA_PATH}");
            ListAvailableFiles();
            return;
        }

        string[] jsonFiles = Directory.GetFiles(DECK_DATA_PATH, "*.json");

        Debug.Log($"[DeckUploader] 📁 Encontrados {jsonFiles.Length} arquivos JSON em: {DECK_DATA_PATH}");
        
        foreach (string file in jsonFiles)
        {
            Debug.Log($"[DeckUploader]   - {Path.GetFileName(file)}");
        }

        // Garantir que há uma instância para rodar a corrotina
        if (instance == null)
        {
            var go = new GameObject("DeckUploader");
            instance = go.AddComponent<DeckUploader>();
        }

        // Se já há um upload em progresso, cancela
        if (instance.uploadCoroutine != null)
        {
            instance.StopCoroutine(instance.uploadCoroutine);
        }

        instance.uploadCoroutine = instance.StartCoroutine(instance.UploadAllDecksCoroutine(jsonFiles));
    }

    private IEnumerator UploadAllDecksCoroutine(string[] jsonFiles)
    {
        foreach (string filePath in jsonFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            Debug.Log($"[DeckUploader] ⏳ Iniciando upload: {fileName}");
            UploadDeckFile(fileName);
            
            // Aguardar 1 segundo entre cada upload
            yield return new WaitForSeconds(1f);
        }
        
        Debug.Log("[DeckUploader] ✅ Todos os uploads foram iniciados!");
    }

    /// <summary>
    /// Faz upload via Cloud Script (recomendado)
    /// </summary>
    private static void UploadViaCloudScript(string key, string value)
    {
        // Verificar autenticação
        if (!PlayFabSettings.staticPlayer.IsEntityLoggedIn())
        {
            Debug.LogError("[DeckUploader] ❌ Não está autenticado no PlayFab! Faça login primeiro.");
            return;
        }

        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "UploadDeck",
            FunctionParameter = new Dictionary<string, object>
            {
                { "key", key },
                { "value", value }
            },
            GeneratePlayStreamEvent = true
        };

        Debug.Log($"[DeckUploader] ☁️ Enviando '{key}' para Cloud Script...");

        PlayFabClientAPI.ExecuteCloudScript(
            request,
            OnCloudScriptSuccess,
            OnCloudScriptError
        );
    }

    private static void OnCloudScriptSuccess(ExecuteCloudScriptResult result)
    {
        if (result.FunctionResult != null)
        {
            var resultDict = result.FunctionResult as Dictionary<string, object>;
            
            if (resultDict != null && resultDict.ContainsKey("success"))
            {
                bool success = (bool)resultDict["success"];
                
                if (success)
                {
                    string message = resultDict.ContainsKey("message") 
                        ? resultDict["message"].ToString() 
                        : "Upload concluído";
                    
                    Debug.Log($"[DeckUploader] ✅ {message}");
                }
                else
                {
                    string error = resultDict.ContainsKey("error") 
                        ? resultDict["error"].ToString() 
                        : "Erro desconhecido";
                    
                    Debug.LogError($"[DeckUploader] ❌ {error}");
                }
            }
            else
            {
                Debug.LogWarning("[DeckUploader] ⚠️ Resposta inesperada do Cloud Script");
                Debug.LogWarning($"Resultado: {result.FunctionResult}");
            }
        }
        else
        {
            Debug.LogWarning("[DeckUploader] ⚠️ Cloud Script retornou resultado vazio");
        }
    }

    private static void OnCloudScriptError(PlayFabError error)
    {
        Debug.LogError($"[DeckUploader] ❌ Erro ao chamar Cloud Script: {error.GenerateErrorReport()}");
    }

    /// <summary>
    /// Lista todos os arquivos disponíveis no diretório de decks
    /// Útil para debug
    /// </summary>
    private static void ListAvailableFiles()
    {
        Debug.LogWarning("[DeckUploader] 📋 Arquivos disponíveis:");
        
        if (!Directory.Exists(DECK_DATA_PATH))
        {
            Debug.LogError($"[DeckUploader] Diretório não existe: {DECK_DATA_PATH}");
            return;
        }

        string[] files = Directory.GetFiles(DECK_DATA_PATH);
        if (files.Length == 0)
        {
            Debug.LogWarning("[DeckUploader] Nenhum arquivo encontrado");
            return;
        }

        foreach (string file in files)
        {
            FileInfo fileInfo = new FileInfo(file);
            Debug.LogWarning($"  - {fileInfo.Name} ({fileInfo.Length} bytes)");
        }
    }

    /// <summary>
    /// Opção alternativa: Salva dados em Player Data (específico do jogador)
    /// Útil se preferir não usar Cloud Script
    /// </summary>
    public static void UploadDeckToPlayerData(string key, string jsonContent)
    {
        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                { key, jsonContent }
            }
        };

        PlayFabClientAPI.UpdateUserData(request,
            result => Debug.Log($"[DeckUploader] ✅ Deck '{key}' salvo em Player Data com sucesso!"),
            error => Debug.LogError($"[DeckUploader] ❌ Erro ao salvar: {error.GenerateErrorReport()}")
        );
    }
}


