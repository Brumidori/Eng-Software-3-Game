// ============================================================
// ThemeAndPowerUpState.cs — fase de 4 segundos onde o tema é
// exibido e o jogador pode ativar seu power-up.
// Ao fim, chama CloudScript StartQuestion para que o servidor
// revele a pergunta de forma idempotente.
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

        // Solicita ao servidor que revele a pergunta e entrega ao cliente.
        // Retry automático se o servidor retornar estado inválido (ex: round ainda não salvo).
        private void RequestQuestion() => TryGetQuestion(0);

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
                        Debug.LogWarning($"[State] StartQuestion: payload inválido (tentativa {attempt + 1}) — {json}");
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
    }
}
