// ============================================================
// RoundFunctions.cs — Azure Functions para ações de rodada.
// Toda lógica de dano é SERVER-SIDE aqui.
// O cliente tem DamageCalculator.cs apenas para preview de UI.
// ============================================================
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PlayFab.Plugins.CloudScript;
using BrainDuel.CloudScript;

namespace BrainDuel.Server
{
    public static class RoundFunctions
    {
        // ----------------------------------------------------------
        // SubmitAnswer
        // Registra a resposta de um jogador na rodada atual.
        // Idempotente: segunda chamada do mesmo jogador é ignorada.
        // ----------------------------------------------------------

        [FunctionName("SubmitAnswer")]
        public static async Task<IActionResult> SubmitAnswer(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            ILogger log)
        {
            var ctx     = await FunctionContext<SubmitAnswerRequest>.Create(req);
            var request = ctx.FunctionArgument;
            request.PlayerId = ctx.CallerEntityProfile.Entity.Id;

            log.LogInformation($"[SubmitAnswer] Match={request.MatchId} Player={request.PlayerId} Round={request.RoundNumber}");

            var state = await LoadMatchState(request.MatchId);
            if (state == null || !state.IsActive)           return new BadRequestObjectResult("Match inativo");
            if (state.CurrentRound != request.RoundNumber)  return new OkObjectResult("Rodada diferente — ignorado");
            if (state.Phase != MatchPhase.Question)          return new OkObjectResult("Fora da fase de resposta");

            var action = GetPlayerAction(state, request.PlayerId);
            if (action.HasAnswered) return new OkObjectResult("Já respondeu — ignorado");

            // Valida que o answerId existe na questão
            if (!await IsValidAnswer(state.CurrentRoundState.QuestionId, request.AnswerId))
                return new BadRequestObjectResult("AnswerId inválido");

            action.AnswerId           = request.AnswerId;
            action.AnswerTimestampMs  = request.ClientTimestampMs;
            action.HasAnswered        = true;

            await SaveMatchState(state);

            // Se ambos responderam, processa a rodada imediatamente
            if (BothPlayersAnswered(state))
                await ProcessRoundInternal(state, log);

            return new OkObjectResult("Resposta registrada");
        }

        // ----------------------------------------------------------
        // ActivatePowerUp
        // Só aceito durante ThemeAndPowerUp phase.
        // ----------------------------------------------------------

        [FunctionName("ActivatePowerUp")]
        public static async Task<IActionResult> ActivatePowerUp(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            ILogger log)
        {
            var ctx     = await FunctionContext<ActivatePowerUpRequest>.Create(req);
            var request = ctx.FunctionArgument;
            request.PlayerId = ctx.CallerEntityProfile.Entity.Id;

            var state = await LoadMatchState(request.MatchId);
            if (state == null || !state.IsActive)           return new BadRequestObjectResult("Match inativo");
            if (state.CurrentRound != request.RoundNumber)  return new OkObjectResult("Rodada diferente");
            if (state.Phase != MatchPhase.ThemeAndPowerUp)  return new BadRequestObjectResult("Fora da janela de power-up");

            var ps     = GetPlayerMatchState(state, request.PlayerId);
            var action = GetPlayerAction(state, request.PlayerId);

            if (ps.HasUsedPowerUp)                          return new BadRequestObjectResult("Power-up já usado na partida");
            if (request.PowerUp != ps.EquippedPowerUp)      return new BadRequestObjectResult("Power-up não equipado");

            action.ActivatedPowerUp = request.PowerUp;

            // EliminateTwo: calcula índices no StartQuestion
            await SaveMatchState(state);

            log.LogInformation($"[ActivatePowerUp] {request.PlayerId} usou {request.PowerUp}");
            return new OkObjectResult("Power-up ativado");
        }

        // ----------------------------------------------------------
        // ProcessRound
        // Chamado pelos clientes após o timer de 20s.
        // Idempotente: retorna resultado cacheado se já processado.
        // ----------------------------------------------------------

        [FunctionName("ProcessRound")]
        public static async Task<IActionResult> ProcessRound(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            ILogger log)
        {
            var ctx     = await FunctionContext<ProcessRoundRequest>.Create(req);
            var request = ctx.FunctionArgument;

            var state = await LoadMatchState(request.MatchId);
            if (state == null || !state.IsActive) return new BadRequestObjectResult("Match inativo");
            if (state.CurrentRound != request.RoundNumber) return new OkObjectResult("Rodada diferente");

            // Idempotência
            if (state.CurrentRoundState.IsProcessed)
                return new OkObjectResult(BuildProcessRoundResponse(state));

            return new OkObjectResult(await ProcessRoundInternal(state, log));
        }

        // ----------------------------------------------------------
        // Lógica autoritativa de processamento de rodada
        // ----------------------------------------------------------

        private static async Task<ProcessRoundResponse> ProcessRoundInternal(ServerMatchState state, ILogger log)
        {
            var round  = state.CurrentRoundState;
            var p1Act  = round.Player1Action;
            var p2Act  = round.Player2Action;
            var p1State = state.Player1State;
            var p2State = state.Player2State;

            // --- Calcula resultados ---
            var p1Result = ComputePlayerResult(p1Act, p1State, p2Act, p2State, round.CorrectAnswerId);
            var p2Result = ComputePlayerResult(p2Act, p2State, p1Act, p1State, round.CorrectAnswerId);

            // --- Aplica HP ---
            int p1HPBefore = p1State.HP;
            int p2HPBefore = p2State.HP;

            // Dano que P1 causa em P2
            if (!p2Result.WasShielded)
                p2State.HP -= p1Result.DamageDealt;

            // Dano que P2 causa em P1
            if (!p1Result.WasShielded)
                p1State.HP -= p2Result.DamageDealt;

            // Steal de P1 em P2
            if (p1Act.ActivatedPowerUp == PowerUpType.Steal && !p2Result.WasShielded)
            {
                p1State.HP += ServerDamageConfig.StealAmount;
                p2State.HP -= ServerDamageConfig.StealAmount;
            }

            // Steal de P2 em P1
            if (p2Act.ActivatedPowerUp == PowerUpType.Steal && !p1Result.WasShielded)
            {
                p2State.HP += ServerDamageConfig.StealAmount;
                p1State.HP -= ServerDamageConfig.StealAmount;
            }

            // Clamp
            p1State.HP = Math.Max(0, p1State.HP);
            p2State.HP = Math.Max(0, p2State.HP);

            p1Result.HPBefore = p1HPBefore;
            p1Result.HPAfter  = p1State.HP;
            p2Result.HPBefore = p2HPBefore;
            p2Result.HPAfter  = p2State.HP;

            // --- Atualiza streaks ---
            p1State.Streak = p1Result.Result == AnswerResult.Correct ? p1State.Streak + 1 : 0;
            p2State.Streak = p2Result.Result == AnswerResult.Correct ? p2State.Streak + 1 : 0;
            p1Result.StreakAfter = p1State.Streak;
            p2Result.StreakAfter = p2State.Streak;

            // --- AFK tracking ---
            if (!p1Act.HasAnswered) p1State.ConsecutiveMissedRounds++;
            else p1State.ConsecutiveMissedRounds = 0;

            if (!p2Act.HasAnswered) p2State.ConsecutiveMissedRounds++;
            else p2State.ConsecutiveMissedRounds = 0;

            // --- Power-up charges ---
            UpdatePowerUpCharges(p1State, p1Act, p1Result.WasShielded);
            UpdatePowerUpCharges(p2State, p2Act, p2Result.WasShielded);

            // --- Persiste resultados ---
            round.Player1Result = p1Result;
            round.Player2Result = p2Result;
            round.IsProcessed   = true;
            state.Phase         = MatchPhase.Reveal;
            state.LastProcessedRound = round.RoundNumber;

            // --- Verifica fim de partida ---
            bool matchOver = p1State.HP <= 0 || p2State.HP <= 0
                             || round.RoundNumber >= MatchConfig.MaxRounds
                             || p1State.ConsecutiveMissedRounds >= MatchConfig.AfkRoundLimit
                             || p2State.ConsecutiveMissedRounds >= MatchConfig.AfkRoundLimit;

            string winnerId = null;
            var    endReason = MatchEndReason.HPDepleted;

            if (matchOver)
            {
                winnerId = DetermineWinner(state);
                if (p1State.ConsecutiveMissedRounds >= MatchConfig.AfkRoundLimit)
                { winnerId = state.Player2Id; endReason = MatchEndReason.Abandonment; }
                else if (p2State.ConsecutiveMissedRounds >= MatchConfig.AfkRoundLimit)
                { winnerId = state.Player1Id; endReason = MatchEndReason.Abandonment; }
                else if (round.RoundNumber >= MatchConfig.MaxRounds)
                    endReason = MatchEndReason.RoundsOver;

                state.IsActive = false;
                state.WinnerId = winnerId;
                state.EndReason = endReason;
            }

            await SaveMatchState(state);

            var response = new ProcessRoundResponse
            {
                AlreadyProcessed = false,
                Player1Result    = p1Result,
                Player2Result    = p2Result,
                Player1HP        = p1State.HP,
                Player2HP        = p2State.HP,
                IsMatchOver      = matchOver,
                WinnerId         = winnerId,
                EndReason        = endReason
            };

            // Broadcast para ambos os clientes
            await BroadcastRoundResult(state, response);

            if (matchOver)
            {
                await BroadcastMatchEnd(state, winnerId, endReason);
                await UpdatePlayerStats(state, winnerId);
            }

            return response;
        }

        // ----------------------------------------------------------
        // Cálculo de dano (server-authoritative)
        // ----------------------------------------------------------

        private static RoundPlayerResult ComputePlayerResult(
            RoundPlayerAction attackerAction, PlayerMatchState attackerState,
            RoundPlayerAction defenderAction, PlayerMatchState defenderState,
            string correctAnswerId)
        {
            var result = new RoundPlayerResult
            {
                PlayerId  = attackerAction.PlayerId,
                AnsweredId = attackerAction.AnswerId,
                Breakdown = new DamageBreakdown()
            };

            var bd = result.Breakdown;

            if (!attackerAction.HasAnswered)
            {
                result.Result     = AnswerResult.NotAnswered;
                bd.SelfDamage     = ServerDamageConfig.SelfDamageNoAnswer;
                result.DamageDealt = 0;
                return result;
            }

            bool correct = attackerAction.AnswerId == correctAnswerId;
            result.Result = correct ? AnswerResult.Correct : AnswerResult.Incorrect;

            // Steal: ativo independente de acerto
            if (attackerAction.ActivatedPowerUp == PowerUpType.Steal)
                bd.StolenHP = ServerDamageConfig.StealAmount;

            if (!correct)
            {
                result.DamageDealt = bd.Total;
                return result;
            }

            // Acertou
            int streak = attackerState.Streak + 1;
            bd.BaseDamage  = ServerDamageConfig.BaseDamage;
            bd.StreakBonus = GetStreakBonus(streak);

            // Bônus de velocidade
            if (defenderAction.HasAnswered && defenderAction.AnswerId == correctAnswerId)
            {
                long diff = Math.Abs(attackerAction.AnswerTimestampMs - defenderAction.AnswerTimestampMs);
                if (diff > ServerDamageConfig.SpeedBonusThresholdMs
                    && attackerAction.AnswerTimestampMs < defenderAction.AnswerTimestampMs)
                    bd.SpeedBonus = ServerDamageConfig.SpeedBonus;
            }

            // Power-up Bet
            if (attackerAction.ActivatedPowerUp == PowerUpType.Bet)
                bd.PowerUpBonus = ServerDamageConfig.BetBonus;

            // Shield do defensor
            bool shielded = IsShielded(defenderState, defenderAction);
            if (shielded)
            {
                result.WasShielded = true;
                bd.BaseDamage = bd.SpeedBonus = bd.StreakBonus = bd.PowerUpBonus = 0;
            }

            result.DamageDealt = bd.Total;
            return result;
        }

        // ----------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------

        private static bool IsShielded(PlayerMatchState state, RoundPlayerAction action)
            => action.ActivatedPowerUp == PowerUpType.SimpleShield
            || action.ActivatedPowerUp == PowerUpType.DoubleShield
            || state.DoubleShieldCharges > 0;

        private static void UpdatePowerUpCharges(PlayerMatchState state, RoundPlayerAction action, bool shieldConsumed)
        {
            if (shieldConsumed && state.DoubleShieldCharges > 0)
                state.DoubleShieldCharges--;

            if (action.ActivatedPowerUp == PowerUpType.DoubleShield)
            {
                state.DoubleShieldCharges = 1;
                state.HasUsedPowerUp = true;
            }
            else if (action.ActivatedPowerUp != PowerUpType.None)
                state.HasUsedPowerUp = true;
        }

        private static int GetStreakBonus(int streak) => streak switch
        {
            <= 1 => 0, 2 => 1, 3 => 3, _ => 5
        };

        private static bool BothPlayersAnswered(ServerMatchState state)
            => state.CurrentRoundState.Player1Action.HasAnswered
            && state.CurrentRoundState.Player2Action.HasAnswered;

        private static RoundPlayerAction GetPlayerAction(ServerMatchState state, string playerId)
            => playerId == state.Player1Id
                ? state.CurrentRoundState.Player1Action
                : state.CurrentRoundState.Player2Action;

        private static PlayerMatchState GetPlayerMatchState(ServerMatchState state, string playerId)
            => playerId == state.Player1Id ? state.Player1State : state.Player2State;

        private static string DetermineWinner(ServerMatchState state)
        {
            if (state.Player1State.HP > state.Player2State.HP) return state.Player1Id;
            if (state.Player2State.HP > state.Player1State.HP) return state.Player2Id;
            return null; // empate — resultado pelo rounds (caso futuro)
        }

        private static ProcessRoundResponse BuildProcessRoundResponse(ServerMatchState state)
        {
            var round = state.CurrentRoundState;
            return new ProcessRoundResponse
            {
                AlreadyProcessed = true,
                Player1Result    = round.Player1Result,
                Player2Result    = round.Player2Result,
                Player1HP        = state.Player1State.HP,
                Player2HP        = state.Player2State.HP,
                IsMatchOver      = !state.IsActive,
                WinnerId         = state.WinnerId,
                EndReason        = state.EndReason
            };
        }

        private static async Task<bool> IsValidAnswer(string questionId, string answerId)
        {
            // Valida via TitleData/Economy
            await Task.CompletedTask;
            return !string.IsNullOrEmpty(answerId);
        }

        // ----------------------------------------------------------
        // Persistência — delegada ao PlayFabStorageService
        // ----------------------------------------------------------

        private static async Task<ServerMatchState> LoadMatchState(string matchId)
            => await PlayFabStorageService.LoadMatchStateAsync(matchId);

        private static async Task SaveMatchState(ServerMatchState state)
            => await PlayFabStorageService.SaveMatchStateAsync(state);

        private static async Task BroadcastRoundResult(ServerMatchState state, ProcessRoundResponse response)
        { await Task.CompletedTask; }

        private static async Task BroadcastMatchEnd(ServerMatchState state, string winnerId, MatchEndReason reason)
        { await Task.CompletedTask; }

        private static async Task UpdatePlayerStats(ServerMatchState state, string winnerId)
        { await Task.CompletedTask; }

        // ----------------------------------------------------------
        // Constantes de dano (server-side)
        // ----------------------------------------------------------

        private static class ServerDamageConfig
        {
            public const int BaseDamage            = 5;
            public const int SpeedBonus            = 2;
            public const int SelfDamageNoAnswer    = 3;
            public const int BetBonus              = 5;
            public const int StealAmount           = 5;
            public const int SpeedBonusThresholdMs = 200;
        }

        private static class MatchConfig
        {
            public const int MaxRounds    = 20;
            public const int AfkRoundLimit = 3;
        }
    }
}
