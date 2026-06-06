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
        private bool _roundProcessRequested;

        public QuestionState(MatchContext ctx, MatchStateMachine machine)
            : base(ctx, machine) { }

        public override void OnEnter()
        {
            _roundProcessRequested = false;
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

            // Não há polling antecipado: o servidor retorna "pending" antes do timer expirar.
            // Ambos os jogadores aguardam o timer → TriggerProcessRound() cuida do Reveal sincronizado.
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

        // Solicita ao servidor que processe a rodada (idempotente).
        // Ambos os clientes chamam — servidor processa apenas uma vez.
        // Resultado tratado diretamente: sem dependência do Party SDK (não suportado em WebGL).
        private void RequestProcessRound()
        {
            if (Context.IsStubMode) return;

            CloudScriptClient.Call("ProcessRound", new
            {
                matchId     = Context.MatchId,
                roundNumber = Context.CurrentRound
            }, onSuccess: result =>
            {
                if (result == null) { Machine.StartCoroutine(RetryAfterDelay(1f)); return; }
                try
                {
                    var json        = PlayFab.Json.PlayFabSimpleJson.SerializeObject(result);
                    var roundResult = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<
                        RoundResultPayload>(json);

                    if (roundResult != null && !string.IsNullOrEmpty(roundResult.CorrectAnswerId))
                    {
                        Debug.Log("[State] ProcessRound: resultado recebido — avançando.");
                        Machine.HandleRoundResultFromState(roundResult);
                    }
                    else
                    {
                        // Servidor ainda não processou (timer ligeiramente dessincronizado).
                        // Retenta em 1s para não travar caso o UI timer já tenha disparado junto.
                        Debug.Log("[State] ProcessRound pendente — retentando em 1s.");
                        Machine.StartCoroutine(RetryAfterDelay(1f));
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[State] ProcessRound: erro ao parsear resultado: {ex.Message}");
                }
            }, onError: err =>
            {
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
