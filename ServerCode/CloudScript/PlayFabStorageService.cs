// ============================================================
// PlayFabStorageService.cs — camada de persistência do
// estado de partida via PlayFab Title Internal Data.
//
// Estratégia de armazenamento:
//   - Cada partida ocupa uma chave: "match_{matchId}"
//   - Valor: JSON do ServerMatchState (< 10 MB — limite PlayFab)
//   - Um índice separado ("active_matches") lista os IDs ativos
//     para que o AfkWatchdog possa varrer todas as partidas.
//
// Autenticação:
//   - Variáveis de ambiente do Azure Function App:
//       PLAYFAB_TITLE_ID
//       PLAYFAB_DEV_SECRET_KEY
//
// Para produção de alta escala, substitua por Azure Cosmos DB
// ou Table Storage (o contrato do serviço não muda).
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.ServerModels;
using BrainDuel.CloudScript;

namespace BrainDuel.Server
{
    public static class PlayFabStorageService
    {
        // ----------------------------------------------------------
        // Prefixos de chave no Title Internal Data
        // ----------------------------------------------------------
        private const string MatchKeyPrefix     = "match_";
        private const string ActiveMatchesIndex = "active_matches_index";

        // ----------------------------------------------------------
        // Inicialização (chamada no início de cada Azure Function)
        // ----------------------------------------------------------

        public static void EnsureInitialized()
        {
            PlayFabSettings.staticSettings.TitleId =
                Environment.GetEnvironmentVariable("PLAYFAB_TITLE_ID")
                ?? throw new InvalidOperationException("PLAYFAB_TITLE_ID não configurado.");

            PlayFabSettings.staticSettings.DeveloperSecretKey =
                Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY")
                ?? throw new InvalidOperationException("PLAYFAB_DEV_SECRET_KEY não configurado.");
        }

        // ----------------------------------------------------------
        // Salvar estado da partida
        // ----------------------------------------------------------

        public static async Task SaveMatchStateAsync(ServerMatchState state, ILogger log = null)
        {
            EnsureInitialized();

            var json = JsonConvert.SerializeObject(state, Formatting.None);
            var key  = MatchKeyPrefix + state.MatchId;

            var request = new SetTitleDataRequest
            {
                Key   = key,
                Value = json
            };

            var result = await PlayFabServerAPI.SetTitleInternalDataAsync(request);

            if (result.Error != null)
                throw new Exception($"[Storage] SaveMatchState falhou: {result.Error.ErrorMessage}");

            log?.LogInformation($"[Storage] Match {state.MatchId} salvo ({json.Length} bytes)");
        }

        // ----------------------------------------------------------
        // Carregar estado de uma partida específica
        // ----------------------------------------------------------

        public static async Task<ServerMatchState> LoadMatchStateAsync(string matchId, ILogger log = null)
        {
            EnsureInitialized();

            var key = MatchKeyPrefix + matchId;

            var result = await PlayFabServerAPI.GetTitleInternalDataAsync(new GetTitleDataRequest
            {
                Keys = new List<string> { key }
            });

            if (result.Error != null)
                throw new Exception($"[Storage] LoadMatchState falhou: {result.Error.ErrorMessage}");

            if (!result.Result.Data.TryGetValue(key, out var json) || string.IsNullOrEmpty(json))
            {
                log?.LogWarning($"[Storage] Match {matchId} não encontrado");
                return null;
            }

            return JsonConvert.DeserializeObject<ServerMatchState>(json);
        }

        // ----------------------------------------------------------
        // Deletar estado após fim da partida (limpeza)
        // ----------------------------------------------------------

        public static async Task DeleteMatchStateAsync(string matchId, ILogger log = null)
        {
            EnsureInitialized();

            // Setar Value como null remove a chave no PlayFab
            await PlayFabServerAPI.SetTitleInternalDataAsync(new SetTitleDataRequest
            {
                Key   = MatchKeyPrefix + matchId,
                Value = null
            });

            await RemoveFromActiveIndexAsync(matchId);
            log?.LogInformation($"[Storage] Match {matchId} removido");
        }

        // ----------------------------------------------------------
        // Carregar todas as partidas ativas (usado pelo AfkWatchdog)
        // ----------------------------------------------------------

        public static async Task<List<ServerMatchState>> LoadAllActiveMatchesAsync(ILogger log = null)
        {
            EnsureInitialized();

            // Carrega o índice de IDs ativos
            var indexResult = await PlayFabServerAPI.GetTitleInternalDataAsync(new GetTitleDataRequest
            {
                Keys = new List<string> { ActiveMatchesIndex }
            });

            if (indexResult.Error != null || !indexResult.Result.Data.TryGetValue(ActiveMatchesIndex, out var indexJson))
                return new List<ServerMatchState>();

            var activeIds = JsonConvert.DeserializeObject<List<string>>(indexJson)
                            ?? new List<string>();

            if (activeIds.Count == 0) return new List<ServerMatchState>();

            // Carrega todas as chaves de uma vez (batch GetTitleInternalData)
            var keys = activeIds.Select(id => MatchKeyPrefix + id).ToList();
            var dataResult = await PlayFabServerAPI.GetTitleInternalDataAsync(new GetTitleDataRequest
            {
                Keys = keys
            });

            if (dataResult.Error != null)
                throw new Exception($"[Storage] LoadAllActiveMatches falhou: {dataResult.Error.ErrorMessage}");

            var states = new List<ServerMatchState>();
            foreach (var kvp in dataResult.Result.Data)
            {
                if (string.IsNullOrEmpty(kvp.Value)) continue;
                try
                {
                    var state = JsonConvert.DeserializeObject<ServerMatchState>(kvp.Value);
                    if (state != null && state.IsActive) states.Add(state);
                }
                catch (Exception ex)
                {
                    log?.LogWarning($"[Storage] Falha ao desserializar {kvp.Key}: {ex.Message}");
                }
            }

            return states;
        }

        // ----------------------------------------------------------
        // Registro de partidas ativas (índice separado)
        // ----------------------------------------------------------

        public static async Task AddToActiveIndexAsync(string matchId)
        {
            EnsureInitialized();
            var ids = await LoadActiveIndexAsync();
            if (!ids.Contains(matchId))
            {
                ids.Add(matchId);
                await SaveActiveIndexAsync(ids);
            }
        }

        public static async Task RemoveFromActiveIndexAsync(string matchId)
        {
            EnsureInitialized();
            var ids = await LoadActiveIndexAsync();
            if (ids.Remove(matchId))
                await SaveActiveIndexAsync(ids);
        }

        // ----------------------------------------------------------
        // Helpers internos do índice
        // ----------------------------------------------------------

        private static async Task<List<string>> LoadActiveIndexAsync()
        {
            var result = await PlayFabServerAPI.GetTitleInternalDataAsync(new GetTitleDataRequest
            {
                Keys = new List<string> { ActiveMatchesIndex }
            });

            if (result.Error != null || !result.Result.Data.TryGetValue(ActiveMatchesIndex, out var json))
                return new List<string>();

            return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
        }

        private static async Task SaveActiveIndexAsync(List<string> ids)
        {
            await PlayFabServerAPI.SetTitleInternalDataAsync(new SetTitleDataRequest
            {
                Key   = ActiveMatchesIndex,
                Value = JsonConvert.SerializeObject(ids)
            });
        }
    }
}
