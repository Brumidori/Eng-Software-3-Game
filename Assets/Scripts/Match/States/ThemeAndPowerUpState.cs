// ============================================================
// ThemeAndPowerUpState.cs — fase de tema/power-up.
// Usa a pergunta embutida no StartNextRound quando disponível,
// evitando a chamada separada ao StartQuestion.
// ============================================================
using System;
using UnityEngine;
using BrainDuel.Match.Core;
using BrainDuel.Match;
using BrainDuel.Network;

namespace BrainDuel.Match.States
{
    public class ThemeAndPowerUpState : BaseMatchState
    {
        private bool _questionRequested;

        public ThemeAndPowerUpState(MatchContext ctx, MatchStateMachine machine)
            : base(ctx, machine) { }

        public override void OnEnter()
        {
            _questionRequested = false;
            Debug.Log($"[State] ThemeAndPowerUp | Round {Context.CurrentRound} | Tema: {Context.ServerState?.CurrentRoundState?.ThemeName}");
        }

        public override void OnUpdate(float deltaTime)
        {
            if (_questionRequested) return;

            // Timer baseado em timestamp do servidor — cliente apenas renderiza
            if (Context.RemainingSeconds <= 0f)
            {
                _questionRequested = true;
                RequestQuestion();
            }
        }

        public override void OnExit() { }

        // Usa a pergunta embutida no StartNextRound/PlayerReady quando disponível,
        // evitando uma chamada separada ao StartQuestion.
        // Fallback: chama StartQuestion se a pergunta não veio na resposta de round start.
        private void RequestQuestion()
        {
            if (Context.CurrentQuestion != null)
            {
                var q = Context.CurrentQuestion;
                long questionStartMs = Context.PhaseStartServerMs + Context.PhaseDurationMs;
                Machine.ReceiveQuestionReveal(new QuestionRevealPayload
                {
                    QuestionId        = q.QuestionId,
                    QuestionText      = q.Text,
                    Answers           = q.Options != null
                        ? System.Array.ConvertAll(q.Options, o => new AnswerOption { Id = o.Id, Text = o.Text })
                        : null,
                    ServerTimestampMs = questionStartMs,
                    DurationMs        = MatchConfig.QuestionPhaseDurationMs,
                });
                return;
            }
            TryGetQuestion(0);
        }

        private const int MaxQuestionAttempts = 5;

        private void TryGetQuestion(int attempt)
        {
            if (Context.IsStubMode) return;

            int roundNumber = Context.CurrentRound;
            Debug.Log($"[State] StartQuestion → matchId={Context.MatchId} roundNumber={roundNumber} (tentativa {attempt + 1}/{MaxQuestionAttempts})");

            CloudScriptClient.Call("StartQuestion", new
            {
                matchId     = Context.MatchId,
                roundNumber = roundNumber
            }, onSuccess: result =>
            {
                if (result == null)
                {
                    Debug.LogWarning("[State] StartQuestion retornou null — retentando");
                    ScheduleRetry(attempt);
                    return;
                }
                try
                {
                    var json    = PlayFab.Json.PlayFabSimpleJson.SerializeObject(result);
                    var payload = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<QuestionRevealPayload>(json);

                    if (payload == null || string.IsNullOrEmpty(payload.QuestionText))
                    {
                        var dict = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(json);
                        if (dict != null && dict.ContainsKey("error") && dict["error"].ToString() == "Match inativo")
                        {
                            HandleInactiveMatch();
                            return;
                        }

                        // save_error: servidor não conseguiu salvar a transição de fase —
                        // retry faz com que o próximo jogador a chamar encontre a fase correta.
                        if (json.Contains("save_error"))
                        {
                            Debug.LogWarning($"[State] StartQuestion save_error (tentativa {attempt + 1}) — retentando");
                            ScheduleRetry(attempt);
                            return;
                        }
                        Debug.LogWarning($"[State] StartQuestion: payload inválido (tentativa {attempt + 1}) — {json}");
                        // round_mismatch com serverRound < clientRound:
                        // processRoundInternal atrasado sobrescreveu StartNextRound — re-avança o servidor
                        if (IsRoundBehind(json))
                            Machine.StartCoroutine(AdvanceRoundThenRetry(attempt));
                        else
                            ScheduleRetry(attempt);
                        return;
                    }

                    Debug.Log($"[State] Pergunta recebida: {payload.QuestionText}");
                    Machine.ReceiveQuestionReveal(payload);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[State] Falha ao parsear StartQuestion: {ex.Message}");
                }
            }, onError: err =>
            {
                Debug.LogWarning($"[State] StartQuestion falhou (tentativa {attempt + 1}): {err}");
                ScheduleRetry(attempt);
            });
        }

        private void ScheduleRetry(int attempt)
        {
            if (attempt + 1 < MaxQuestionAttempts)
                Machine.StartCoroutine(RetryAfterDelay(attempt + 1));
            else
                Debug.LogError($"[State] StartQuestion: {MaxQuestionAttempts} tentativas falharam. Partida pode estar travada.");
        }

        private System.Collections.IEnumerator RetryAfterDelay(int nextAttempt)
        {
            yield return new UnityEngine.WaitForSeconds(1f);
            TryGetQuestion(nextAttempt);
        }

        // Detecta round_mismatch onde serverRound < clientRound
        private bool IsRoundBehind(string json)
        {
            try
            {
                var dict = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<
                    System.Collections.Generic.Dictionary<string, object>>(json);
                if (dict == null) return false;
                if (!dict.TryGetValue("reason", out var reason) || reason?.ToString() != "round_mismatch")
                    return false;
                long serverRound = 0, clientRound = 0;
                if (dict.TryGetValue("serverRound", out var sr)) long.TryParse(sr?.ToString(), out serverRound);
                if (dict.TryGetValue("clientRound", out var cr)) long.TryParse(cr?.ToString(), out clientRound);
                return serverRound < clientRound;
            }
            catch { return false; }
        }

        // Chama StartNextRound para re-avançar o servidor e então retenta StartQuestion
        private System.Collections.IEnumerator AdvanceRoundThenRetry(int attempt)
        {
            int round = Context.CurrentRound;
            Debug.Log($"[State] StartNextRound recovery → round={round}");

            bool done = false;
            CloudScriptClient.Call("StartNextRound", new
            {
                matchId     = Context.MatchId,
                roundNumber = round
            }, onSuccess: _ => done = true,
               onError:   err =>
               {
                   Debug.LogWarning($"[State] StartNextRound recovery falhou: {err}");
                   done = true;
               });

            while (!done) yield return null;

            yield return new UnityEngine.WaitForSeconds(0.5f);
            TryGetQuestion(attempt);
        }
    }
}
