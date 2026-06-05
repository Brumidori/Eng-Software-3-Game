using UnityEngine;
using UnityEngine.UI;
using BrainDuel.Match.Core;
using BrainDuel.Match.UI;
using BrainDuel.Match;

namespace BrainDuel.Match.UI
{
    public class AbandonarPartidaModal : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private GameObject       panelModal;
        [SerializeField] private Button           btnAbandonar;
        [SerializeField] private Button           btnConfirmar;
        [SerializeField] private Button           btnRetomar;
        [SerializeField] private MatchStateMachine    stateMachine;
        [SerializeField] private MatchSceneController sceneController;

        void Start()
        {
            panelModal.SetActive(false);

            btnAbandonar.onClick.AddListener(AbrirModal);
            btnConfirmar.onClick.AddListener(ConfirmarAbandono);
            btnRetomar.onClick.AddListener(FecharModal);
        }

        void OnDestroy()
        {
            btnAbandonar.onClick.RemoveListener(AbrirModal);
            btnConfirmar.onClick.RemoveListener(ConfirmarAbandono);
            btnRetomar.onClick.RemoveListener(FecharModal);
        }

        public void AbrirModal()  => panelModal.SetActive(true);
        public void FecharModal() => panelModal.SetActive(false);

        private void ConfirmarAbandono()
        {
            FecharModal();

            if (stateMachine?.Context != null)
            {
                // 1. Notifica oponente via Party imediatamente
                stateMachine.NotificarAbandono();

                // 2. Registra abandono no servidor
                CloudScriptClient.Call("AbandonMatch", new
                {
                    matchId = stateMachine.Context.MatchId
                }, onSuccess: _ => Debug.Log("[Modal] AbandonMatch confirmado."),
                   onError:   err => Debug.LogWarning($"[Modal] Erro ao notificar abandono: {err}"));

                // 3. Para a state machine imediatamente (impede que timers/estados
                //    sobrescrevam o painel de fim de partida após o abandono)
                var opponentId = stateMachine.Context.OpponentPlayer?.PlayerId
                                 ?? stateMachine.Context.ServerState?.Player2Id
                                 ?? "opponent";

                stateMachine.ForcarFimDePartida(new BrainDuel.Network.MatchEndPayload
                {
                    WinnerId          = opponentId,
                    WinnerHP          = stateMachine.Context.OpponentHP,
                    LoserHP           = 0,
                    Reason            = BrainDuel.Match.MatchEndReason.Abandonment,
                    TotalRoundsPlayed = stateMachine.Context.CurrentRound,
                });
            }

            // 4. Penalidade de XP no cliente (modo stub/offline)
            StatisticsService.Instance?.UpdateMatchStatistics(-10, wonMatch: false);
        }
    }
}
