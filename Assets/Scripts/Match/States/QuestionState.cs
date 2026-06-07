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
        private bool      _roundProcessRequested;
        // Guarda a referência da corrotina de retry para que OnExit possa cancelá-la.
        // Sem isso, um retry de uma rodada anterior pode acordar durante a fase
        // Question da rodada seguinte e chamar ProcessRound com o round errado,
        // causando o bug "wrong_round".
        private Coroutine _retryCoroutine;

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

        public override void OnExit()
        {
            // Cancela qualquer corrotina de retry pendente ao sair da fase Question.
            // Isso evita que um retry de uma rodada anterior acorde depois que a
            // state machine já avançou para ThemeAndPowerUp ou Question da próxima
            // rodada, chamando ProcessRound com o número de rodada errado.
            if (_retryCoroutine != null)
            {
                Machine.StopCoroutine(_retryCoroutine);
                _retryCoroutine = null;
            }
        }

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

            // Captura o round no momento da chamada: o callback assíncrono pode
            // chegar depois que a state machine já avançou para outra rodada.
            int roundSnapshot = Context.CurrentRound;

            CloudScriptClient.Call("ProcessRound", new
            {
                matchId     = Context.MatchId,
                roundNumber = roundSnapshot
            }, onSuccess: result =>
            {
                // Se a máquina já avançou para outra rodada, ignora completamente.
                // Isso rompe a cadeia de retries órfãos que causam o wrong_round.
                if (Context.CurrentRound != roundSnapshot)
                {
                    Debug.Log($"[State] ProcessRound(round={roundSnapshot}) ignorado — rodada atual é {Context.CurrentRound}.");
                    return;
                }

                if (result == null)
                {
                    _retryCoroutine = Machine.StartCoroutine(RetryAfterDelay(1f, roundSnapshot));
                    return;
                }
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
                        _retryCoroutine = Machine.StartCoroutine(RetryAfterDelay(1f, roundSnapshot));
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[State] ProcessRound: erro ao parsear resultado: {ex.Message}");
                }
            }, onError: err =>
            {
                if (Context.CurrentRound == roundSnapshot)
                    _retryCoroutine = Machine.StartCoroutine(RetryAfterDelay(1f, roundSnapshot));
                Debug.LogWarning($"[State] ProcessRound falhou — retentando: {err}");
            });
        }

        // Parâmetro roundForWhich garante que o retry só executa se ainda
        // estivermos na mesma rodada que o gerou — proteção dupla além do OnExit.
        private System.Collections.IEnumerator RetryAfterDelay(float seconds, int roundForWhich)
        {
            yield return new UnityEngine.WaitForSeconds(seconds);
            _retryCoroutine = null;
            // Não retenta se a máquina já avançou para outra fase OU outra rodada.
            if (Machine.Phase == MatchPhase.Question && Context.CurrentRound == roundForWhich)
                RequestProcessRound();
        }
    }
}
