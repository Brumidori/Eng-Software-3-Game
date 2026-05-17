// ============================================================
// MatchStateMachine.cs — orquestra os estados da partida no
// cliente.  Recebe eventos da PartyNetworkManager e do
// CloudScript e delega para o estado ativo.
//
// Fluxo por rodada:
//   ThemeAndPowerUp (4s) → Question (20s) → Reveal (3s) → RoundEnd (1.5s)
//   → [next round ou MatchEnd]
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using BrainDuel.Match;
using BrainDuel.Match.States;
using BrainDuel.Match.Network;
using BrainDuel.Network;

namespace BrainDuel.Match.Core
{
    public class MatchStateMachine : MonoBehaviour
    {
        // ----------------------------------------------------------
        // Eventos para a UI
        // ----------------------------------------------------------
        public event Action<MatchPhase>          OnPhaseChanged;
        public event Action<int, int>            OnHPUpdated;         // (localHP, opponentHP)
        public event Action<string>              OnOpponentConnected;
        public event Action<string>              OnOpponentDisconnected;
        public event Action<MatchEndPayload>     OnMatchEnded;
        public event Action<RoundResultPayload>  OnRoundResultReceived;

        // ----------------------------------------------------------
        // Estado
        // ----------------------------------------------------------
        public MatchContext    Context  { get; private set; }
        public MatchPhase      Phase    { get; private set; } = MatchPhase.Initializing;

        private BaseMatchState                         _currentState;
        private Dictionary<MatchPhase, BaseMatchState> _states;
        private ReconnectionManager                    _reconnectionManager;

        // ----------------------------------------------------------
        // Inicialização
        // ----------------------------------------------------------

        public void Initialize(MatchContext context)
        {
            Context = context;

            _states = new Dictionary<MatchPhase, BaseMatchState>
            {
                [MatchPhase.ThemeAndPowerUp] = new ThemeAndPowerUpState(context, this),
                [MatchPhase.Question]        = new QuestionState(context, this),
                [MatchPhase.Reveal]          = new RevealState(context, this),
                [MatchPhase.RoundEnd]        = new RoundEndState(context, this),
                [MatchPhase.MatchEnd]        = new MatchEndState(context, this),
            };

            _reconnectionManager = GetComponent<ReconnectionManager>();
            SubscribeToNetworkEvents();
        }

        private void Update()
        {
            _currentState?.OnUpdate(Time.deltaTime);
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
        }

        // ----------------------------------------------------------
        // Transição de fases
        // ----------------------------------------------------------

        public void TransitionTo(MatchPhase newPhase)
        {
            if (Phase == newPhase) return;

            _currentState?.OnExit();
            Phase = newPhase;

            if (_states.TryGetValue(newPhase, out var next))
            {
                _currentState = next;
                _currentState.OnEnter();
                OnPhaseChanged?.Invoke(newPhase);
                Debug.Log($"[Match] → {newPhase} | Round {Context.CurrentRound}/{MatchConfig.MaxRounds}");
            }
            else
            {
                Debug.LogError($"[Match] Estado não registrado: {newPhase}");
            }
        }

        // ----------------------------------------------------------
        // Input do jogador (chamado pela UI)
        // ----------------------------------------------------------

        public void SubmitAnswer(string answerId)
        {
            if (Phase != MatchPhase.Question) return;
            if (Context.HasAnsweredThisRound) return;

            Context.SubmitAnswer(answerId);

            // Envia ao servidor via CloudScript (autoritativo)
            CloudScriptClient.Call("SubmitAnswer", new SubmitAnswerRequest
            {
                MatchId          = Context.MatchId,
                RoundNumber      = Context.CurrentRound,
                AnswerId         = answerId,
                ClientTimestampMs = Context.AnswerTimestampMs
            }, onSuccess: _ =>
            {
                // Notifica oponente via Party (sem revelar resposta)
                PartyNetworkManager.Instance?.Broadcast(MessageType.OpponentAnswered,
                    new OpponentAnsweredPayload
                    {
                        PlayerId    = Context.LocalPlayerId,
                        TimestampMs = Context.AnswerTimestampMs
                    });
            });
        }

        public void ActivatePowerUp(PowerUpType type)
        {
            if (Phase != MatchPhase.ThemeAndPowerUp) return;
            if (!Context.CanUsePowerUp) return;

            Context.ActivatePowerUp(type);

            CloudScriptClient.Call("ActivatePowerUp", new ActivatePowerUpRequest
            {
                MatchId     = Context.MatchId,
                RoundNumber = Context.CurrentRound,
                PowerUp     = type
            }, onSuccess: _ =>
            {
                PartyNetworkManager.Instance?.Broadcast(MessageType.PowerUpActivated,
                    new PowerUpActivatedPayload
                    {
                        PlayerId = Context.LocalPlayerId,
                        PowerUp  = type
                    });
            });
        }

        // ----------------------------------------------------------
        // Handlers de rede (roteados da PartyNetworkManager)
        // ----------------------------------------------------------

        private void SubscribeToNetworkEvents()
        {
            var party = PartyNetworkManager.Instance;
            if (party == null) return;

            party.OnRoundStart          += HandleRoundStart;
            party.OnQuestionReveal      += HandleQuestionReveal;
            party.OnOpponentAnswered    += HandleOpponentAnswered;
            party.OnRoundResult         += HandleRoundResult;
            party.OnMatchEnd            += HandleMatchEnd;
            party.OnPowerUpActivated    += HandlePowerUpActivated;
            party.OnReconnectSync       += HandleReconnectSync;
            party.OnOpponentDisconnected += HandleOpponentDisconnected;
            party.OnOpponentReconnected  += HandleOpponentReconnected;
            party.OnOpponentAbandoned    += HandleOpponentAbandoned;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            var party = PartyNetworkManager.Instance;
            if (party == null) return;

            party.OnRoundStart          -= HandleRoundStart;
            party.OnQuestionReveal      -= HandleQuestionReveal;
            party.OnOpponentAnswered    -= HandleOpponentAnswered;
            party.OnRoundResult         -= HandleRoundResult;
            party.OnMatchEnd            -= HandleMatchEnd;
            party.OnPowerUpActivated    -= HandlePowerUpActivated;
            party.OnReconnectSync       -= HandleReconnectSync;
            party.OnOpponentDisconnected -= HandleOpponentDisconnected;
            party.OnOpponentReconnected  -= HandleOpponentReconnected;
            party.OnOpponentAbandoned    -= HandleOpponentAbandoned;
        }

        private void HandleRoundStart(RoundStartPayload p)
        {
            Context.ResetRoundInputs();
            Context.PhaseStartServerMs = p.ServerTimestampMs;
            Context.PhaseDurationMs    = p.ThemeDurationMs;
            TransitionTo(MatchPhase.ThemeAndPowerUp);
        }

        private void HandleQuestionReveal(QuestionRevealPayload p)
        {
            Context.PhaseStartServerMs = p.ServerTimestampMs;
            Context.PhaseDurationMs    = p.DurationMs;
            TransitionTo(MatchPhase.Question);
        }

        private void HandleOpponentAnswered(OpponentAnsweredPayload p)
        {
            Context.OpponentAnsweredThisRound   = true;
            Context.OpponentAnswerTimestampMs   = p.TimestampMs;
        }

        private void HandleRoundResult(RoundResultPayload p)
        {
            Context.LastRoundResult = p;

            // Atualiza HP local a partir do resultado autoritativo
            var localResult    = Context.GetLocalResult(p);
            var opponentResult = Context.GetOpponentResult(p);

            if (Context.ServerState != null)
            {
                Context.LocalPlayer.HP    = localResult.HPAfter;
                Context.OpponentPlayer.HP = opponentResult.HPAfter;
            }

            OnHPUpdated?.Invoke(Context.LocalHP, Context.OpponentHP);
            OnRoundResultReceived?.Invoke(p);

            TransitionTo(MatchPhase.Reveal);
        }

        private void HandleMatchEnd(MatchEndPayload p)
        {
            Context.LastRoundResult = null;
            OnMatchEnded?.Invoke(p);
            TransitionTo(MatchPhase.MatchEnd);
        }

        private void HandlePowerUpActivated(PowerUpActivatedPayload p)
        {
            // Repassado para o estado ativo (ex.: QuestionState aplica EliminateTwo na UI)
            (_currentState as QuestionState)?.OnPowerUpActivated(p);
        }

        private void HandleReconnectSync(ReconnectSyncPayload p)
        {
            Context.ServerState      = p.FullState;
            Context.CurrentQuestion  = p.CurrentQuestion;
            _reconnectionManager?.OnSyncReceived(p);
            // Restaura fase correta
            TransitionTo(p.FullState.Phase);
        }

        private void HandleOpponentDisconnected(string playerId)
        {
            Context.IsOpponentConnected    = false;
            Context.OpponentDisconnectedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            OnOpponentDisconnected?.Invoke(playerId);
        }

        private void HandleOpponentReconnected(string playerId)
        {
            Context.IsOpponentConnected = true;
            OnOpponentConnected?.Invoke(playerId);
        }

        private void HandleOpponentAbandoned(string playerId)
        {
            // Servidor declarará vitória — aguarda MatchEnd
        }
    }
}
