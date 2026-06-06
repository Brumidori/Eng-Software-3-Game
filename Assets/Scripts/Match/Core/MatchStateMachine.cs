// ============================================================
// MatchStateMachine.cs — orquestra os estados da partida no
// cliente.  Recebe eventos da PartyNetworkManager e do
// CloudScript e delega para o estado ativo.
//
// Fluxo por rodada:
//   ThemeAndPowerUp (5s) → Question (20s) → Reveal (3s) → RoundEnd (1.5s)
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
        public event Action<RoundResultPayload>      OnRoundResultReceived;
        public event Action<RoundStartPayload>       OnRoundStarted;
        public event Action<QuestionRevealPayload>   OnQuestionRevealed;
        public event Action<PowerUpActivatedPayload> OnPowerUpActivatedReceived;

        // ----------------------------------------------------------
        // Estado
        // ----------------------------------------------------------
        public MatchContext    Context  { get; private set; }
        public MatchPhase      Phase    { get; private set; } = MatchPhase.Initializing;

        private BaseMatchState                         _currentState;
        private Dictionary<MatchPhase, BaseMatchState> _states;
        private ReconnectionManager                    _reconnectionManager;
        private Coroutine                              _stubConductorCoroutine;

        private struct StubRoundData { public string ThemeName; public Carta Carta; }
        private StubRoundData[] _stubRoundPool;

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
                // PlayFabId clássico — deve bater com o Player1Id/Player2Id armazenado no CloudScript
                LocalPlayerId    = MatchSessionData.LocalPlayerId
                                   ?? PlayFab.PlayFabSettings.staticPlayer?.PlayFabId
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
            // Garante que o singleton existe para SubscribeToNetworkEvents
            if (PartyNetworkManager.Instance == null)
            {
                var go = new GameObject("PartyNetworkManager");
                go.AddComponent<PartyNetworkManager>();
                yield return null;
                SubscribeToNetworkEvents();
            }

            // Aguarda todos os Start() dos outros MonoBehaviours (MatchSceneController, etc.)
            yield return null;

            // Modo stub: cria ServerState mínimo para que HP / round funcionem na UI
            if (Context.IsStubMode && Context.ServerState == null)
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
                        PlayerId        = Context.LocalPlayerId,
                        DisplayName     = Context.LocalDisplayName,
                        Level           = Context.LocalLevel,
                        HP              = MatchConfig.InitialHP,
                        IsConnected     = true,
                        EquippedPowerUp = ResolveEquippedPowerUp(),
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

            if (Context.IsStubMode)
            {
                yield return BuildStubRoundPool();

                string temaRound1 = _stubRoundPool != null && _stubRoundPool.Length > 0
                    ? _stubRoundPool[0].ThemeName
                    : "Historia";

                // Modo stub: pula chamada ao servidor e dispara RoundStart localmente
                HandleRoundStart(new RoundStartPayload
                {
                    RoundNumber        = 1,
                    ThemeId            = temaRound1,
                    ThemeName          = temaRound1,
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
                    onSuccess: result =>
                    {
                        // Verifica se o servidor retornou erro (ex: "Match inativo" — CreateMatch não foi chamado)
                        if (result != null)
                        {
                            try
                            {
                                var chkJson = PlayFab.Json.PlayFabSimpleJson.SerializeObject(result);
                                var chk     = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<Dictionary<string, object>>(chkJson);
                                if (chk != null && chk.ContainsKey("error"))
                                {
                                    Debug.LogError($"[Match] StartNextRound erro do servidor: {chk["error"]} | matchId={Context.MatchId}");
                                    roundStarted = true;
                                    return;
                                }
                            }
                            catch { /* ignora falha no check, continua normalmente */ }
                        }

                        if (result != null && Context.ServerState == null)
                            InicializarServerStateDoRetorno(result);

                        // Dispara a transição de fase diretamente — não depende de Party broadcast
                        var payload = ParsearRoundStart(result, 1);
                        if (payload != null)
                            HandleRoundStart(payload);

                        roundStarted = true;
                    },
                    onError: _ => roundStarted = true);

                float waited = 0f;
                while (!roundStarted && waited < 5f)
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
            MatchSessionData.Clear();
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

                if (Context.IsStubMode)
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

                    QuestionRevealPayload qPayload;
                    int roundIdx = Context.CurrentRound - 1;

                    if (_stubRoundPool != null && roundIdx >= 0 && roundIdx < _stubRoundPool.Length
                        && _stubRoundPool[roundIdx].Carta != null)
                    {
                        var carta   = _stubRoundPool[roundIdx].Carta;
                        var answers = new AnswerOption[carta.alternativas.Count];
                        for (int i = 0; i < carta.alternativas.Count; i++)
                            answers[i] = new AnswerOption { Id = ((char)('A' + i)).ToString(), Text = carta.alternativas[i] };

                        // Expõe a resposta correta para o EliminateTwo client-side
                        CurrentStubCorrectAnswerId = ((char)('A' + carta.respostaCorreta)).ToString();

                        qPayload = new QuestionRevealPayload
                        {
                            QuestionId        = carta.id,
                            QuestionText      = carta.pergunta,
                            Answers           = answers,
                            ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            DurationMs        = MatchConfig.QuestionPhaseDurationMs,
                        };
                    }
                    else
                    {
                        CurrentStubCorrectAnswerId = "A"; // fallback hardcoded
                        qPayload = new QuestionRevealPayload
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
                        };
                    }

                    HandleQuestionReveal(qPayload);
                    break;
                }

                case MatchPhase.Question:
                {
                    yield return new WaitForSeconds(MatchConfig.QuestionPhaseDurationMs / 1000f + 0.1f);
                    if (Phase != MatchPhase.Question) yield break;

                    int round    = Context.CurrentRound;
                    int roundIdx = round - 1;
                    int localHP    = Context.LocalHP;
                    int opponentHP = Context.OpponentHP;

                    // Resolve correct answer ID from pool (index → letter) or hardcoded fallback
                    string correctAnswerId = "A";
                    if (_stubRoundPool != null && roundIdx >= 0 && roundIdx < _stubRoundPool.Length
                        && _stubRoundPool[roundIdx].Carta != null)
                        correctAnswerId = ((char)('A' + _stubRoundPool[roundIdx].Carta.respostaCorreta)).ToString();

                    bool answered = Context.HasAnsweredThisRound;
                    bool acertou  = answered && Context.SelectedAnswerId == correctAnswerId;

                    // Streak: novo valor após esta rodada
                    int currentStreak = Context.LocalStreak;
                    int newStreak     = acertou ? currentStreak + 1 : 0;
                    int streakBonus   = acertou ? DamageConfig.GetStreakBonus(newStreak) : 0;
                    // Aposta: +5 de dano extra se acertou
                    int betBonus   = (acertou && Context.PendingPowerUp == PowerUpType.Bet) ? DamageConfig.BetBonus : 0;
                    int dmg        = acertou ? DamageConfig.BaseDamage + streakBonus + betBonus : 0;
                    // Roubo: transfere 5 HP do oponente para o jogador local (independente da resposta)
                    int stealAmount = Context.PendingPowerUp == PowerUpType.Steal ? DamageConfig.StealAmount : 0;

                    int newOppHP   = Mathf.Max(0, opponentHP - dmg - stealAmount);
                    int newLocalHP = Mathf.Min(MatchConfig.InitialHP, localHP + stealAmount);

                    // Rastreia rodadas sem resposta do jogador local
                    if (Context.LocalPlayer != null)
                    {
                        if (!answered)
                            Context.LocalPlayer.ConsecutiveMissedRounds++;
                        else
                            Context.LocalPlayer.ConsecutiveMissedRounds = 0;
                    }

                    // AFK: 3 rodadas sem responder → derrota por abandono
                    bool localAfk  = Context.LocalPlayer != null
                                  && Context.LocalPlayer.ConsecutiveMissedRounds >= MatchConfig.AfkRoundLimit;
                    bool matchOver = newOppHP <= 0 || newLocalHP <= 0 || round >= MatchConfig.MaxRounds || localAfk;

                    // Determina vencedor: AFK local → oponente vence; caso contrário, lógica normal
                    string stubWinnerId = null;
                    MatchEndReason stubEndReason = MatchEndReason.HPDepleted;
                    if (matchOver)
                    {
                        if (localAfk)
                        {
                            stubWinnerId  = Context.ServerState?.Player2Id ?? "oponente_stub";
                            stubEndReason = MatchEndReason.Abandonment;
                        }
                        else if (round >= MatchConfig.MaxRounds)
                        {
                            stubWinnerId  = newOppHP < newLocalHP ? Context.LocalPlayerId
                                          : newLocalHP < newOppHP ? Context.ServerState?.Player2Id
                                          : null;
                            stubEndReason = MatchEndReason.RoundsOver;
                        }
                        else
                        {
                            stubWinnerId  = newLocalHP <= 0 ? Context.ServerState?.Player2Id ?? "oponente_stub"
                                          : Context.LocalPlayerId;
                            stubEndReason = MatchEndReason.HPDepleted;
                        }
                    }

                    HandleRoundResult(new RoundResultPayload
                    {
                        RoundNumber     = round,
                        CorrectAnswerId = correctAnswerId,
                        Player1Result   = new RoundPlayerResult
                        {
                            PlayerId    = Context.LocalPlayerId,
                            Result      = answered
                                            ? (acertou ? AnswerResult.Correct : AnswerResult.Incorrect)
                                            : AnswerResult.NotAnswered,
                            AnsweredId  = Context.SelectedAnswerId,
                            DamageDealt = dmg + stealAmount,
                            HPBefore    = localHP,
                            HPAfter     = newLocalHP,
                            WasShielded = false,
                            StreakAfter = newStreak,
                            Breakdown   = new DamageBreakdown { BaseDamage = DamageConfig.BaseDamage, StreakBonus = streakBonus, PowerUpBonus = betBonus, StolenHP = stealAmount },
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
                        Player1HP   = newLocalHP,
                        Player2HP   = newOppHP,
                        IsMatchOver = matchOver,
                        WinnerId    = stubWinnerId,
                        EndReason   = stubEndReason,
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

                    int nextRoundIdx = nextRound - 1;
                    string temaStub;
                    if (_stubRoundPool != null && nextRoundIdx >= 0 && nextRoundIdx < _stubRoundPool.Length)
                        temaStub = _stubRoundPool[nextRoundIdx].ThemeName;
                    else
                    {
                        string[] temasStub = { "Historia", "Ciencia", "Geografia", "Tecnologia", "Literatura" };
                        temaStub = temasStub[(nextRound - 1) % temasStub.Length];
                    }

                    HandleRoundStart(new RoundStartPayload
                    {
                        RoundNumber        = nextRound,
                        ThemeId            = temaStub,
                        ThemeName          = temaStub,
                        ServerTimestampMs  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        ThemeDurationMs    = MatchConfig.ThemePhaseDurationMs,
                        QuestionDurationMs = MatchConfig.QuestionPhaseDurationMs,
                    });
                    break;
                }
            }
        }

        // ----------------------------------------------------------
        // Carregamento de perguntas reais para modo stub
        // ----------------------------------------------------------

        private IEnumerator BuildStubRoundPool()
        {
            _stubRoundPool = null;

            // Garante que o DeckService existe mesmo quando a cena Match é iniciada diretamente
            if (DeckService.Instance == null)
            {
                var go = new GameObject("DeckService");
                go.AddComponent<DeckService>();
                yield return null; // deixa o Awake/Start rodar
            }

            var deckService = DeckService.Instance;
            if (deckService == null)
            {
                Debug.LogWarning("[MatchStateMachine] DeckService não disponível — usando pergunta fallback.");
                yield break;
            }

            // Aguarda DeckIndex ficar disponível (máx 8s)
            float idxDeadline = Time.time + 8f;
            while (deckService.GetAvailableCategories().Count == 0 && Time.time < idxDeadline)
                yield return new WaitForSeconds(0.3f);

            var allCategories = deckService.GetAvailableCategories();
            if (allCategories.Count == 0)
            {
                Debug.LogWarning("[MatchStateMachine] DeckIndex vazio após espera — usando pergunta fallback.");
                yield break;
            }

            Debug.Log($"[MatchStateMachine] Categorias disponíveis: {string.Join(", ", allCategories)}");

            // Aguarda PlayerDataService carregar o perfil (máx 3s)
            if (PlayerDataService.Instance == null)
            {
                new GameObject("PlayerDataService").AddComponent<PlayerDataService>();
                yield return null;
            }
            float profileDeadline = Time.time + 3f;
            while (PlayerDataService.Instance?.CurrentProfile == null && Time.time < profileDeadline)
                yield return new WaitForSeconds(0.3f);

            // Resolve categorias do jogador: tenta pelo suffix do id, depois pelo campo category
            var profile    = PlayerDataService.Instance?.CurrentProfile;
            var ownedDecks = profile?.decks?.FindAll(d => d.isOwned);
            var categorias = new List<string>();

            if (ownedDecks != null && ownedDecks.Count > 0)
            {
                foreach (var deck in ownedDecks)
                {
                    // Tenta 1: "deckHistoria" → suffix "Historia"
                    var suffix = deck.id.StartsWith("deck", StringComparison.OrdinalIgnoreCase)
                        ? deck.id.Substring(4) : deck.id;
                    var match = allCategories.Find(c =>
                        string.Equals(c, suffix, StringComparison.OrdinalIgnoreCase));

                    // Tenta 2: compara com o campo category do perfil (ex: "HISTÓRIA")
                    if (match == null && !string.IsNullOrEmpty(deck.category))
                        match = allCategories.Find(c =>
                            string.Equals(c, deck.category, StringComparison.OrdinalIgnoreCase));

                    if (match != null && !categorias.Contains(match))
                    {
                        categorias.Add(match);
                        Debug.Log($"[MatchStateMachine] Deck '{deck.id}' → categoria '{match}'");
                    }
                    else if (match == null)
                    {
                        Debug.LogWarning($"[MatchStateMachine] Deck '{deck.id}' não mapeado. Categorias: {string.Join(", ", allCategories)}");
                    }
                }
            }

            // Fallback: usa todas as categorias disponíveis
            if (categorias.Count == 0)
            {
                Debug.Log("[MatchStateMachine] Nenhum deck do jogador mapeado — carregando todas as categorias.");
                categorias.AddRange(allCategories);
            }

            // Dispara carregamento dos decks ainda não cacheados
            foreach (var cat in categorias)
            {
                if (!deckService.IsDeckLoaded(cat))
                    deckService.LoadDeck(cat);
            }

            // Aguarda carregamento (máx 8s).
            // Usa IsDeckLoaded para não disparar erros em decks que não têm TitleData.
            // Após o deadline coleta o que carregou, sem travar por categorias inexistentes.
            float deckDeadline = Time.time + 8f;
            while (Time.time < deckDeadline)
            {
                bool allLoaded = true;
                foreach (var cat in categorias)
                {
                    if (!deckService.IsDeckLoaded(cat)) { allLoaded = false; break; }
                }
                if (allLoaded) break;
                yield return new WaitForSeconds(0.5f);
            }

            // Diagnóstico pós-espera
            foreach (var cat in categorias)
                Debug.Log($"[MatchStateMachine] Deck '{cat}': {(deckService.IsDeckLoaded(cat) ? deckService.GetDeck(cat)?.Count + " perguntas" : "não carregado")}");


            // Coleta todas as perguntas disponíveis.
            // ThemeName vem de carta.categoria (= deckPayload.theme do TitleData, sem acentos)
            // em vez do nome da categoria do DeckIndex (que pode ter acentos).
            var pool = new List<StubRoundData>();
            foreach (var cat in categorias)
            {
                var deck = deckService.GetDeck(cat);
                if (deck == null) continue;
                foreach (var carta in deck)
                {
                    var theme = !string.IsNullOrEmpty(carta.categoria) ? carta.categoria : cat;
                    pool.Add(new StubRoundData { ThemeName = theme, Carta = carta });
                }
            }

            if (pool.Count == 0)
            {
                Debug.LogWarning("[MatchStateMachine] Pool de perguntas vazio após carregamento — usando pergunta fallback.");
                yield break;
            }

            // Embaralha Fisher-Yates
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            int rounds = MatchConfig.MaxRounds;
            _stubRoundPool = new StubRoundData[rounds];
            for (int i = 0; i < rounds; i++)
                _stubRoundPool[i] = pool[i % pool.Count];

            Debug.Log($"[MatchStateMachine] Pool stub pronto: {rounds} rodadas, {pool.Count} perguntas de {categorias.Count} deck(s).");
        }

        // ----------------------------------------------------------
        // Input do jogador (chamado pela UI)
        // ----------------------------------------------------------

        public void SubmitAnswer(string answerId)
        {
            if (Phase != MatchPhase.Question) return;
            if (Context.HasAnsweredThisRound) return;

            Context.SubmitAnswer(answerId);

            if (Context.IsStubMode) return;

            // Envia ao servidor via CloudScript (autoritativo)
            CloudScriptClient.Call("SubmitAnswer", new SubmitAnswerRequest
            {
                MatchId           = Context.MatchId,
                RoundNumber       = Context.CurrentRound,
                AnswerId          = answerId,
                ClientTimestampMs = Context.AnswerTimestampMs
            }, onSuccess: _ =>
            {
                // O resultado da rodada NÃO é processado aqui.
                // Ambos os jogadores aguardam o timer expirar → TriggerProcessRound() garante
                // que o Reveal aparece ao mesmo tempo para os dois.
            });
        }

        public void ActivatePowerUp(PowerUpType type)
        {
            if (Phase != MatchPhase.ThemeAndPowerUp) return;
            if (Context.LocalPlayer == null || Context.LocalPlayer.HasUsedPowerUp) return;

            Context.ActivatePowerUp(type);

            if (Context.IsStubMode) return;

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
        // Helpers
        // ----------------------------------------------------------

        private static PowerUpType ResolveEquippedPowerUp()
        {
            var raw = PlayerDataService.Instance?.CurrentProfile?.equippedPowerUp;
            if (!string.IsNullOrWhiteSpace(raw) &&
                Enum.TryParse<PowerUpType>(raw, ignoreCase: true, out var type) &&
                type != PowerUpType.None)
            {
                return type;
            }
            return PowerUpType.None;
        }

        // ----------------------------------------------------------
        // Processamento de rodada por timeout (chamado pelo UI quando o timer esgota)
        // ----------------------------------------------------------

        public void TriggerProcessRound()
        {
            if (Context.IsStubMode || Phase != MatchPhase.Question) return;

            CloudScriptClient.Call("ProcessRound",
                new { matchId = Context.MatchId, roundNumber = Context.CurrentRound },
                onSuccess: result =>
                {
                    if (result == null) return;
                    try
                    {
                        // ProcessRound retorna o payload diretamente (PascalCase, sem wrapper "roundResult")
                        var json        = PlayFab.Json.PlayFabSimpleJson.SerializeObject(result);
                        var roundResult = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<RoundResultPayload>(json);
                        // CorrectAnswerId vazio = rodada pendente ("pending") ou erro — não processa
                        if (roundResult != null && !string.IsNullOrEmpty(roundResult.CorrectAnswerId))
                            HandleRoundResult(roundResult);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Match] TriggerProcessRound parse falhou: {ex.Message}");
                    }
                },
                onError: err => Debug.LogWarning($"[Match] ProcessRound falhou: {err}"));
        }

        private RoundStartPayload ParsearRoundStart(object result, int roundNumber)
        {
            if (result == null) return null;
            try
            {
                var json = PlayFab.Json.PlayFabSimpleJson.SerializeObject(result);
                Debug.Log($"[Match] StartNextRound resposta: {json.Substring(0, Mathf.Min(300, json.Length))}");
                var dict = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<Dictionary<string, object>>(json);
                if (dict == null) return null;

                // Tenta camelCase (CloudScript JS) e PascalCase como fallback
                object tidRaw, tnRaw, tsRaw;
                dict.TryGetValue("themeId",   out tidRaw); if (tidRaw == null) dict.TryGetValue("ThemeId",   out tidRaw);
                dict.TryGetValue("themeName", out tnRaw);  if (tnRaw  == null) dict.TryGetValue("ThemeName", out tnRaw);
                dict.TryGetValue("serverTimestampMs", out tsRaw); if (tsRaw == null) dict.TryGetValue("ServerTimestampMs", out tsRaw);

                string themeId   = tidRaw != null ? tidRaw.ToString() : string.Empty;
                string themeName = tnRaw  != null ? tnRaw.ToString()  : themeId;
                if (string.IsNullOrEmpty(themeName)) themeName = themeId;

                long serverTs = 0;
                if (tsRaw != null) long.TryParse(tsRaw.ToString(), out serverTs);
                if (serverTs == 0) serverTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                return new RoundStartPayload
                {
                    RoundNumber        = roundNumber,
                    ThemeId            = themeId,
                    ThemeName          = themeName,
                    ServerTimestampMs  = serverTs,
                    ThemeDurationMs    = MatchConfig.ThemePhaseDurationMs,
                    QuestionDurationMs = MatchConfig.QuestionPhaseDurationMs,
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Match] Falha ao parsear RoundStart: {ex.Message}");
                return null;
            }
        }

        private RoundResultPayload TentarParsearRoundResult(object result)
        {
            if (result == null) return null;
            try
            {
                var json = PlayFab.Json.PlayFabSimpleJson.SerializeObject(result);
                var dict = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<Dictionary<string, object>>(json);
                if (dict == null || !dict.TryGetValue("roundResult", out var rr) || rr == null)
                    return null;
                var rrJson = PlayFab.Json.PlayFabSimpleJson.SerializeObject(rr);
                return PlayFab.Json.PlayFabSimpleJson.DeserializeObject<RoundResultPayload>(rrJson);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Match] Falha ao parsear roundResult: {ex.Message}");
                return null;
            }
        }

        // ----------------------------------------------------------
        // Inicializa ServerState a partir do retorno de StartNextRound
        // ----------------------------------------------------------

        private void InicializarServerStateDoRetorno(object result)
        {
            try
            {
                // PlayFab retorna FunctionResult como objeto — re-serializa para parsear campos
                var json = PlayFab.Json.PlayFabSimpleJson.SerializeObject(result);
                var dict = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<
                    Dictionary<string, object>>(json);

                if (dict == null) return;

                string p1Id = dict.TryGetValue("player1Id", out var v1) ? v1?.ToString() : string.Empty;
                string p2Id = dict.TryGetValue("player2Id", out var v2) ? v2?.ToString() : string.Empty;

                PlayerMatchState BuildState(string pid, object stateObj)
                {
                    var ps = new PlayerMatchState { PlayerId = pid, HP = MatchConfig.InitialHP, IsConnected = true };
                    if (stateObj == null) return ps;
                    var sj = PlayFab.Json.PlayFabSimpleJson.SerializeObject(stateObj);
                    var sd = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<Dictionary<string, object>>(sj);
                    if (sd == null) return ps;
                    ps.DisplayName = sd.TryGetValue("DisplayName", out var dn) ? dn?.ToString() : string.Empty;
                    if (sd.TryGetValue("Level", out var lv) && lv != null)
                        int.TryParse(lv.ToString(), out ps.Level);
                    return ps;
                }

                dict.TryGetValue("player1State", out var s1Raw);
                dict.TryGetValue("player2State", out var s2Raw);

                Context.ServerState = new ServerMatchState
                {
                    MatchId      = Context.MatchId,
                    Player1Id    = p1Id,
                    Player2Id    = p2Id,
                    CurrentRound = 1,
                    IsActive     = true,
                    Player1State = BuildState(p1Id, s1Raw),
                    Player2State = BuildState(p2Id, s2Raw),
                };

                Debug.Log($"[Match] ServerState inicial: P1={Context.ServerState.Player1State.DisplayName} P2={Context.ServerState.Player2State.DisplayName}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Match] Falha ao parsear ServerState inicial: {ex.Message}");
            }
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

            // Mantém CurrentRound sincronizado — sem isso StartQuestion envia roundNumber errado
            if (Context.ServerState != null)
                Context.ServerState.CurrentRound = p.RoundNumber;

            OnRoundStarted?.Invoke(p);
            TransitionTo(MatchPhase.ThemeAndPowerUp);
        }

        // Resposta correta da rodada atual (apenas em stub mode — usada pelo EliminateTwo)
        public string CurrentStubCorrectAnswerId { get; private set; }

        // Chamado pelo ThemeAndPowerUpState após receber a resposta do StartQuestion
        public void ReceiveQuestionReveal(QuestionRevealPayload payload) =>
            HandleQuestionReveal(payload);

        // Chamado pelos estados (RoundEndState) após receber a resposta do StartNextRound
        public void ReceiveRoundStart(RoundStartPayload payload) =>
            HandleRoundStart(payload);

        // Chamado pelo QuestionState após receber o resultado de ProcessRound diretamente
        public void HandleRoundResultFromState(RoundResultPayload payload) =>
            HandleRoundResult(payload);

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

            var localResult    = Context.GetLocalResult(p);
            var opponentResult = Context.GetOpponentResult(p);

            // No modo real, o ServerState pode não ter sido inicializado pelo cliente —
            // cria um estado mínimo a partir do payload para que HP e nomes funcionem.
            if (Context.ServerState == null)
            {
                Context.ServerState = new ServerMatchState
                {
                    MatchId      = Context.MatchId,
                    Player1Id    = localResult.PlayerId,
                    Player2Id    = opponentResult.PlayerId,
                    CurrentRound = p.RoundNumber,
                    IsActive     = !p.IsMatchOver,
                    Player1State = new PlayerMatchState { PlayerId = localResult.PlayerId },
                    Player2State = new PlayerMatchState { PlayerId = opponentResult.PlayerId },
                };
            }

            // Atualiza HP e streak a partir do resultado autoritativo do servidor
            Context.LocalPlayer.HP       = localResult.HPAfter;
            Context.LocalPlayer.Streak   = localResult.StreakAfter;
            Context.OpponentPlayer.HP    = opponentResult.HPAfter;
            Context.OpponentPlayer.Streak = opponentResult.StreakAfter;

            OnHPUpdated?.Invoke(Context.LocalHP, Context.OpponentHP);
            OnRoundResultReceived?.Invoke(p);

            TransitionTo(MatchPhase.Reveal);
        }

        // Chamado pelo MatchEndState para notificar a UI quando a partida termina
        // por HP zerado ou 20 rodadas (sem payload vindo da rede).
        public void NotifyMatchEnded(MatchEndPayload payload)
        {
            OnMatchEnded?.Invoke(payload);
        }

        private void HandleMatchEnd(MatchEndPayload p)
        {
            Context.LastRoundResult = null;
            OnMatchEnded?.Invoke(p);
            TransitionTo(MatchPhase.MatchEnd);
        }

        private void HandlePowerUpActivated(PowerUpActivatedPayload p)
        {
            OnPowerUpActivatedReceived?.Invoke(p);
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

        // Chamado pelo AbandonarPartidaModal antes de ir para a tela de derrota
        public void NotificarAbandono()
        {
            PartyNetworkManager.Instance?.Broadcast(MessageType.OpponentAbandoned, new { });
        }

        // Para a partida imediatamente, dispara OnMatchEnded e transiciona para MatchEnd.
        // Garante que stub conductor e timers não continuem após o abandono.
        public void ForcarFimDePartida(MatchEndPayload payload)
        {
            if (_stubConductorCoroutine != null)
            {
                StopCoroutine(_stubConductorCoroutine);
                _stubConductorCoroutine = null;
            }
            // Impede MatchEndState de disparar OnMatchEnded uma segunda vez
            Context.LastRoundResult = null;
            OnMatchEnded?.Invoke(payload);
            TransitionTo(MatchPhase.MatchEnd);
        }

        private void HandleOpponentAbandoned(string playerId)
        {
            // Oponente abandonou — encerra a partida localmente com vitória
            HandleMatchEnd(new MatchEndPayload
            {
                WinnerId          = Context.LocalPlayerId,
                WinnerHP          = Context.LocalHP,
                LoserHP           = 0,
                Reason            = MatchEndReason.Abandonment,
                TotalRoundsPlayed = Context.CurrentRound,
            });

            // Garante que o servidor finalize e processe as estatísticas do vencedor
            if (!Context.IsStubMode)
            {
                CloudScriptClient.Call("FinalizeMatch",
                    new { matchId = Context.MatchId, winnerId = Context.LocalPlayerId },
                    onSuccess: _ => { },
                    onError:   err => Debug.LogWarning($"[Match] FinalizeMatch pós-abandono: {err}"));
            }
        }
    }
}
