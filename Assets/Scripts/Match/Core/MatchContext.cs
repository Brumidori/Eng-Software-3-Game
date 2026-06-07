// ============================================================
// MatchContext.cs — estado local da partida no cliente.
// É a "memória de trabalho" da state machine.  Não contém
// lógica de regras — apenas dados e acesso controlado.
// ============================================================
using System;
using UnityEngine;
using BrainDuel.Match;
using BrainDuel.Network;

namespace BrainDuel.Match.Core
{
    public class MatchContext
    {
        // ----------------------------------------------------------
        // Identidade
        // ----------------------------------------------------------
        public string MatchId       { get; set; }
        public string LocalPlayerId { get; set; }
        public string OpponentId    { get; set; }

        private string _localDisplayName    = "Você";
        private string _opponentDisplayName = "Adversário";

        public string LocalDisplayName
        {
            get
            {
                var s = LocalPlayer?.DisplayName;
                return !string.IsNullOrEmpty(s) ? s : _localDisplayName;
            }
            set => _localDisplayName = value;
        }

        public string OpponentDisplayName
        {
            get
            {
                var s = OpponentPlayer?.DisplayName;
                return !string.IsNullOrEmpty(s) ? s : _opponentDisplayName;
            }
            set => _opponentDisplayName = value;
        }

        public int LocalLevel    { get; set; } = 1;
        public int OpponentLevel => OpponentPlayer?.Level ?? 0;

        // ----------------------------------------------------------
        // Estado autoritativo (sincronizado com servidor)
        // ----------------------------------------------------------
        public ServerMatchState ServerState     { get; set; }
        public QuestionData     CurrentQuestion { get; set; }
        // Pool completo da partida (20 perguntas), recebido no PlayerReady e cacheado localmente.
        // Elimina chamadas de rede por rodada — cada rodada usa QuestionPool[roundNumber-1].
        public QuestionData[]   QuestionPool    { get; set; }

        // ----------------------------------------------------------
        // Estado da rodada atual (local — para UI)
        // ----------------------------------------------------------
        public int    CurrentRound     => ServerState?.CurrentRound ?? 0;
        public int    LocalHP          => LocalPlayer?.HP ?? MatchConfig.InitialHP;
        public int    OpponentHP       => OpponentPlayer?.HP ?? MatchConfig.InitialHP;

        public PlayerMatchState LocalPlayer    =>
            ServerState?.Player1Id == LocalPlayerId ? ServerState.Player1State : ServerState?.Player2State;
        public PlayerMatchState OpponentPlayer =>
            ServerState?.Player1Id == LocalPlayerId ? ServerState.Player2State : ServerState?.Player1State;

        // ----------------------------------------------------------
        // Inputs da rodada atual (pendentes de envio / confirmação)
        // ----------------------------------------------------------
        public string     SelectedAnswerId      { get; private set; }
        public long       AnswerTimestampMs      { get; private set; }
        public bool       HasAnsweredThisRound   { get; private set; }
        public PowerUpType PendingPowerUp        { get; private set; }
        public bool       HasActivatedPowerUpThisRound { get; private set; }

        // ----------------------------------------------------------
        // Indicadores de presença do oponente
        // ----------------------------------------------------------
        public bool OpponentAnsweredThisRound  { get; set; }
        public long OpponentAnswerTimestampMs  { get; set; }
        public bool IsOpponentConnected        { get; set; } = true;
        public long OpponentDisconnectedAtMs   { get; set; }

        // ----------------------------------------------------------
        // Timer (sincronizado com servidor)
        // ----------------------------------------------------------
        public long PhaseStartServerMs { get; set; }
        public int  PhaseDurationMs    { get; set; }

        public float RemainingSeconds
        {
            get
            {
                long elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - PhaseStartServerMs;
                float remaining = (PhaseDurationMs - elapsed) / 1000f;
                return Mathf.Max(0f, remaining);
            }
        }

        // ----------------------------------------------------------
        // Power-up do jogador local
        // ----------------------------------------------------------
        public PowerUpType EquippedPowerUp => LocalPlayer?.EquippedPowerUp ?? PowerUpType.None;
        public bool        CanUsePowerUp   => LocalPlayer != null
                                              && !LocalPlayer.HasUsedPowerUp
                                              && LocalPlayer.EquippedPowerUp != PowerUpType.None;

        // ----------------------------------------------------------
        // Resultado da última rodada (para Reveal phase)
        // ----------------------------------------------------------
        public RoundResultPayload LastRoundResult { get; set; }

        // ----------------------------------------------------------
        // Histórico de rounds (para streak visual, etc.)
        // ----------------------------------------------------------
        public int LocalStreak    => LocalPlayer?.Streak ?? 0;
        public int OpponentStreak => OpponentPlayer?.Streak ?? 0;

        // ----------------------------------------------------------
        // Métodos de mutação (round-local)
        // ----------------------------------------------------------

        public void SubmitAnswer(string answerId)
        {
            if (HasAnsweredThisRound) return;
            SelectedAnswerId    = answerId;
            AnswerTimestampMs   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            HasAnsweredThisRound = true;
        }

        public void ActivatePowerUp(PowerUpType type)
        {
            if (HasActivatedPowerUpThisRound) return;
            PendingPowerUp              = type;
            HasActivatedPowerUpThisRound = true;
        }

        public void ResetRoundInputs()
        {
            SelectedAnswerId             = null;
            AnswerTimestampMs            = 0;
            HasAnsweredThisRound         = false;
            PendingPowerUp               = PowerUpType.None;
            HasActivatedPowerUpThisRound = false;
            OpponentAnsweredThisRound    = false;
            OpponentAnswerTimestampMs    = 0;
        }

        // ----------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------

        // Stub = partida local contra bot (Party SDK não necessário).
        // Real = dois jogadores reais sincronizados via CloudScript (suportado em WebGL).
        public bool IsStubMode => !BrainDuel.Match.Core.MatchSessionData.IsRealMatch;

        public bool IsLocalPlayer(string playerId) => playerId == LocalPlayerId;

        public RoundPlayerResult GetLocalResult(RoundResultPayload payload) =>
            payload.Player1Result.PlayerId == LocalPlayerId
                ? payload.Player1Result
                : payload.Player2Result;

        public RoundPlayerResult GetOpponentResult(RoundResultPayload payload) =>
            payload.Player1Result.PlayerId == LocalPlayerId
                ? payload.Player2Result
                : payload.Player1Result;
    }
}
