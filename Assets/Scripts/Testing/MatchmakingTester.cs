using UnityEngine;
using UnityEngine.InputSystem;
using PlayFab;

public class MatchmakingTester : PlayFabTerminalTester
{
    private const string Title = "MatchmakingTester";
    private const string LegacyQueueId = "queue_test!";
    private const string DefaultQueueId = "queue_test";
    private const string LegacyUserBId = "test_user_456";
    private const string DefaultUserBId = "teste_user_456";

    [Header("Queue e jogadores de teste")]
    [SerializeField] private string queueName = DefaultQueueId;
    [SerializeField] private string userAId = "test_user_123";
    [SerializeField] private string userBId = DefaultUserBId;
    [SerializeField] private bool createMissingUsersForTest = false;

    [Header("Tempos")]
    [SerializeField] private int timeoutSeconds = 30;
    [SerializeField] private float pollIntervalSeconds = 2f;

    private void OnValidate()
    {
        if (queueName == LegacyQueueId)
        {
            queueName = DefaultQueueId;
            Debug.LogWarning($"[{Title}] queueName legado detectado ('{LegacyQueueId}'). Valor migrado automaticamente para '{DefaultQueueId}'.");
        }

        if (userBId == LegacyUserBId)
        {
            userBId = DefaultUserBId;
            Debug.LogWarning($"[{Title}] userBId legado detectado ('{LegacyUserBId}'). Valor migrado automaticamente para '{DefaultUserBId}'.");
        }
    }

    protected override void Start()
    {
        base.Start();
        EnsureService<MatchmakingService>();
        MatchmakingService.OnStateChanged += HandleStateChanged;
        MatchmakingService.OnMatchFound += HandleMatchFound;
        MatchmakingService.OnMatchmakingFailed += HandleError;
        Debug.Log($"[{Title}] Config atual: queue='{queueName}', userA='{userAId}', userB='{userBId}', createMissingUsersForTest={createMissingUsersForTest}");
        PrintReadyMessage(Title, "1=iniciar teste 2x usuarios, 2=cancelar busca, 3=mostrar estado atual, 4=mostrar diagnostico");
    }

    private void OnDestroy()
    {
        MatchmakingService.OnStateChanged -= HandleStateChanged;
        MatchmakingService.OnMatchFound -= HandleMatchFound;
        MatchmakingService.OnMatchmakingFailed -= HandleError;
    }

    private void Update()
    {
        if (!HasKeyboard())
        {
            return;
        }

        var keyboard = Keyboard.current;

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            MatchmakingService.Instance.StartTwoUserMatchmaking(queueName, userAId, userBId, timeoutSeconds, pollIntervalSeconds, createMissingUsersForTest);
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            MatchmakingService.Instance.CancelCurrentSearch();
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            Debug.Log($"[{Title}] Estado atual: {MatchmakingService.Instance.CurrentState}");
        }
        else if (keyboard.digit4Key.wasPressedThisFrame)
        {
            Debug.Log($"[{Title}] {MatchmakingService.Instance.GetDiagnosticsSummary()}");
        }
    }

    private void HandleStateChanged(MatchmakingService.MatchmakingState state)
    {
        Debug.Log($"[{Title}] Estado atualizado: {state}");
    }

    private void HandleMatchFound(string matchId)
    {
        Debug.Log($"[{Title}] ✅ Match encontrado: {matchId}");
    }

    private void HandleError(PlayFabError error)
    {
        Debug.LogError($"[{Title}] ❌ Erro PlayFab: {error.GenerateErrorReport()}");
    }
}