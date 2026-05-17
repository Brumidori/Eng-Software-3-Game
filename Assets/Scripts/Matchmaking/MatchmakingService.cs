// ============================================================
// MatchmakingService.cs — gerencia a fila de matchmaking
// público via PlayFab Matchmaking 2.0.
//
// Fases da busca:
//   0-15 s   → filtra por nível parecido + latência
//   15-35 s  → ignora nível, prioriza conectividade
//   > 35 s   → desempate por tempo em fila
//
// Ao encontrar partida:
//   → chama CloudScript CreateMatch para criar estado no servidor
//   → cria Party network e distribui descriptor ao oponente
//   → dispara OnMatchFound com todos os dados necessários
// ============================================================
using System;
using System.Collections;
using UnityEngine;
using PlayFab;
using PlayFab.MultiplayerModels;
using BrainDuel.Match;
using BrainDuel.Match.Core;
using BrainDuel.Match.Network;

namespace BrainDuel.Matchmaking
{
    public enum MatchmakingState
    {
        Idle, Searching, MatchFound, Cancelled, Failed
    }

    public class MatchFoundData
    {
        public string MatchId;
        public string OpponentId;
        public string OpponentDisplayName;
        public string PartyNetworkDescriptor;
        public PowerUpType LocalEquippedPowerUp;
    }

    public class MatchmakingService : MonoBehaviour
    {
        public static MatchmakingService Instance { get; private set; }

        [Header("Configuração PlayFab")]
        [SerializeField] private string _queueName         = "BrainDuelPublicQueue";
        [SerializeField] private float  _pollInterval      = 2f;
        [SerializeField] private float  _maxWaitSeconds    = 120f;

        public MatchmakingState State  { get; private set; } = MatchmakingState.Idle;

        // Eventos
        public event Action<MatchFoundData> OnMatchFound;
        public event Action<string>         OnFailed;
        public event Action                 OnCancelled;
        public event Action<float>          OnElapsedUpdated;

        private string   _ticketId;
        private float    _elapsed;
        private Coroutine _pollRoutine;

        // ----------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ----------------------------------------------------------
        // API pública
        // ----------------------------------------------------------

        public void StartMatchmaking(int playerLevel, string deckId, PowerUpType equippedPowerUp)
        {
            if (State == MatchmakingState.Searching) return;

            State    = MatchmakingState.Searching;
            _elapsed = 0f;

            CreateTicket(playerLevel, deckId, equippedPowerUp);
        }

        public void Cancel()
        {
            if (State != MatchmakingState.Searching) return;

            StopPoll();
            State = MatchmakingState.Cancelled;

            if (!string.IsNullOrEmpty(_ticketId))
                CancelTicketSilently(_ticketId);

            OnCancelled?.Invoke();
        }

        // ----------------------------------------------------------
        // Criação do ticket
        // ----------------------------------------------------------

        private void CreateTicket(int level, string deckId, PowerUpType powerUp)
        {
            var req = new CreateMatchmakingTicketRequest
            {
                QueueName          = _queueName,
                GiveUpAfterSeconds = (int)_maxWaitSeconds,
                Creator = new MatchmakingPlayer
                {
                    Entity = new EntityKey
                    {
                        Id   = PlayFabSettings.staticPlayer.EntityId,
                        Type = PlayFabSettings.staticPlayer.EntityType
                    },
                    Attributes = new MatchmakingPlayerAttributes
                    {
                        DataObject = new
                        {
                            Level       = level,
                            DeckId      = deckId,
                            PowerUp     = (int)powerUp,
                            Region      = DetectRegion(),
                            QueuedAtMs  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        }
                    }
                }
            };

            PlayFabMultiplayerAPI.CreateMatchmakingTicket(req,
                result =>
                {
                    _ticketId    = result.TicketId;
                    _pollRoutine = StartCoroutine(PollLoop(powerUp));
                    Debug.Log($"[Matchmaking] Ticket criado: {_ticketId}");
                },
                error =>
                {
                    State = MatchmakingState.Failed;
                    OnFailed?.Invoke(error.ErrorMessage);
                });
        }

        // ----------------------------------------------------------
        // Polling
        // ----------------------------------------------------------

        private IEnumerator PollLoop(PowerUpType powerUp)
        {
            while (State == MatchmakingState.Searching && _elapsed < _maxWaitSeconds)
            {
                yield return new WaitForSeconds(_pollInterval);
                _elapsed += _pollInterval;
                OnElapsedUpdated?.Invoke(_elapsed);

                var tcs = new System.Threading.Tasks.TaskCompletionSource<GetMatchmakingTicketResult>();
                PlayFabMultiplayerAPI.GetMatchmakingTicket(
                    new GetMatchmakingTicketRequest
                    {
                        QueueName = _queueName,
                        TicketId  = _ticketId
                    },
                    tcs.SetResult,
                    err => tcs.SetException(new Exception(err.ErrorMessage)));

                yield return new WaitUntil(() => tcs.Task.IsCompleted);

                if (tcs.Task.IsFaulted)
                {
                    Debug.LogWarning($"[Matchmaking] Poll erro: {tcs.Task.Exception?.Message}");
                    continue;
                }

                var ticket = tcs.Task.Result;
                Debug.Log($"[Matchmaking] Status: {ticket.Status}");

                if (ticket.Status == "Matched")
                {
                    StopPoll();
                    FetchAndInitMatch(ticket.MatchId, powerUp);
                    yield break;
                }

                if (ticket.Status is "Cancelled" or "Failed")
                {
                    StopPoll();
                    State = MatchmakingState.Failed;
                    OnFailed?.Invoke($"Ticket encerrado: {ticket.Status}");
                    yield break;
                }
            }

            if (_elapsed >= _maxWaitSeconds)
            {
                StopPoll();
                State = MatchmakingState.Failed;
                OnFailed?.Invoke("Timeout no matchmaking");
            }
        }

        // ----------------------------------------------------------
        // Após match encontrado
        // ----------------------------------------------------------

        private void FetchAndInitMatch(string matchId, PowerUpType powerUp)
        {
            PlayFabMultiplayerAPI.GetMatch(
                new GetMatchRequest
                {
                    QueueName             = _queueName,
                    MatchId               = matchId,
                    ReturnMemberAttributes = true
                },
                result =>
                {
                    var myId     = PlayFabSettings.staticPlayer.EntityId;
                    var opponent = result.Members.Find(m => m.Entity.Id != myId);

                    var data = new MatchFoundData
                    {
                        MatchId                = matchId,
                        OpponentId             = opponent?.Entity?.Id ?? string.Empty,
                        OpponentDisplayName    = ExtractDisplayName(opponent),
                        LocalEquippedPowerUp   = powerUp
                    };

                    // Inicializa partida no servidor e cria Party network
                    InitMatchOnServer(data);
                },
                error =>
                {
                    State = MatchmakingState.Failed;
                    OnFailed?.Invoke(error.ErrorMessage);
                });
        }

        private void InitMatchOnServer(MatchFoundData data)
        {
            PlayFabClientAPI.ExecuteCloudScript(
                new PlayFab.ClientModels.ExecuteCloudScriptRequest
                {
                    FunctionName      = "CreateMatch",
                    FunctionParameter = new
                    {
                        matchId    = data.MatchId,
                        player1Id  = PlayFabSettings.staticPlayer.EntityId,
                        player2Id  = data.OpponentId
                    }
                },
                result =>
                {
                    var payload    = result.FunctionResult as System.Collections.Generic.Dictionary<string, object>;
                    var descriptor = payload != null && payload.ContainsKey("networkDescriptor")
                                     ? payload["networkDescriptor"]?.ToString()
                                     : null;

                    // Ambos entram na Party network (o server distribuiu o descriptor)
                    PartyNetworkManager.Instance.JoinNetwork(descriptor,
                        onJoined: () =>
                        {
                            State = MatchmakingState.MatchFound;
                            data.PartyNetworkDescriptor = descriptor;
                            OnMatchFound?.Invoke(data);
                        },
                        onError: err =>
                        {
                            State = MatchmakingState.Failed;
                            OnFailed?.Invoke($"Party join falhou: {err}");
                        });
                },
                error =>
                {
                    State = MatchmakingState.Failed;
                    OnFailed?.Invoke($"CreateMatch falhou: {error.ErrorMessage}");
                });
        }

        // ----------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------

        private void StopPoll()
        {
            if (_pollRoutine != null) StopCoroutine(_pollRoutine);
            _pollRoutine = null;
        }

        private void CancelTicketSilently(string ticketId)
        {
            PlayFabMultiplayerAPI.CancelMatchmakingTicket(
                new CancelMatchmakingTicketRequest { QueueName = _queueName, TicketId = ticketId },
                _ => { }, err => Debug.LogWarning($"[Matchmaking] Cancel ticket: {err.ErrorMessage}"));
        }

        private string DetectRegion() => "EastUS"; // TODO: PlayFab QoS latency measurement

        private string ExtractDisplayName(MatchmakingPlayerWithTeamAssignment player)
        {
            try
            {
                var attrs = player?.Attributes?.DataObject as System.Collections.Generic.Dictionary<string, object>;
                return attrs != null && attrs.ContainsKey("DisplayName")
                    ? attrs["DisplayName"]?.ToString() ?? "Oponente"
                    : "Oponente";
            }
            catch { return "Oponente"; }
        }
    }
}
