using UnityEngine;
using UnityEngine.UI;
using BrainDuel.Match.Core;
using BrainDuel.Match.UI;

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
                // 1. Notifica oponente via Party imediatamente (antes de qualquer atraso de rede)
                stateMachine.NotificarAbandono();

                // 2. Registra abandono no servidor — CloudScript finaliza a partida e
                //    processa XP/moedas do vencedor (oponente)
                CloudScriptClient.Call("AbandonMatch", new
                {
                    matchId = stateMachine.Context.MatchId
                }, onSuccess: _ =>
                {
                    Debug.Log("[Modal] AbandonMatch confirmado pelo servidor.");
                }, onError: err =>
                {
                    Debug.LogWarning($"[Modal] Erro ao notificar abandono: {err}");
                });
            }

            // 3. Registra derrota e penalidade de XP no cliente local
            StatisticsService.Instance?.UpdateMatchStatistics(-10, wonMatch: false);

            // 4. Mostra tela de derrota
            sceneController?.MostrarDerrotaPorAbandono();
        }
    }
}
