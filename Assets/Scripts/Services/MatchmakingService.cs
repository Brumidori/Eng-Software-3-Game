using System;
using System.Collections;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.MultiplayerModels;
using UnityEngine;
using MultiplayerEntityKey = PlayFab.MultiplayerModels.EntityKey;

public class MatchmakingService : MonoBehaviour
{
    public enum MatchmakingState
    {
        Idle,
        Searching,
        Matched,
        TimedOut,
        Cancelled,
        Failed
    }

    private sealed class MatchmakingTestUser
    {
        public string customId;
        public PlayFabClientInstanceAPI clientApi;
        public PlayFabMultiplayerInstanceAPI multiplayerApi;
        public PlayFabAuthenticationContext authContext;
        public MultiplayerEntityKey entity;
        public string ticketId;
        public string status;
        public string matchId;
    }

    public static MatchmakingService Instance { get; private set; }

    public static event Action<MatchmakingState> OnStateChanged;
    public static event Action<string> OnMatchFound;
    public static event Action<PlayFabError> OnMatchmakingFailed;

    public MatchmakingState CurrentState { get; private set; } = MatchmakingState.Idle;

    private string queueName;
    private int timeoutSeconds;
    private float pollIntervalSeconds;
    private MatchmakingTestUser userA;
    private MatchmakingTestUser userB;
    private Coroutine matchmakingRoutine;
    private bool cancellationRequested;
    private bool createMissingUsersForTest;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartTwoUserMatchmaking(string queue, string userAId, string userBId, int timeout, float pollInterval, bool allowCreateMissingUsers = false)
    {
        if (matchmakingRoutine != null)
        {
            Debug.LogWarning("[MatchmakingService] Ja existe um teste de matchmaking em andamento.");
            return;
        }

        if (string.IsNullOrWhiteSpace(queue) || string.IsNullOrWhiteSpace(userAId) || string.IsNullOrWhiteSpace(userBId))
        {
            Debug.LogError("[MatchmakingService] Queue e usuarios devem estar preenchidos.");
            SetState(MatchmakingState.Failed);
            return;
        }

        queueName = queue.Trim();
        timeoutSeconds = Mathf.Max(5, timeout);
        pollIntervalSeconds = Mathf.Max(0.5f, pollInterval);
        createMissingUsersForTest = allowCreateMissingUsers;
        userA = new MatchmakingTestUser { customId = userAId.Trim() };
        userB = new MatchmakingTestUser { customId = userBId.Trim() };
        cancellationRequested = false;

        string titleId = PlayFabSettings.staticSettings != null ? PlayFabSettings.staticSettings.TitleId : "<desconhecido>";
        Debug.Log($"[MatchmakingService] Iniciando teste para queue '{queueName}' com usuarios '{userA.customId}' e '{userB.customId}'. Timeout: {timeoutSeconds}s. TitleId='{titleId}' CreateMissingUsers={createMissingUsersForTest}");
        SetState(MatchmakingState.Searching);
        matchmakingRoutine = StartCoroutine(RunTwoUserMatchmaking());
    }

    public void CancelCurrentSearch()
    {
        if (matchmakingRoutine == null)
        {
            Debug.LogWarning("[MatchmakingService] Nao ha busca ativa para cancelar.");
            return;
        }

        cancellationRequested = true;
        Debug.Log("[MatchmakingService] Cancelamento solicitado.");
    }

    public string GetDiagnosticsSummary()
    {
        return $"Queue='{queueName}' | Estado={CurrentState} | UserA='{userA?.customId}' Ticket='{userA?.ticketId}' Status='{userA?.status}' Match='{userA?.matchId}' | UserB='{userB?.customId}' Ticket='{userB?.ticketId}' Status='{userB?.status}' Match='{userB?.matchId}'";
    }

    private IEnumerator RunTwoUserMatchmaking()
    {
        yield return LoginUser(userA);
        if (CurrentState == MatchmakingState.Failed)
        {
            FinishRoutine();
            yield break;
        }

        yield return LoginUser(userB);
        if (CurrentState == MatchmakingState.Failed)
        {
            FinishRoutine();
            yield break;
        }

        yield return CancelAllTicketsForUser(userA);
        yield return CancelAllTicketsForUser(userB);

        yield return CreateTicket(userA);
        if (CurrentState == MatchmakingState.Failed)
        {
            FinishRoutine();
            yield break;
        }

        yield return CreateTicket(userB);
        if (CurrentState == MatchmakingState.Failed)
        {
            yield return CancelActiveTickets();
            FinishRoutine();
            yield break;
        }

        var deadline = Time.time + timeoutSeconds;
        while (Time.time < deadline && !cancellationRequested)
        {
            yield return PollTicket(userA);
            yield return PollTicket(userB);

            if (AreUsersMatchedTogether())
            {
                SetState(MatchmakingState.Matched);
                Debug.Log($"[MatchmakingService] Match encontrado! MatchId compartilhado: {userA.matchId}");
                OnMatchFound?.Invoke(userA.matchId);
                yield return GetMatchDetails(userA.matchId);
                FinishRoutine();
                yield break;
            }

            yield return new WaitForSeconds(pollIntervalSeconds);
        }

        if (cancellationRequested)
        {
            yield return CancelActiveTickets();
            SetState(MatchmakingState.Cancelled);
            FinishRoutine();
            yield break;
        }

        yield return CancelActiveTickets();
        SetState(MatchmakingState.TimedOut);
        Debug.LogWarning($"[MatchmakingService] Busca encerrada por timeout apos {timeoutSeconds}s sem match compartilhado.");
        FinishRoutine();
    }

    private IEnumerator LoginUser(MatchmakingTestUser user)
    {
        user.clientApi = new PlayFabClientInstanceAPI();

        LoginResult loginResult = null;
        PlayFabError loginError = null;
        yield return LoginWithCustomId(user.clientApi, user.customId, false, result => loginResult = result, error => loginError = error);

        if (loginError != null && loginError.Error == PlayFabErrorCode.AccountNotFound && createMissingUsersForTest)
        {
            Debug.LogWarning($"[MatchmakingService] Usuario '{user.customId}' nao existe no Title atual. Tentando CreateAccount=true para ambiente de teste.");
            loginResult = null;
            loginError = null;
            yield return LoginWithCustomId(user.clientApi, user.customId, true, result => loginResult = result, error => loginError = error);
        }

        if (loginError != null)
        {
            string titleId = PlayFabSettings.staticSettings != null ? PlayFabSettings.staticSettings.TitleId : "<desconhecido>";
            HandlePlayFabFailure($"Falha no login do usuario '{user.customId}' (TitleId='{titleId}', CreateMissingUsers={createMissingUsersForTest})", loginError);
            yield break;
        }

        user.authContext = loginResult.AuthenticationContext ?? user.clientApi.authenticationContext;
        user.multiplayerApi = new PlayFabMultiplayerInstanceAPI(user.authContext);
        user.entity = new MultiplayerEntityKey
        {
            Id = user.authContext.EntityId,
            Type = user.authContext.EntityType
        };

        Debug.Log($"[MatchmakingService] Usuario '{user.customId}' logado. Entity: {user.entity.Type}/{user.entity.Id}");
    }

    private static IEnumerator LoginWithCustomId(PlayFabClientInstanceAPI api, string customId, bool createAccount, Action<LoginResult> onSuccess, Action<PlayFabError> onError)
    {
        bool done = false;

        api.LoginWithCustomID(new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = createAccount
        },
        result =>
        {
            onSuccess?.Invoke(result);
            done = true;
        },
        error =>
        {
            onError?.Invoke(error);
            done = true;
        });

        while (!done)
        {
            yield return null;
        }
    }

    private IEnumerator CancelAllTicketsForUser(MatchmakingTestUser user)
    {
        bool done = false;

        user.multiplayerApi.CancelAllMatchmakingTicketsForPlayer(new CancelAllMatchmakingTicketsForPlayerRequest
        {
            QueueName = queueName,
            Entity = user.entity
        },
        _ => { done = true; },
        error =>
        {
            done = true;
            Debug.LogWarning($"[MatchmakingService] Nao foi possivel limpar tickets antigos do usuario '{user.customId}': {error.GenerateErrorReport()}");
        });

        while (!done)
        {
            yield return null;
        }
    }

    private IEnumerator CreateTicket(MatchmakingTestUser user)
    {
        bool done = false;
        CreateMatchmakingTicketResult ticketResult = null;
        PlayFabError ticketError = null;

        user.multiplayerApi.CreateMatchmakingTicket(new CreateMatchmakingTicketRequest
        {
            QueueName = queueName,
            GiveUpAfterSeconds = timeoutSeconds,
            Creator = new MatchmakingPlayer
            {
                Entity = user.entity
            }
        },
        result =>
        {
            ticketResult = result;
            done = true;
        },
        error =>
        {
            ticketError = error;
            done = true;
        });

        while (!done)
        {
            yield return null;
        }

        if (ticketError != null)
        {
            HandlePlayFabFailure($"Falha ao criar ticket para '{user.customId}'", ticketError);
            yield break;
        }

        user.ticketId = ticketResult.TicketId;
        user.status = "WaitingForMatch";
        user.matchId = null;
        Debug.Log($"[MatchmakingService] Ticket criado para '{user.customId}': {user.ticketId}");
    }

    private IEnumerator PollTicket(MatchmakingTestUser user)
    {
        if (string.IsNullOrWhiteSpace(user.ticketId))
        {
            yield break;
        }

        bool done = false;
        GetMatchmakingTicketResult ticketResult = null;
        PlayFabError ticketError = null;

        user.multiplayerApi.GetMatchmakingTicket(new GetMatchmakingTicketRequest
        {
            QueueName = queueName,
            TicketId = user.ticketId
        },
        result =>
        {
            ticketResult = result;
            done = true;
        },
        error =>
        {
            ticketError = error;
            done = true;
        });

        while (!done)
        {
            yield return null;
        }

        if (ticketError != null)
        {
            Debug.LogWarning($"[MatchmakingService] Falha ao consultar ticket de '{user.customId}': {ticketError.GenerateErrorReport()}");
            yield break;
        }

        var previousStatus = user.status;
        user.status = ticketResult.Status;
        user.matchId = ticketResult.MatchId;

        if (!string.Equals(previousStatus, user.status, StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(user.matchId))
        {
            Debug.Log($"[MatchmakingService] Ticket '{user.ticketId}' de '{user.customId}' -> Status={user.status}, MatchId={(string.IsNullOrWhiteSpace(user.matchId) ? "(vazio)" : user.matchId)}");
        }
    }

    private bool AreUsersMatchedTogether()
    {
        return !string.IsNullOrWhiteSpace(userA?.matchId)
            && !string.IsNullOrWhiteSpace(userB?.matchId)
            && string.Equals(userA.matchId, userB.matchId, StringComparison.Ordinal);
    }

    private IEnumerator GetMatchDetails(string matchId)
    {
        bool done = false;
        GetMatchResult matchResult = null;
        PlayFabError matchError = null;

        userA.multiplayerApi.GetMatch(new GetMatchRequest
        {
            QueueName = queueName,
            MatchId = matchId,
            ReturnMemberAttributes = false
        },
        result =>
        {
            matchResult = result;
            done = true;
        },
        error =>
        {
            matchError = error;
            done = true;
        });

        while (!done)
        {
            yield return null;
        }

        if (matchError != null)
        {
            Debug.LogWarning($"[MatchmakingService] Match encontrado, mas falhou GetMatch: {matchError.GenerateErrorReport()}");
            yield break;
        }

        var memberCount = matchResult.Members == null ? 0 : matchResult.Members.Count;
        Debug.Log($"[MatchmakingService] Detalhes da partida {matchResult.MatchId}: membros={memberCount}, arranjo='{matchResult.ArrangementString}'");

        if (matchResult.Members == null)
        {
            yield break;
        }

        foreach (var member in matchResult.Members)
        {
            var entityType = member.Entity == null ? "?" : member.Entity.Type;
            var entityId = member.Entity == null ? "?" : member.Entity.Id;
            Debug.Log($"[MatchmakingService] Membro: {entityType}/{entityId} Team={member.TeamId}");
        }
    }

    private IEnumerator CancelActiveTickets()
    {
        yield return CancelTicketIfNeeded(userA);
        yield return CancelTicketIfNeeded(userB);
    }

    private IEnumerator CancelTicketIfNeeded(MatchmakingTestUser user)
    {
        if (user == null || string.IsNullOrWhiteSpace(user.ticketId) || user.multiplayerApi == null)
        {
            yield break;
        }

        bool done = false;
        user.multiplayerApi.CancelMatchmakingTicket(new CancelMatchmakingTicketRequest
        {
            QueueName = queueName,
            TicketId = user.ticketId
        },
        _ =>
        {
            done = true;
            Debug.Log($"[MatchmakingService] Ticket cancelado para '{user.customId}': {user.ticketId}");
        },
        error =>
        {
            done = true;
            Debug.LogWarning($"[MatchmakingService] Cancelamento do ticket '{user.ticketId}' de '{user.customId}' retornou: {error.GenerateErrorReport()}");
        });

        while (!done)
        {
            yield return null;
        }
    }

    private void HandlePlayFabFailure(string context, PlayFabError error)
    {
        Debug.LogError($"[MatchmakingService] {context}: {error.GenerateErrorReport()}");
        SetState(MatchmakingState.Failed);
        OnMatchmakingFailed?.Invoke(error);
    }

    private void SetState(MatchmakingState state)
    {
        CurrentState = state;
        OnStateChanged?.Invoke(state);
    }

    private void FinishRoutine()
    {
        matchmakingRoutine = null;
        cancellationRequested = false;
    }

    private void OnDisable()
    {
        if (matchmakingRoutine != null)
        {
            StopCoroutine(matchmakingRoutine);
            matchmakingRoutine = null;
        }
    }
}
