// ============================================================
// ServerModels.cs — modelos exclusivos do CloudScript.
// Espelham os contratos de BrainDuel.Match mas sem dependências
// Unity para compilar no ambiente Azure Functions.
// ============================================================
using System;
using System.Collections.Generic;

namespace BrainDuel.CloudScript
{
    // ----------------------------------------------------------
    // Enums (espelhos de MatchContracts.cs)
    // ----------------------------------------------------------

    public enum MatchPhase    { Initializing, ThemeAndPowerUp, Question, Reveal, RoundEnd, MatchEnd }
    public enum PowerUpType   { None, SimpleShield, DoubleShield, EliminateTwo, Bet, Steal }
    public enum AnswerResult  { NotAnswered, Correct, Incorrect }
    public enum MatchEndReason { HPDepleted, RoundsOver, Abandonment, Disconnected }

    // ----------------------------------------------------------
    // Estado da partida (persiste em Entity Objects do PlayFab)
    // ----------------------------------------------------------

    public class ServerMatchState
    {
        public string          MatchId;
        public string          Player1Id;
        public string          Player2Id;
        public PlayerMatchState Player1State;
        public PlayerMatchState Player2State;
        public int             CurrentRound;           // 1-based
        public MatchPhase      Phase;
        public long            PhaseStartTimestampMs;
        public List<string>    QuestionPool;           // IDs em ordem aleatória
        public ServerRoundState CurrentRoundState;
        public bool            IsActive;
        public string          WinnerId;
        public MatchEndReason  EndReason;
        public long            MatchStartTimestampMs;
        public string          PartyNetworkDescriptor;

        // Idempotência: evita processar mesma ação duas vezes
        public int LastProcessedRound;
    }

    public class PlayerMatchState
    {
        public string      PlayerId;
        public string      DisplayName;
        public int         HP                    = 100;
        public int         Streak                = 0;
        public bool        HasUsedPowerUp        = false;
        public PowerUpType EquippedPowerUp;
        public int         DoubleShieldCharges   = 0;
        public bool        IsConnected           = true;
        public int         ConsecutiveMissedRounds = 0;
        public long        LastHeartbeatMs;
        public long        DisconnectedAtMs;
    }

    public class ServerRoundState
    {
        public int                RoundNumber;
        public string             QuestionId;
        public string             ThemeId;
        public string             ThemeName;
        public string             CorrectAnswerId;
        public RoundPlayerAction  Player1Action  = new RoundPlayerAction();
        public RoundPlayerAction  Player2Action  = new RoundPlayerAction();
        public bool               IsProcessed    = false;
        public RoundPlayerResult  Player1Result;
        public RoundPlayerResult  Player2Result;
    }

    public class RoundPlayerAction
    {
        public string      PlayerId;
        public string      AnswerId;
        public long        AnswerTimestampMs;
        public bool        HasAnswered       = false;
        public PowerUpType ActivatedPowerUp  = PowerUpType.None;
    }

    public class DamageBreakdown
    {
        public int BaseDamage;
        public int SpeedBonus;
        public int StreakBonus;
        public int PowerUpBonus;
        public int StolenHP;
        public int SelfDamage;
        public int Total => BaseDamage + SpeedBonus + StreakBonus + PowerUpBonus + StolenHP - SelfDamage;
    }

    public class RoundPlayerResult
    {
        public string          PlayerId;
        public AnswerResult    Result;
        public string          AnsweredId;
        public int             DamageDealt;
        public int             HPBefore;
        public int             HPAfter;
        public bool            WasShielded;
        public int             StreakAfter;
        public DamageBreakdown Breakdown    = new DamageBreakdown();
    }

    // ----------------------------------------------------------
    // Requests recebidos pelo CloudScript
    // ----------------------------------------------------------

    public class CreateMatchRequest
    {
        public string MatchId;
        public string Player1Id;
        public string Player2Id;
    }

    public class SubmitAnswerRequest
    {
        public string MatchId;
        public int    RoundNumber;
        public string AnswerId;
        public long   ClientTimestampMs;
        public string PlayerId; // preenchido pelo contexto PlayFab
    }

    public class ActivatePowerUpRequest
    {
        public string      MatchId;
        public int         RoundNumber;
        public PowerUpType PowerUp;
        public string      PlayerId;
    }

    public class ProcessRoundRequest
    {
        public string MatchId;
        public int    RoundNumber;
        public string PlayerId; // quem iniciou o pedido (não afeta resultado)
    }

    public class StartNextRoundRequest
    {
        public string MatchId;
        public int    RoundNumber; // número da próxima rodada
    }

    public class RejoinMatchRequest
    {
        public string MatchId;
        public string PlayerId;
    }

    public class GrantStarterDecksRequest
    {
        public string CatalogVersion; // opcional — padrão "mainCatalog"
    }

    // ----------------------------------------------------------
    // Responses enviadas de volta ao cliente e/ou broadcast Party
    // ----------------------------------------------------------

    public class CreateMatchResponse
    {
        public string MatchId;
        public string NetworkDescriptor;
        public bool   Success;
    }

    public class ProcessRoundResponse
    {
        public bool             AlreadyProcessed;
        public RoundPlayerResult Player1Result;
        public RoundPlayerResult Player2Result;
        public int              Player1HP;
        public int              Player2HP;
        public bool             IsMatchOver;
        public string           WinnerId;
        public MatchEndReason   EndReason;
    }

    // ----------------------------------------------------------
    // Dados de questão (lidos do Title Data ou catalog)
    // ----------------------------------------------------------

    public class QuestionData
    {
        public string        QuestionId;
        public string        Text;
        public string        ThemeId;
        public string        ThemeName;
        public List<AnswerOption> Options;
        public string        CorrectOptionId;
        public int           DifficultyLevel;
    }

    public class AnswerOption
    {
        public string Id;
        public string Text;
    }

    // ----------------------------------------------------------
    // Deck schemas (espelham DeckSchemaV2.cs do cliente)
    // Deserializados do Title Data — chave: "cartas_<categoria>"
    // ----------------------------------------------------------

    public class DeckSchemaServer
    {
        public string deck_id;
        public string theme;
        public List<DeckQuestionServer> questions;
    }

    public class DeckQuestionServer
    {
        public string id;
        public string text;
        public List<DeckOptionServer> options;
        public int time_limit;
    }

    public class DeckOptionServer
    {
        public string text;
        public bool is_correct;
    }

    // ----------------------------------------------------------
    // Perfil do jogador — apenas campos usados no servidor
    // Deserializado do User Data — chave: "player_profile"
    // ----------------------------------------------------------

    public class PlayerProfileServer
    {
        public string equippedDeckId  = "";
        public string equippedPowerUp = "";   // enum name: "SimpleShield", "Bet", etc.
    }
}
