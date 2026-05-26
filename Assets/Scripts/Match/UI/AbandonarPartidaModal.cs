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

            // Notifica servidor — CloudScript aplica penalidade ao perdedor
            // e concede 40 moedas + 20 XP ao vencedor
            if (stateMachine?.Context != null)
            {
                CloudScriptClient.Call("AbandonMatch", new
                {
                    matchId  = stateMachine.Context.MatchId,
                    playerId = stateMachine.Context.LocalPlayerId
                }, onSuccess: _ =>
                {
                    Debug.Log("[Modal] AbandonMatch confirmado pelo servidor.");
                }, onError: err =>
                {
                    Debug.LogWarning($"[Modal] Erro ao notificar abandono: {err}");
                });
            }

            // Registra derrota e penalidade de XP no cliente
            StatisticsService.Instance?.UpdateMatchStatistics(-10, wonMatch: false);

            // Mostra tela de derrota sem navegar — jogador clica em Menu ou Outra Partida
            sceneController?.MostrarDerrotaPorAbandono();
        }
    }
}
