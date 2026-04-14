using UnityEngine;
using UnityEngine.InputSystem;

public class AchievementTester : PlayFabTerminalTester
{
    private const string Title = "AchievementTester";

    [SerializeField] private int requiredWins = 3;
    [SerializeField] private int requiredScore = 300;
    [SerializeField] private int requiredLevel = 2;

    private int localWins;
    private int localScore;

    protected override void Start()
    {
        base.Start();
        EnsureService<StatisticsService>();
        EnsureService<PlayerDataService>();
        PrintReadyMessage(Title, "1=simular vitoria, 2=simular derrota, 3=avaliar conquista, 4=mostrar progresso local");
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
            localWins++;
            localScore += 100;
            StatisticsService.Instance.UpdateMatchStatistics(100, true);
            Debug.Log($"[{Title}] Vitoria simulada. Wins={localWins}, Score={localScore}");
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            localScore += 10;
            StatisticsService.Instance.UpdateMatchStatistics(10, false);
            Debug.Log($"[{Title}] Derrota simulada. Wins={localWins}, Score={localScore}");
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            EvaluateAchievement();
        }
        else if (keyboard.digit4Key.wasPressedThisFrame)
        {
            var profile = PlayerDataService.Instance != null ? PlayerDataService.Instance.CurrentProfile : null;
            int level = profile != null ? profile.level : 0;
            Debug.Log($"[{Title}] Progresso local -> Wins={localWins}, Score={localScore}, Level={level}");
        }
    }

    private void EvaluateAchievement()
    {
        var profile = PlayerDataService.Instance != null ? PlayerDataService.Instance.CurrentProfile : null;
        int level = profile != null ? profile.level : 0;

        bool unlocked = localWins >= requiredWins && localScore >= requiredScore && level >= requiredLevel;

        if (unlocked)
        {
            Debug.Log($"[{Title}] ✅ Conquista desbloqueada por regras: Wins>={requiredWins}, Score>={requiredScore}, Level>={requiredLevel}");
        }
        else
        {
            Debug.LogWarning($"[{Title}] Conquista ainda bloqueada. Atual={localWins}/{requiredWins} wins, {localScore}/{requiredScore} score, Level={level}/{requiredLevel}");
        }
    }
}