// ============================================================
// MatchContracts.cs — modelos e enums compartilhados
// entre cliente e servidor (espelhados no CloudScript).
// ============================================================
using System;

namespace BrainDuel.Match
{
    // ----------------------------------------------------------
    // Enums
    // ----------------------------------------------------------

    public enum MatchPhase
    {
        Initializing    = 0,
        ThemeAndPowerUp = 1,   // 4 s: exibe tema + janela power-up
        Question        = 2,   // 20 s: exibe pergunta + timer
        Reveal          = 3,   // 3 s: revela resultado da rodada
        RoundEnd        = 4,   // 1.5 s: pausa antes da próxima rodada
        MatchEnd        = 5
    }

    public enum PowerUpType
    {
        None         = 0,
        SimpleShield = 1,   // bloqueia dano desta rodada
        DoubleShield = 2,   // bloqueia os 2 próximos danos
        EliminateTwo = 3,   // remove 2 respostas erradas
        Bet          = 4,   // +5 de dano se acertar
        Steal        = 5    // rouba 5 HP independente da resposta
    }

    public enum AnswerResult
    {
        NotAnswered = 0,
        Correct     = 1,
        Incorrect   = 2
    }

    public enum MatchEndReason
    {
        HPDepleted   = 0,
        RoundsOver   = 1,
        Abandonment  = 2,
        Disconnected = 3
    }

    // ----------------------------------------------------------
    // Constantes da partida
    // ----------------------------------------------------------

    public static class MatchConfig
    {
        public const int   InitialHP                = 100;
        public const int   MaxRounds               = 20;
        public const int   ThemePhaseDurationMs    = 5_000;
        public const int   QuestionPhaseDurationMs = 20_000;
        public const int   RevealPhaseDurationMs   = 5_000;
        public const int   RoundEndPhaseDurationMs = 1_500;
        public const int   SpeedBonusThresholdMs   = 200;
        public const int   AfkRoundLimit           = 3;
        public const int   ReconnectWindowMs       = 30_000;
    }

    public static class DamageConfig
    {
        public const int BaseDamage        = 5;
        public const int SpeedBonus        = 2;
        public const int SelfDamageNoAnswer = 3;
        public const int BetBonus          = 5;
        public const int StealAmount       = 5;

        public static int GetStreakBonus(int streak) => streak switch
        {
            <= 1 => 0,
            2    => 1,
            3    => 3,
            _    => 5
        };
    }

    // ----------------------------------------------------------
    // Modelos de pergunta
    // ----------------------------------------------------------

    [Serializable]
    public class AnswerOption
    {
        public string Id;
        public string Text;
        public bool   IsEliminated; // ativado pelo EliminateTwo
    }

    [Serializable]
    public class QuestionData
    {
        public string       QuestionId;
        public string       Text;
        public string       ThemeId;
        public string       ThemeName;
        public AnswerOption[] Options;
        public string       CorrectOptionId;
        public int          DifficultyLevel; // 1-5
    }

    // ----------------------------------------------------------
    // Estado do jogador na partida
    // ----------------------------------------------------------

    [Serializable]
    public class PlayerMatchState
    {
        public string    PlayerId;
        public string    DisplayName;
        public int       Level;
        public int       HP;
        public int       Streak;
        public bool      HasUsedPowerUp;
        public PowerUpType EquippedPowerUp;
        public int       DoubleShieldCharges;  // cargas restantes do DoubleShield
        public bool      IsConnected;
        public int       ConsecutiveMissedRounds;
        public long      LastHeartbeatMs;
    }

    // ----------------------------------------------------------
    // Estado da rodada (autoritativo, vive no servidor)
    // ----------------------------------------------------------

    [Serializable]
    public class RoundPlayerAction
    {
        public string    PlayerId;
        public string    AnswerId;
        public long      AnswerTimestampMs;
        public bool      HasAnswered;
        public PowerUpType ActivatedPowerUp;
    }

    [Serializable]
    public class DamageBreakdown
    {
        public int BaseDamage;
        public int SpeedBonus;
        public int StreakBonus;
        public int PowerUpBonus;  // Bet
        public int StolenHP;      // Steal
        public int SelfDamage;    // penalidade por não responder

        public int Total => BaseDamage + SpeedBonus + StreakBonus + PowerUpBonus + StolenHP - SelfDamage;
    }

    [Serializable]
    public class RoundPlayerResult
    {
        public string        PlayerId;
        public AnswerResult  Result;
        public string        AnsweredId;
        public int           DamageDealt;
        public int           HPBefore;
        public int           HPAfter;
        public bool          WasShielded;
        public int           StreakAfter;
        public DamageBreakdown Breakdown;
    }

    // ----------------------------------------------------------
    // Estado completo da partida (fonte da verdade no servidor)
    // ----------------------------------------------------------

    [Serializable]
    public class ServerRoundState
    {
        public int              RoundNumber;
        public string           QuestionId;
        public string           ThemeId;
        public string           ThemeName;
        public string           CorrectAnswerId;
        public RoundPlayerAction Player1Action;
        public RoundPlayerAction Player2Action;
        public bool             IsProcessed;
        public RoundPlayerResult Player1Result;
        public RoundPlayerResult Player2Result;
    }

    [Serializable]
    public class ServerMatchState
    {
        public string          MatchId;
        public string          Player1Id;
        public string          Player2Id;
        public PlayerMatchState Player1State;
        public PlayerMatchState Player2State;
        public int             CurrentRound;
        public MatchPhase      Phase;
        public long            PhaseStartTimestampMs;
        public string[]        QuestionPool;          // IDs ordenados
        public ServerRoundState CurrentRoundState;
        public bool            IsActive;
        public string          WinnerId;
        public MatchEndReason  EndReason;
        public long            MatchStartTimestampMs;
    }
}
