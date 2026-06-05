// ============================================================
// NetworkMessages.cs — contratos de rede entre cliente e
// servidor (via PlayFab Party).  Cada mensagem é serializada
// como JSON dentro do campo Payload de NetworkEnvelope.
// ============================================================
using System;
using BrainDuel.Match;

namespace BrainDuel.Network
{
    // ----------------------------------------------------------
    // Envelope genérico que trafega pela Party network
    // ----------------------------------------------------------

    public enum MessageType : byte
    {
        // Fase de round
        RoundStart          = 10,
        QuestionReveal      = 11,
        OpponentAnswered    = 12,  // avisa que oponente respondeu (sem revelar o quê)
        RoundResult         = 13,
        PowerUpActivated    = 15,

        // Controle de partida
        MatchEnd            = 20,
        PlayerReady         = 21,

        // Latência
        Ping                = 30,
        Pong                = 31,

        // Reconexão
        ReconnectRequest    = 40,
        ReconnectSync       = 41,

        // Presença
        OpponentDisconnected = 50,
        OpponentReconnected  = 51,
        OpponentAbandoned    = 52,
    }

    [Serializable]
    public class NetworkEnvelope
    {
        public MessageType Type;
        public string      Payload;    // JSON do payload específico
        public long        SentAtMs;
        public string      SenderId;
        public int         SequenceId; // para ordenação garantida
    }

    // ----------------------------------------------------------
    // Servidor → Clientes
    // ----------------------------------------------------------

    /// <summary>Inicia uma nova rodada — enviado pelo servidor após processar a anterior.</summary>
    [Serializable]
    public class RoundStartPayload
    {
        public int    RoundNumber;
        public string ThemeId;
        public string ThemeName;
        public long   ServerTimestampMs;   // referência para sincronizar timers
        public int    ThemeDurationMs;     // padrão: 4000
        public int    QuestionDurationMs;  // padrão: 20000
    }

    /// <summary>Revela a pergunta após os 4 s do tema.</summary>
    [Serializable]
    public class QuestionRevealPayload
    {
        public string        QuestionId;
        public string        QuestionText;
        public AnswerOption[] Answers;
        public long          ServerTimestampMs;
        public int           DurationMs;
        public int[]         EliminatedIndices; // índices eliminados pelo EliminateTwo
    }

    /// <summary>Notifica que o oponente respondeu (sem revelar resposta).</summary>
    [Serializable]
    public class OpponentAnsweredPayload
    {
        public string PlayerId;
        public long   TimestampMs;
    }

    /// <summary>Resultado autoritativo da rodada, calculado pelo servidor.</summary>
    [Serializable]
    public class RoundResultPayload
    {
        public int              RoundNumber;
        public string           CorrectAnswerId;  // revelado após todos responderem
        public RoundPlayerResult Player1Result;
        public RoundPlayerResult Player2Result;
        public int              Player1HP;
        public int              Player2HP;
        public bool             IsMatchOver;
        public string           WinnerId;
        public MatchEndReason   EndReason;
    }

    /// <summary>Fim de partida.</summary>
    [Serializable]
    public class MatchEndPayload
    {
        public string        WinnerId;
        public int           WinnerHP;
        public int           LoserHP;
        public MatchEndReason Reason;
        public int           TotalRoundsPlayed;
    }

    // ----------------------------------------------------------
    // Cliente → Servidor  (via CloudScript HTTP, não Party)
    // Documentados aqui como contrato de API
    // ----------------------------------------------------------

    /// <summary>Corpo da chamada CloudScript SubmitAnswer.</summary>
    [Serializable]
    public class SubmitAnswerRequest
    {
        public string MatchId;
        public int    RoundNumber;
        public string AnswerId;
        public long   ClientTimestampMs;
    }

    /// <summary>Corpo da chamada CloudScript ActivatePowerUp.</summary>
    [Serializable]
    public class ActivatePowerUpRequest
    {
        public string    MatchId;
        public int       RoundNumber;
        public PowerUpType PowerUp;
    }

    // ----------------------------------------------------------
    // Power-up broadcast
    // ----------------------------------------------------------

    /// <summary>Informa oponente que um power-up foi ativado nesta rodada.</summary>
    [Serializable]
    public class PowerUpActivatedPayload
    {
        public string    PlayerId;
        public PowerUpType PowerUp;
        // EliminateTwo: quais índices de resposta serão removidos
        public int[]     EliminatedIndices;
    }

    // ----------------------------------------------------------
    // Latência
    // ----------------------------------------------------------

    [Serializable]
    public class PingPayload
    {
        public long ClientTimestampMs;
    }

    [Serializable]
    public class PongPayload
    {
        public long OriginalClientTimestampMs;
        public long ServerTimestampMs;
    }

    // ----------------------------------------------------------
    // Reconexão
    // ----------------------------------------------------------

    [Serializable]
    public class ReconnectSyncPayload
    {
        public ServerMatchState FullState;
        public QuestionData     CurrentQuestion; // null se não estiver em QuestionPhase
        public long             ServerTimestampMs;
    }
}
