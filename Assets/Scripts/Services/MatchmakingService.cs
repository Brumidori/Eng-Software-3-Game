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

    public enum TestUserLoginType { CustomId, EmailPassword }

    private sealed class MatchmakingTestUser
    {
        public TestUserLoginType loginType;
        public string customId;
        public string email;
        public string password;
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
    private MatchmakingTestUser singleUser;
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

    public void StartSinglePlayerMatchmaking(string queue, int timeout = 60, float pollInterval = 3f)
    {
        if (matchmakingRoutine != null)
        {
            StopCoroutine(matchmakingRoutine);
            matchmakingRoutine = null;
        }

        if (string.IsNullOrWhiteSpace(queue))
        {
            Debug.LogError("[MatchmakingService] Queue deve estar preenchida.");
            SetState(MatchmakingState.Failed);
            return;
        }

        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            Debug.LogError("[MatchmakingService] Nenhum jogador logado.");
            SetState(MatchmakingState.Failed);
            OnMatchmakingFailed?.Invoke(null);
            return;
        }

        queueName           = queue.Trim();
        timeoutSeconds      = Mathf.Max(5, timeout);
        pollIntervalSeconds = Mathf.Max(0.5f, pollInterval);
        cancellationRequested = false;

        var authContext = PlayFabSettings.staticPlayer;
        singleUser = new MatchmakingTestUser
        {
            authContext   = authContext,
            multiplayerApi = new PlayFabMultiplayerInstanceAPI(authContext),
            entity = new MultiplayerEntityKey
            {
                Id   = authContext.EntityId,
                Type = authContext.EntityType
            }
        };

        Debug.Log($"[MatchmakingService] Iniciando matchmaking individual | fila='{queueName}' entity={authContext.EntityType}/{authContext.EntityId}");
        SetState(MatchmakingState.Searching);
        matchmakingRoutine = StartCoroutine(RunSinglePlayerMatchmaking());
    }

    public void StartTwoUserMatchmaking(string queue, string userAId, string userBId, int timeout, float pollInterval, bool allowCreateMissingUsers = false)
    {
        if (!ValidateMatchmakingStart(queue)) return;

        queueName           = queue.Trim();
        timeoutSeconds      = Mathf.Max(5, timeout);
        pollIntervalSeconds = Mathf.Max(0.5f, pollInterval);
        createMissingUsersForTest = allowCreateMissingUsers;
        userA = new MatchmakingTestUser { loginType = TestUserLoginType.CustomId, customId = userAId.Trim() };
        userB = new MatchmakingTestUser { loginType = TestUserLoginType.CustomId, customId = userBId.Trim() };
        cancellationRequested = false;

        Debug.Log($"[MatchmakingService] Iniciando (CustomId) queue='{queueName}' userA='{userAId}' userB='{userBId}'");
        SetState(MatchmakingState.Searching);
        matchmakingRoutine = StartCoroutine(RunTwoUserMatchmaking());
    }

    public void StartTwoUserMatchmakingWithEmail(string queue, string emailA, string passwordA, string emailB, string passwordB, int timeout, float pollInterval)
    {
        if (!ValidateMatchmakingStart(queue)) return;

        queueName           = queue.Trim();
        timeoutSeconds      = Mathf.Max(5, timeout);
        pollIntervalSeconds = Mathf.Max(0.5f, pollInterval);
        createMissingUsersForTest = false;
        userA = new MatchmakingTestUser { loginType = TestUserLoginType.EmailPassword, email = emailA.Trim(), password = passwordA };
        userB = new MatchmakingTestUser { loginType = TestUserLoginType.EmailPassword, email = emailB.Trim(), password = passwordB };
        cancellationRequested = false;

        Debug.Log($"[MatchmakingService] Iniciando (Email) queue='{queueName}' userA='{emailA}' userB='{emailB}'");
        SetState(MatchmakingState.Searching);
        matchmakingRoutine = StartCoroutine(RunTwoUserMatchmaking());
    }

    private bool ValidateMatchmakingStart(string queue)
    {
        if (matchmakingRoutine != null)
        {
            Debug.LogWarning("[MatchmakingService] Ja existe um teste de matchmaking em andamento.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(queue))
        {
            Debug.LogError("[MatchmakingService] Queue deve estar preenchida.");
            SetState(MatchmakingState.Failed);
            return false;
        }
        return true;
    }

#if UNITY_EDITOR
    public void SimularMatchEncontrado(string matchId = "debug-match-id")
    {
        SetState(MatchmakingState.Matched);
        OnMatchFound?.Invoke(matchId);
    }
#endif

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

    private IEnumerator RunSinglePlayerMatchmaking()
    {
        yield return CancelAllTicketsForUser(singleUser);

        yield return CreateTicket(singleUser);
        if (CurrentState == MatchmakingState.Failed)
        {
            FinishRoutine();
            yield break;
        }

        var deadline = Time.time + timeoutSeconds;
        while (Time.time < deadline && !cancellationRequested)
        {
            yield return new WaitForSeconds(pollIntervalSeconds);
            yield return PollTicket(singleUser);

            if (singleUser.status == "Matched" && !string.IsNullOrWhiteSpace(singleUser.matchId))
            {
                SetState(MatchmakingState.Matched);
                Debug.Log($"[MatchmakingService] Match encontrado! MatchId: {singleUser.matchId}");
                OnMatchFound?.Invoke(singleUser.matchId);
                FinishRoutine();
                yield break;
            }
        }

        if (cancellationRequested)
        {
            yield return CancelTicketIfNeeded(singleUser);
            SetState(MatchmakingState.Cancelled);
            FinishRoutine();
            yield break;
        }

        yield return CancelTicketIfNeeded(singleUser);
        SetState(MatchmakingState.TimedOut);
        Debug.LogWarning($"[MatchmakingService] Timeout após {timeoutSeconds}s sem match.");
        FinishRoutine();
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

        if (user.loginType == TestUserLoginType.EmailPassword)
        {
            yield return LoginWithEmail(user.clientApi, user.email, user.password,
                r => loginResult = r, e => loginError = e);
        }
        else
        {
            yield return LoginWithCustomIdComRetry(user.clientApi, user.customId, false,
                r => loginResult = r, e => loginError = e);

            if (loginError != null && loginError.Error == PlayFabErrorCode.AccountNotFound && createMissingUsersForTest)
            {
                Debug.LogWarning($"[MatchmakingService] Usuario '{user.customId}' nao existe. Tentando CreateAccount=true.");
                loginResult = null;
                loginError  = null;
                yield return LoginWithCustomIdComRetry(user.clientApi, user.customId, true,
                    r => loginResult = r, e => loginError = e);
            }
        }

        if (loginError != null)
        {
            string label = user.loginType == TestUserLoginType.EmailPassword ? user.email : user.customId;
            HandlePlayFabFailure($"Falha no login do usuario '{label}'", loginError);
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

    private static IEnumerator LoginWithEmail(PlayFabClientInstanceAPI api, string email, string password, Action<LoginResult> onSuccess, Action<PlayFabError> onError)
    {
        bool done = false;

        api.LoginWithEmailAddress(new LoginWithEmailAddressRequest
        {
            Email    = email,
            Password = password
        },
        result => { onSuccess?.Invoke(result); done = true; },
        error  => { onError?.Invoke(error);   done = true; });

        while (!done) yield return null;
    }

    private static IEnumerator LoginWithCustomIdComRetry(PlayFabClientInstanceAPI api, string customId, bool createAccount, Action<LoginResult> onSuccess, Action<PlayFabError> onError, int maxRetries = 3)
    {
        float[] delays = new float[] { 3f, 6f, 12f };

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            LoginResult loginResult = null;
            PlayFabError loginError = null;

            yield return LoginWithCustomId(api, customId, createAccount,
                r => loginResult = r,
                e => loginError = e);

            if (loginResult != null)
            {
                onSuccess?.Invoke(loginResult);
                yield break;
            }

            bool throttled = loginError?.HttpCode == 429;

            if (!throttled || attempt == maxRetries)
            {
                onError?.Invoke(loginError);
                yield break;
            }

            float wait = delays[Mathf.Min(attempt, delays.Length - 1)];
            Debug.LogWarning($"[MatchmakingService] Throttling no login de '{customId}'. Tentativa {attempt + 1}/{maxRetries}. Aguardando {wait}s...");
            yield return new WaitForSeconds(wait);
        }
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
