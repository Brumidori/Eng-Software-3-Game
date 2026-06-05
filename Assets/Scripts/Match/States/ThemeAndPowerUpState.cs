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

        // Solicita ao servidor que revele a pergunta e entrega ao cliente
        private void RequestQuestion()
        {
            if (Context.IsStubMode) return;

            CloudScriptClient.Call("StartQuestion", new
            {
                matchId     = Context.MatchId,
                roundNumber = Context.CurrentRound
            }, onSuccess: result =>
            {
                if (result == null) { Debug.LogError("[State] StartQuestion retornou null"); return; }
                try
                {
                    var json    = PlayFab.Json.PlayFabSimpleJson.SerializeObject(result);
                    var payload = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<QuestionRevealPayload>(json);

                    if (payload == null || string.IsNullOrEmpty(payload.QuestionText))
                    {
                        Debug.LogError($"[State] StartQuestion: payload inválido — {json}");
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
                Debug.LogError($"[State] StartQuestion falhou: {err}");
            });
        }
    }
}
