// ============================================================
// MatchEndState.cs — estado terminal da partida.
// Registra resultado no PlayFab e libera recursos.
// ============================================================
using UnityEngine;
using UnityEngine.SceneManagement;
using BrainDuel.Match.Core;
using BrainDuel.Match;
using BrainDuel.Match.Network;

namespace BrainDuel.Match.States
{
    public class MatchEndState : BaseMatchState
    {
        private bool _resultSaved;

        public MatchEndState(MatchContext ctx, MatchStateMachine machine)
            : base(ctx, machine) { }

        public override void OnEnter()
        {
            _resultSaved = false;
            Debug.Log($"[State] MatchEnd | Vencedor: {Context.LastRoundResult?.WinnerId ?? "N/A"}");

            SaveMatchResult();
        }

        public override void OnUpdate(float deltaTime) { }

        public override void OnExit()
        {
            PartyNetworkManager.Instance?.Disconnect();
        }

        private void SaveMatchResult()
        {
            if (_resultSaved) return;
            _resultSaved = true;

            CloudScriptClient.Call("FinalizeMatch", new
            {
                matchId    = Context.MatchId,
                winnerId   = Context.LastRoundResult?.WinnerId,
                localHP    = Context.LocalHP,
                opponentHP = Context.OpponentHP
            }, onSuccess: _ =>
            {
                Debug.Log("[Match] Resultado salvo com sucesso");
            }, onError: err =>
            {
                Debug.LogWarning($"[Match] Falha ao salvar resultado: {err}");
            });
        }

        public void NavigateToResult()
        {
            SceneManager.LoadScene("MatchResult");
        }
    }
}
