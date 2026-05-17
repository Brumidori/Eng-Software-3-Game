// ============================================================
// MatchFunctions.cs — Azure Functions (CloudScript V2)
// Gerencia ciclo de vida da partida: criação, início de rodadas,
// reconexão, finalização e watchdog de AFK.
//
// Deploy: Azure Functions App apontado no PlayFab Title Settings
// Namespace PlayFab.Plugins.CloudScript (nuget PlayFabAllSDK)
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.Plugins.CloudScript;
using BrainDuel.CloudScript;

namespace BrainDuel.Server
{
    public static class MatchFunctions
    {
        // Chave no Entity Object onde o estado é persistido
        private const string MatchStateKey = "MatchState";
        private const string ObjectCollection = "MatchObjects";

        // ----------------------------------------------------------
        // CreateMatch
        // Chamado pelo primeiro cliente após matchmaking.
        // Idempotente: retorna estado existente se já criado.
        // ----------------------------------------------------------

        [FunctionName("CreateMatch")]
        public static async Task<IActionResult> CreateMatch(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            ILogger log)
        {
            var ctx     = await FunctionContext<CreateMatchRequest>.Create(req);
            var request = ctx.FunctionArgument;

            log.LogInformation($"[CreateMatch] {request.MatchId}");

            // Verifica se já existe
            var existing = await LoadMatchState(request.MatchId);
            if (existing != null && existing.IsActive)
            {
                return new OkObjectResult(new CreateMatchResponse
                {
                    MatchId           = existing.MatchId,
                    NetworkDescriptor = existing.PartyNetworkDescriptor,
                    Success           = true
                });
            }

            // Carrega pool de perguntas combinado dos decks de ambos
            var questionPool = await BuildQuestionPool(request.Player1Id, request.Player2Id);

            // Carrega power-ups equipados
            var p1PowerUp = await GetEquippedPowerUp(request.Player1Id);
            var p2PowerUp = await GetEquippedPowerUp(request.Player2Id);

            var networkDescriptor = $"brainduel_{request.MatchId}"; // gerado pelo Party SDK real

            var state = new ServerMatchState
            {
                MatchId               = request.MatchId,
                Player1Id             = request.Player1Id,
                Player2Id             = request.Player2Id,
                Player1State          = new PlayerMatchState { PlayerId = request.Player1Id, HP = 100, EquippedPowerUp = p1PowerUp, IsConnected = true },
                Player2State          = new PlayerMatchState { PlayerId = request.Player2Id, HP = 100, EquippedPowerUp = p2PowerUp, IsConnected = true },
                CurrentRound          = 0,
                Phase                 = MatchPhase.Initializing,
                QuestionPool          = questionPool,
                IsActive              = true,
                MatchStartTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PartyNetworkDescriptor = networkDescriptor,
                LastProcessedRound    = 0
            };

            await SaveMatchState(state);
            await PlayFabStorageService.AddToActiveIndexAsync(state.MatchId);

            // Agenda início da primeira rodada
            await TriggerStartNextRound(state, 1);

            return new OkObjectResult(new CreateMatchResponse
            {
                MatchId           = state.MatchId,
                NetworkDescriptor = networkDescriptor,
                Success           = true
            });
        }

        // ----------------------------------------------------------
        // StartNextRound
        // Prepara a próxima rodada e faz broadcast de RoundStart.
        // Idempotente: ignorado se rodada já iniciada.
        // ----------------------------------------------------------

        [FunctionName("StartNextRound")]
        public static async Task<IActionResult> StartNextRound(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            ILogger log)
        {
            var ctx     = await FunctionContext<StartNextRoundRequest>.Create(req);
            var request = ctx.FunctionArgument;

            var state = await LoadMatchState(request.MatchId);
            if (state == null || !state.IsActive) return new BadRequestObjectResult("Match não encontrado");

            // Idempotência
            if (state.CurrentRound >= request.RoundNumber)
                return new OkObjectResult("Rodada já iniciada");

            // Fim de partida por número de rodadas
            if (request.RoundNumber > MatchConfig.MaxRounds)
            {
                await FinalizeMatch(state, DetermineWinnerByHP(state), MatchEndReason.RoundsOver);
                return new OkObjectResult("Partida encerrada por rodadas");
            }

            var question = GetQuestionForRound(state, request.RoundNumber);
            if (question == null)
            {
                log.LogError($"[StartNextRound] Pergunta não encontrada para rodada {request.RoundNumber}");
                return new StatusCodeResult(500);
            }

            state.CurrentRound      = request.RoundNumber;
            state.Phase             = MatchPhase.ThemeAndPowerUp;
            state.PhaseStartTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            state.CurrentRoundState = new ServerRoundState
            {
                RoundNumber     = request.RoundNumber,
                QuestionId      = question.QuestionId,
                ThemeId         = question.ThemeId,
                ThemeName       = question.ThemeName,
                CorrectAnswerId = question.CorrectOptionId,
                Player1Action   = new RoundPlayerAction { PlayerId = state.Player1Id },
                Player2Action   = new RoundPlayerAction { PlayerId = state.Player2Id }
            };

            await SaveMatchState(state);

            // Broadcast RoundStart para ambos os clientes via Party
            await BroadcastToMatch(state, "RoundStart", new
            {
                roundNumber      = state.CurrentRound,
                themeId          = question.ThemeId,
                themeName        = question.ThemeName,
                serverTimestampMs = state.PhaseStartTimestampMs,
                themeDurationMs  = MatchConfig.ThemePhaseDurationMs,
                questionDurationMs = MatchConfig.QuestionPhaseDurationMs
            });

            // Agenda transição para Question após ThemePhaseDuration
            // (via Azure Durable Functions ou verificação no ProcessRound)

            log.LogInformation($"[StartNextRound] Rodada {request.RoundNumber} iniciada para {request.MatchId}");
            return new OkObjectResult("OK");
        }

        // ----------------------------------------------------------
        // StartQuestion
        // Chamado pelos clientes após 4s do tema.
        // Idempotente — processa apenas uma vez.
        // ----------------------------------------------------------

        [FunctionName("StartQuestion")]
        public static async Task<IActionResult> StartQuestion(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            ILogger log)
        {
            var ctx  = await FunctionContext<dynamic>.Create(req);
            string matchId     = ctx.FunctionArgument.matchId;
            int    roundNumber = (int)ctx.FunctionArgument.roundNumber;

            var state = await LoadMatchState(matchId);
            if (state == null || state.CurrentRound != roundNumber) return new OkObjectResult("Ignorado");
            if (state.Phase != MatchPhase.ThemeAndPowerUp) return new OkObjectResult("Fase incorreta");

            state.Phase             = MatchPhase.Question;
            state.PhaseStartTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await SaveMatchState(state);

            var question = await LoadQuestionData(state.CurrentRoundState.QuestionId);

            // Determina respostas eliminadas pelo EliminateTwo
            int[] eliminatedIndices = ComputeEliminatedIndices(state, question);

            await BroadcastToMatch(state, "QuestionReveal", new
            {
                questionId        = question.QuestionId,
                questionText      = question.Text,
                answers           = question.Options,
                serverTimestampMs = state.PhaseStartTimestampMs,
                durationMs        = MatchConfig.QuestionPhaseDurationMs,
                eliminatedIndices
            });

            return new OkObjectResult("OK");
        }

        // ----------------------------------------------------------
        // RejoinMatch
        // Reconecta jogador e envia estado completo.
        // ----------------------------------------------------------

        [FunctionName("RejoinMatch")]
        public static async Task<IActionResult> RejoinMatch(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            ILogger log)
        {
            var ctx     = await FunctionContext<RejoinMatchRequest>.Create(req);
            var request = ctx.FunctionArgument;

            var state = await LoadMatchState(request.MatchId);
            if (state == null || !state.IsActive) return new BadRequestObjectResult("Match não encontrado");

            // Marca jogador como reconectado
            var playerState = GetPlayerState(state, request.PlayerId);
            if (playerState != null)
            {
                playerState.IsConnected         = true;
                playerState.ConsecutiveMissedRounds = 0;
            }

            await SaveMatchState(state);

            // Broadcast ReconnectSync apenas para o reconectado
            var question = state.Phase == MatchPhase.Question
                ? await LoadQuestionData(state.CurrentRoundState.QuestionId)
                : null;

            await BroadcastToPlayer(state, request.PlayerId, "ReconnectSync", new
            {
                fullState         = state,
                currentQuestion   = question,
                serverTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            // Notifica oponente que o jogador voltou
            await BroadcastToMatch(state, "OpponentReconnected", new { playerId = request.PlayerId });

            return new OkObjectResult("OK");
        }

        // ----------------------------------------------------------
        // FinalizeMatch
        // ----------------------------------------------------------

        [FunctionName("FinalizeMatch")]
        public static async Task<IActionResult> FinalizeMatchEndpoint(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            ILogger log)
        {
            var ctx  = await FunctionContext<dynamic>.Create(req);
            string matchId  = ctx.FunctionArgument.matchId;
            string winnerId = ctx.FunctionArgument.winnerId;

            var state = await LoadMatchState(matchId);
            if (state == null) return new BadRequestObjectResult("Match não encontrado");

            await FinalizeMatch(state, winnerId, MatchEndReason.HPDepleted);
            return new OkObjectResult("OK");
        }

        // ----------------------------------------------------------
        // AfkWatchdog — Timer Trigger (executa a cada 30s)
        // ----------------------------------------------------------

        [FunctionName("AfkWatchdog")]
        public static async Task AfkWatchdog(
            [TimerTrigger("0 */1 * * * *")] TimerInfo timer, // a cada 1 minuto
            ILogger log)
        {
            // Lista matches ativos e verifica jogadores desconectados há > 30s
            var activeMatches = await LoadAllActiveMatches();

            foreach (var state in activeMatches)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                foreach (var ps in new[] { state.Player1State, state.Player2State })
                {
                    if (!ps.IsConnected && (now - ps.DisconnectedAtMs) > MatchConfig.ReconnectWindowMs)
                    {
                        log.LogWarning($"[AFK] Jogador {ps.PlayerId} abandonou {state.MatchId}");
                        var opponent = ps.PlayerId == state.Player1Id ? state.Player2Id : state.Player1Id;
                        await BroadcastToMatch(state, "OpponentAbandoned", new { playerId = ps.PlayerId });
                        await FinalizeMatch(state, opponent, MatchEndReason.Abandonment);
                        break;
                    }
                }
            }
        }

        // ----------------------------------------------------------
        // Helpers internos
        // ----------------------------------------------------------

        private static async Task FinalizeMatch(ServerMatchState state, string winnerId, MatchEndReason reason)
        {
            state.IsActive  = false;
            state.WinnerId  = winnerId;
            state.EndReason = reason;
            state.Phase     = MatchPhase.MatchEnd;

            await SaveMatchState(state);
            await PlayFabStorageService.RemoveFromActiveIndexAsync(state.MatchId);

            var winnerState = GetPlayerState(state, winnerId);
            var loserState  = GetPlayerState(state, winnerId == state.Player1Id ? state.Player2Id : state.Player1Id);

            await BroadcastToMatch(state, "MatchEnd", new
            {
                winnerId,
                winnerHP         = winnerState?.HP ?? 0,
                loserHP          = loserState?.HP ?? 0,
                reason           = (int)reason,
                totalRoundsPlayed = state.CurrentRound
            });

            // Atualiza estatísticas dos jogadores
            await UpdatePlayerStats(state, winnerId);
        }

        private static string DetermineWinnerByHP(ServerMatchState state)
        {
            if (state.Player1State.HP > state.Player2State.HP) return state.Player1Id;
            if (state.Player2State.HP > state.Player1State.HP) return state.Player2Id;
            return null; // empate
        }

        private static PlayerMatchState GetPlayerState(ServerMatchState state, string playerId) =>
            playerId == state.Player1Id ? state.Player1State : state.Player2State;

        private static QuestionData GetQuestionForRound(ServerMatchState state, int round)
        {
            int idx = round - 1;
            if (idx < 0 || idx >= state.QuestionPool.Count) return null;
            // LoadQuestionData é chamado de forma síncrona aqui via cache
            return LoadQuestionDataSync(state.QuestionPool[idx]);
        }

        private static int[] ComputeEliminatedIndices(ServerMatchState state, QuestionData question)
        {
            // Se o jogador local usou EliminateTwo, calcula 2 índices errados aleatórios
            var result = new List<int>();
            var correctId = state.CurrentRoundState.CorrectAnswerId;
            var wrongIndices = question.Options
                .Select((o, i) => (o, i))
                .Where(x => x.o.Id != correctId)
                .Select(x => x.i)
                .ToList();

            if (wrongIndices.Count >= 2)
            {
                var rng = new Random();
                wrongIndices = wrongIndices.OrderBy(_ => rng.Next()).Take(2).ToList();
                result.AddRange(wrongIndices);
            }
            return result.ToArray();
        }

        private static async Task TriggerStartNextRound(ServerMatchState state, int roundNumber)
        {
            // Em produção: usar Durable Functions para agendar StartNextRound
            // após RoundEndPhaseDurationMs. Por ora, delegado ao cliente.
            await Task.CompletedTask;
        }

        // ----------------------------------------------------------
        // PlayFab Storage — delegado ao PlayFabStorageService
        // ----------------------------------------------------------

        private static async Task SaveMatchState(ServerMatchState state, ILogger log = null)
        {
            await PlayFabStorageService.SaveMatchStateAsync(state, log);
        }

        private static async Task<ServerMatchState> LoadMatchState(string matchId, ILogger log = null)
        {
            return await PlayFabStorageService.LoadMatchStateAsync(matchId, log);
        }

        private static async Task<List<ServerMatchState>> LoadAllActiveMatches(ILogger log = null)
        {
            return await PlayFabStorageService.LoadAllActiveMatchesAsync(log);
        }

        private static async Task<QuestionData> LoadQuestionData(string questionId)
        {
            // PlayFabEconomyAPI.GetItems ou TitleData
            await Task.CompletedTask;
            return null;
        }

        private static QuestionData LoadQuestionDataSync(string questionId) => null; // via cache

        private static async Task<List<string>> BuildQuestionPool(string p1Id, string p2Id)
        {
            // Carrega decks de ambos os jogadores, combina e embaralha
            await Task.CompletedTask;
            return new List<string>();
        }

        private static async Task<PowerUpType> GetEquippedPowerUp(string playerId)
        {
            // Lê do PlayFab Player Data
            await Task.CompletedTask;
            return PowerUpType.None;
        }

        // ----------------------------------------------------------
        // Party broadcast (via PlayFab Party Management API)
        // ----------------------------------------------------------

        private static async Task BroadcastToMatch(ServerMatchState state, string messageType, object payload)
        {
            // PlayFab Party API: enviar mensagem a todos os membros da network
            // PartyNetworkAPI.SendMessage(state.PartyNetworkDescriptor, payload)
            await Task.CompletedTask;
        }

        private static async Task BroadcastToPlayer(ServerMatchState state, string playerId, string messageType, object payload)
        {
            // Envia apenas para o endpoint do player especificado
            await Task.CompletedTask;
        }

        private static async Task UpdatePlayerStats(ServerMatchState state, string winnerId)
        {
            // PlayFabServerAPI.UpdatePlayerStatistics para wins/losses/ELO
            await Task.CompletedTask;
        }

        // ----------------------------------------------------------
        // Constantes espelhadas (sem referência ao Unity)
        // ----------------------------------------------------------
        private static class MatchConfig
        {
            public const int MaxRounds             = 20;
            public const int ThemePhaseDurationMs  = 4_000;
            public const int QuestionPhaseDurationMs = 20_000;
            public const long ReconnectWindowMs    = 30_000;
        }
    }
}
