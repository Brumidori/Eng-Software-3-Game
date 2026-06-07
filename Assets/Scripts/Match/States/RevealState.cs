// ============================================================
// RevealState.cs — exibe o resultado da rodada por 3 segundos
// e depois avança para RoundEnd (ou MatchEnd se partida acabou).
// ============================================================
using UnityEngine;
using BrainDuel.Match.Core;
using BrainDuel.Match;
using BrainDuel.Network;

namespace BrainDuel.Match.States
{
    public class RevealState : BaseMatchState
    {
        private float _elapsed;

        public RevealState(MatchContext ctx, MatchStateMachine machine)
            : base(ctx, machine) { }

        public override void OnEnter()
        {
            _elapsed = 0f;

            var result = Context.LastRoundResult;
            if (result == null) return;

            Debug.Log($"[State] Reveal | Round {result.RoundNumber} | " +
                      $"P1 HP={result.Player1HP} P2 HP={result.Player2HP} | " +
                      $"Match over={result.IsMatchOver}");

            MatchEvents.NotifyRoundReveal(result);
        }

        public override void OnUpdate(float deltaTime)
        {
            _elapsed += deltaTime;
            float duration = MatchConfig.RevealPhaseDurationMs / 1000f;

            if (_elapsed >= duration)
            {
                var result = Context.LastRoundResult;
                if (result != null && result.IsMatchOver)
                    Machine.TransitionTo(MatchPhase.MatchEnd);
                else
                    Machine.TransitionTo(MatchPhase.RoundEnd);
            }
        }

        public override void OnExit() { }
    }

    // ============================================================
    // RoundEndState.cs — pausa curta antes da próxima rodada.
    // Solicita ao servidor que inicie a próxima rodada.
    // ============================================================
    public class RoundEndState : BaseMatchState
    {
        private float     _elapsed;
        private bool      _nextRoundRequested;
        private Coroutine _retryCoroutine;

        private const int MaxAttempts = 10;

        public RoundEndState(MatchContext ctx, MatchStateMachine machine)
            : base(ctx, machine) { }

        public override void OnEnter()
        {
            _elapsed            = 0f;
            _nextRoundRequested = false;
            Debug.Log($"[State] RoundEnd | Round {Context.CurrentRound} | próxima={Context.CurrentRound + 1}");
        }

        public override void OnUpdate(float deltaTime)
        {
            if (_nextRoundRequested) return;

            _elapsed += deltaTime;
            float duration = MatchConfig.RoundEndPhaseDurationMs / 1000f;

            if (_elapsed >= duration)
            {
                _nextRoundRequested = true;
                RequestNextRound();
            }
        }

        public override void OnExit()
        {
            if (_retryCoroutine != null)
            {
                Machine.StopCoroutine(_retryCoroutine);
                _retryCoroutine = null;
            }
        }

        private void RequestNextRound()
        {
            if (Context.IsStubMode) return;
            int nextRound = Context.CurrentRound + 1;
            Debug.Log($"[State] StartNextRound → round={nextRound}");
            TryStartNextRound(nextRound, 0);
        }

        private void TryStartNextRound(int nextRound, int attempt)
        {
            CloudScriptClient.Call("StartNextRound", new
            {
                matchId     = Context.MatchId,
                roundNumber = nextRound
            }, onSuccess: result =>
            {
                if (result == null)
                {
                    Debug.LogWarning($"[State] StartNextRound retornou null (tentativa {attempt + 1})");
                    ScheduleRetry(nextRound, attempt);
                    return;
                }
                try
                {
                    var json = PlayFab.Json.PlayFabSimpleJson.SerializeObject(result);
                    var dict = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<
                        System.Collections.Generic.Dictionary<string, object>>(json);
                    if (dict == null) { ScheduleRetry(nextRound, attempt); return; }

                    if (dict.ContainsKey("error"))
                    {
                        Debug.LogError($"[State] StartNextRound erro: {dict["error"]} | round={nextRound}");
                        return;
                    }

                    // Gate "ambos prontos": oponente ainda não confirmou o Reveal
                    if (dict.TryGetValue("status", out var st) && st?.ToString() == "waiting")
                    {
                        Debug.Log($"[State] StartNextRound: aguardando oponente (tentativa {attempt + 1})…");
                        ScheduleRetry(nextRound, attempt);
                        return;
                    }

                    string themeId   = dict.TryGetValue("themeId",   out var tid) ? tid?.ToString() : string.Empty;
                    string themeName = dict.TryGetValue("themeName", out var tn)  ? tn?.ToString()  : themeId;
                    long   serverTs  = 0;
                    if (dict.TryGetValue("serverTimestampMs", out var ts) && ts != null)
                        long.TryParse(ts.ToString(), out serverTs);
                    if (serverTs == 0)
                        serverTs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    QuestionRevealPayload cachedQuestion = null;
                    if (dict.TryGetValue("question", out var qRaw) && qRaw != null)
                    {
                        try
                        {
                            var qJson = PlayFab.Json.PlayFabSimpleJson.SerializeObject(qRaw);
                            cachedQuestion = PlayFab.Json.PlayFabSimpleJson
                                .DeserializeObject<QuestionRevealPayload>(qJson);
                            if (cachedQuestion != null)
                            {
                                cachedQuestion.ServerTimestampMs = serverTs + MatchConfig.ThemePhaseDurationMs;
                                cachedQuestion.DurationMs        = MatchConfig.QuestionPhaseDurationMs;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[State] StartNextRound: falha ao parsear pergunta embutida: {ex.Message}");
                        }
                    }

                    if (cachedQuestion == null)
                        Debug.LogWarning($"[State] StartNextRound round={nextRound}: pergunta embutida ausente — fallback para StartQuestion.");

                    Machine.ReceiveRoundStart(new RoundStartPayload
                    {
                        RoundNumber        = nextRound,
                        ThemeId            = themeId,
                        ThemeName          = themeName,
                        ServerTimestampMs  = serverTs,
                        ThemeDurationMs    = MatchConfig.ThemePhaseDurationMs,
                        QuestionDurationMs = MatchConfig.QuestionPhaseDurationMs,
                        CachedQuestion     = cachedQuestion,
                    });
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[State] Falha ao parsear StartNextRound: {ex.Message}");
                    ScheduleRetry(nextRound, attempt);
                }
            }, onError: err =>
            {
                Debug.LogError($"[State] StartNextRound falhou: {err}");
                ScheduleRetry(nextRound, attempt);
            });
        }

        private void ScheduleRetry(int nextRound, int attempt)
        {
            if (attempt + 1 < MaxAttempts)
                _retryCoroutine = Machine.StartCoroutine(RetryAfterDelay(nextRound, attempt + 1));
            else
                Debug.LogError($"[State] StartNextRound: {MaxAttempts} tentativas falharam — partida pode estar travada.");
        }

        private System.Collections.IEnumerator RetryAfterDelay(int nextRound, int attempt)
        {
            yield return new UnityEngine.WaitForSeconds(1.5f);
            _retryCoroutine = null;
            if (Machine.Phase == MatchPhase.RoundEnd)
                TryStartNextRound(nextRound, attempt);
        }
    }
}
