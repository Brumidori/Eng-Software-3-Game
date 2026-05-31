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
using System.Collections;
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
        public event Action<RoundStartPayload>   OnRoundStarted;
        public event Action<QuestionRevealPayload> OnQuestionRevealed;

        // ----------------------------------------------------------
        // Estado
        // ----------------------------------------------------------
        public MatchContext    Context  { get; private set; }
        public MatchPhase      Phase    { get; private set; } = MatchPhase.Initializing;

        private BaseMatchState                         _currentState;
        private Dictionary<MatchPhase, BaseMatchState> _states;
        private ReconnectionManager                    _reconnectionManager;
        private Coroutine                              _stubConductorCoroutine;

        // ----------------------------------------------------------
        // Inicialização
        // ----------------------------------------------------------

        // Awake: apenas cria o contexto (antes de qualquer Start())
        private void Awake()
        {
            if (Context != null) return;

            Context = new MatchContext
            {
                MatchId          = MatchSessionData.MatchId          ?? string.Empty,
                LocalPlayerId    = MatchSessionData.LocalPlayerId
                                   ?? PlayFab.PlayFabSettings.staticPlayer?.EntityId
                                   ?? string.Empty,
                LocalDisplayName = MatchSessionData.LocalDisplayName,
                LocalLevel       = MatchSessionData.LocalLevel,
            };
        }

        // Start: finaliza setup quando todos os Awakes (inclusive PartyNetworkManager) já rodaram
        private void Start()
        {
            if (_states == null)
            {
                _states = new Dictionary<MatchPhase, BaseMatchState>
                {
                    [MatchPhase.ThemeAndPowerUp] = new ThemeAndPowerUpState(Context, this),
                    [MatchPhase.Question]        = new QuestionState(Context, this),
                    [MatchPhase.Reveal]          = new RevealState(Context, this),
                    [MatchPhase.RoundEnd]        = new RoundEndState(Context, this),
                    [MatchPhase.MatchEnd]        = new MatchEndState(Context, this),
                };
            }
            _reconnectionManager = GetComponent<ReconnectionManager>();
            SubscribeToNetworkEvents();
            StartCoroutine(IniciarPartida());
        }

        // ----------------------------------------------------------
        // Arranque da partida — conecta ao Party e aguarda RoundStart
        // ----------------------------------------------------------

        private IEnumerator IniciarPartida()
        {
            var party = PartyNetworkManager.Instance;
            if (party == null)
            {
                var go = new GameObject("PartyNetworkManager");
                party = go.AddComponent<PartyNetworkManager>();
                // Awake() runs synchronously; give Start() one frame
                yield return null;
                // Re-subscribe now that the instance exists
                SubscribeToNetworkEvents();
            }

            bool joinDone  = false;
            bool joinError = false;

            party.JoinNetwork(Context.MatchId,
                onJoined: ()  => joinDone  = true,
                onError:  _   => { joinDone = true; joinError = true; });

            while (!joinDone) yield return null;

            if (joinError)
            {
                Debug.LogError("[Match] Falha ao entrar na rede Party");
                yield break;
            }

            // Modo stub: cria ServerState mínimo para que HP / round funcionem na UI
            if (party.IsStubMode && Context.ServerState == null)
            {
                Context.ServerState = new ServerMatchState
                {
                    MatchId      = Context.MatchId,
                    Player1Id    = Context.LocalPlayerId,
                    Player2Id    = "oponente_stub",
                    CurrentRound = 1,
                    Phase        = MatchPhase.ThemeAndPowerUp,
                    IsActive     = true,
                    Player1State = new PlayerMatchState
                    {
                        PlayerId    = Context.LocalPlayerId,
                        DisplayName = Context.LocalDisplayName,
                        Level       = Context.LocalLevel,
                        HP          = MatchConfig.InitialHP,
                        IsConnected = true,
                    },
                    Player2State = new PlayerMatchState
                    {
                        PlayerId    = "oponente_stub",
                        DisplayName = "Adversário Teste",
                        Level       = 1,
                        HP          = MatchConfig.InitialHP,
                        IsConnected = true,
                    },
                };
            }

            if (party.IsStubMode)
            {
                // Modo stub: pula chamada ao servidor e dispara RoundStart localmente
                HandleRoundStart(new RoundStartPayload
                {
                    RoundNumber        = 1,
                    ThemeId            = "stub",
                    ThemeName          = "Tema de Teste",
                    ServerTimestampMs  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ThemeDurationMs    = MatchConfig.ThemePhaseDurationMs,
                    QuestionDurationMs = MatchConfig.QuestionPhaseDurationMs,
                });
            }
            else
            {
                bool roundStarted = false;
                CloudScriptClient.Call("StartNextRound",
                    new { matchId = Context.MatchId, roundNumber = 1 },
                    onSuccess: _ => roundStarted = true,
                    onError:   _ => roundStarted = true);

                float waited = 0f;
                while (!roundStarted && waited < 3f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
            }
        }

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

                var party = PartyNetworkManager.Instance;
                if (party != null && party.IsStubMode)
                {
                    if (_stubConductorCoroutine != null)
                        StopCoroutine(_stubConductorCoroutine);
                    _stubConductorCoroutine = StartCoroutine(StubConductor(newPhase));
                }
            }
            else
            {
                Debug.LogError($"[Match] Estado não registrado: {newPhase}");
            }
        }

        // ----------------------------------------------------------
        // Stub conductor — simula broadcasts do servidor em modo stub
        // ----------------------------------------------------------

        private IEnumerator StubConductor(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.ThemeAndPowerUp:
                {
                    yield return new WaitForSeconds(MatchConfig.ThemePhaseDurationMs / 1000f + 0.1f);
                    if (Phase != MatchPhase.ThemeAndPowerUp) yield break;
                    HandleQuestionReveal(new QuestionRevealPayload
                    {
                        QuestionId        = "stub_q",
                        QuestionText      = "Qual é a capital do Brasil?",
                        Answers           = new[]
                        {
                            new AnswerOption { Id = "A", Text = "Brasília"       },
                            new AnswerOption { Id = "B", Text = "São Paulo"      },
                            new AnswerOption { Id = "C", Text = "Rio de Janeiro" },
                            new AnswerOption { Id = "D", Text = "Salvador"       },
                        },
                        ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        DurationMs        = MatchConfig.QuestionPhaseDurationMs,
                    });
                    break;
                }

                case MatchPhase.Question:
                {
                    yield return new WaitForSeconds(MatchConfig.QuestionPhaseDurationMs / 1000f + 0.1f);
                    if (Phase != MatchPhase.Question) yield break;

                    int round      = Context.CurrentRound;
                    int localHP    = Context.LocalHP;
                    int opponentHP = Context.OpponentHP;
                    bool answered  = Context.HasAnsweredThisRound;
                    int dmg        = answered ? DamageConfig.BaseDamage : 0;
                    int newOppHP   = Mathf.Max(0, opponentHP - dmg);
                    bool matchOver = newOppHP <= 0 || round >= MatchConfig.MaxRounds;

                    HandleRoundResult(new RoundResultPayload
                    {
                        RoundNumber   = round,
                        Player1Result = new RoundPlayerResult
                        {
                            PlayerId    = Context.LocalPlayerId,
                            Result      = answered ? AnswerResult.Correct : AnswerResult.NotAnswered,
                            AnsweredId  = Context.SelectedAnswerId,
                            DamageDealt = dmg,
                            HPBefore    = localHP,
                            HPAfter     = localHP,
                            WasShielded = false,
                            StreakAfter = answered ? Context.LocalStreak + 1 : 0,
                            Breakdown   = new DamageBreakdown { BaseDamage = dmg },
                        },
                        Player2Result = new RoundPlayerResult
                        {
                            PlayerId    = Context.ServerState?.Player2Id ?? "oponente_stub",
                            Result      = AnswerResult.NotAnswered,
                            AnsweredId  = null,
                            DamageDealt = 0,
                            HPBefore    = opponentHP,
                            HPAfter     = newOppHP,
                            WasShielded = false,
                            StreakAfter = 0,
                            Breakdown   = new DamageBreakdown(),
                        },
                        Player1HP   = localHP,
                        Player2HP   = newOppHP,
                        IsMatchOver = matchOver,
                        WinnerId    = matchOver ? Context.LocalPlayerId : null,
                    });
                    break;
                }

                case MatchPhase.RoundEnd:
                {
                    yield return new WaitForSeconds(MatchConfig.RoundEndPhaseDurationMs / 1000f + 0.1f);
                    if (Phase != MatchPhase.RoundEnd) yield break;

                    int nextRound = (Context.ServerState?.CurrentRound ?? 1) + 1;
                    if (Context.ServerState != null)
                        Context.ServerState.CurrentRound = nextRound;

                    HandleRoundStart(new RoundStartPayload
                    {
                        RoundNumber        = nextRound,
                        ThemeId            = "stub",
                        ThemeName          = $"Tema de Teste (Round {nextRound})",
                        ServerTimestampMs  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        ThemeDurationMs    = MatchConfig.ThemePhaseDurationMs,
                        QuestionDurationMs = MatchConfig.QuestionPhaseDurationMs,
                    });
                    break;
                }
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
            OnRoundStarted?.Invoke(p);
            TransitionTo(MatchPhase.ThemeAndPowerUp);
        }

        private void HandleQuestionReveal(QuestionRevealPayload p)
        {
            Context.PhaseStartServerMs = p.ServerTimestampMs;
            Context.PhaseDurationMs    = p.DurationMs;
            OnQuestionRevealed?.Invoke(p);
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
