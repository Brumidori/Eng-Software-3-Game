// ============================================================
// BaseMatchState.cs — contrato de todos os estados da partida.
// ============================================================
using BrainDuel.Match.Core;

namespace BrainDuel.Match.States
{
    public abstract class BaseMatchState
    {
        protected MatchContext    Context    { get; }
        protected MatchStateMachine Machine  { get; }

        protected BaseMatchState(MatchContext ctx, MatchStateMachine machine)
        {
            Context = ctx;
            Machine = machine;
        }

        public abstract void OnEnter();
        public abstract void OnUpdate(float deltaTime);
        public abstract void OnExit();

        protected void HandleInactiveMatch()
        {
            UnityEngine.Debug.Log("[State] Match inativo detectado! Solicitando sincronização com servidor para finalizar a partida...");
            Machine.TriggerReconnectSync();
        }
    }
}
