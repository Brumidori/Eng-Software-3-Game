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
        private float _elapsed;
        private bool  _nextRoundRequested;

        public RoundEndState(MatchContext ctx, MatchStateMachine machine)
            : base(ctx, machine) { }

        public override void OnEnter()
        {
            _elapsed            = 0f;
            _nextRoundRequested = false;
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

        public override void OnExit() { }

        private void RequestNextRound()
        {
            if (Context.IsStubMode) return;

            int nextRound = Context.CurrentRound + 1;

            CloudScriptClient.Call("StartNextRound", new
            {
                matchId     = Context.MatchId,
                roundNumber = nextRound
            }, onSuccess: result =>
            {
                if (result == null) { Debug.LogError("[State] StartNextRound retornou null"); return; }
                try
                {
                    var json = PlayFab.Json.PlayFabSimpleJson.SerializeObject(result);
                    var dict = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<
                        System.Collections.Generic.Dictionary<string, object>>(json);
                    if (dict == null) return;

                    // Não transiciona se o servidor retornou erro (ex: Match inativo)
                    if (dict.ContainsKey("error"))
                    {
                        Debug.LogError($"[State] StartNextRound erro: {dict["error"]} | round={nextRound}");
                        return;
                    }

                    string themeId   = dict.TryGetValue("themeId",   out var tid) ? tid?.ToString() : string.Empty;
                    string themeName = dict.TryGetValue("themeName", out var tn)  ? tn?.ToString()  : themeId;
                    long   serverTs  = 0;
                    if (dict.TryGetValue("serverTimestampMs", out var ts) && ts != null)
                        long.TryParse(ts.ToString(), out serverTs);
                    if (serverTs == 0)
                        serverTs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    Machine.ReceiveRoundStart(new RoundStartPayload
                    {
                        RoundNumber        = nextRound,
                        ThemeId            = themeId,
                        ThemeName          = themeName,
                        ServerTimestampMs  = serverTs,
                        ThemeDurationMs    = MatchConfig.ThemePhaseDurationMs,
                        QuestionDurationMs = MatchConfig.QuestionPhaseDurationMs,
                    });
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[State] Falha ao parsear StartNextRound: {ex.Message}");
                }
            }, onError: err =>
            {
                Debug.LogError($"[State] StartNextRound falhou: {err}");
            });
        }
    }
}
