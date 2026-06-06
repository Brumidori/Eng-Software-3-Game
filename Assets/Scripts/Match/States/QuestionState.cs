// ============================================================
// QuestionState.cs — fase de 20 segundos onde a pergunta é
// exibida.  Ao fim do timer (via servidor), chama ProcessRound.
// Aplica efeitos de power-up na UI (EliminateTwo).
// ============================================================
using UnityEngine;
using BrainDuel.Match.Core;
using BrainDuel.Match;
using BrainDuel.Network;

namespace BrainDuel.Match.States
{
    public class QuestionState : BaseMatchState
    {
        private bool  _roundProcessRequested;
        private float _pollTimer;
        private const float PollIntervalSeconds = 2f;

        public QuestionState(MatchContext ctx, MatchStateMachine machine)
            : base(ctx, machine) { }

        public override void OnEnter()
        {
            _roundProcessRequested = false;
            _pollTimer             = 0f;
            Debug.Log($"[State] Question | Round {Context.CurrentRound}");
        }

        public override void OnUpdate(float deltaTime)
        {
            if (_roundProcessRequested) return;

            if (Context.RemainingSeconds <= 0f)
            {
                _roundProcessRequested = true;
                RequestProcessRound();
                return;
            }

            // Polling: após responder, verifica periodicamente se o oponente já respondeu
            // (substitui o broadcast via Party que não funciona no WebGL)
            if (!Context.IsStubMode && Context.HasAnsweredThisRound)
            {
                _pollTimer += deltaTime;
                if (_pollTimer >= PollIntervalSeconds)
                {
                    _pollTimer = 0f;
                    Machine.TriggerProcessRound();
                }
            }
        }

        public override void OnExit() { }

        // Chamado pela MatchStateMachine quando recebe PowerUpActivated do oponente
        public void OnPowerUpActivated(PowerUpActivatedPayload payload)
        {
            if (payload.PowerUp == PowerUpType.EliminateTwo && payload.EliminatedIndices != null)
            {
                // Notifica UI para desativar as opções eliminadas
                MatchEvents.NotifyEliminateTwo(payload.EliminatedIndices);
            }
        }

        // Solicita ao servidor que processe a rodada (idempotente)
        // Ambos os clientes chamam — servidor processa apenas uma vez
        // Em modo stub o StubConductor já cuida da transição, então não chama o servidor
        private void RequestProcessRound()
        {
            if (Context.IsStubMode) return;

            CloudScriptClient.Call("ProcessRound", new
            {
                matchId     = Context.MatchId,
                roundNumber = Context.CurrentRound
            }, onSuccess: result =>
            {
                // RoundResult chega via Party broadcast pelo servidor
                Debug.Log("[State] ProcessRound solicitado");
            }, onError: err =>
            {
                // Retry em 1s
                Machine.StartCoroutine(RetryAfterDelay(1f));
                Debug.LogWarning($"[State] ProcessRound falhou — retentando: {err}");
            });
        }

        private System.Collections.IEnumerator RetryAfterDelay(float seconds)
        {
            yield return new UnityEngine.WaitForSeconds(seconds);
            RequestProcessRound();
        }
    }
}
