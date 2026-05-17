// ============================================================
// MatchEvents.cs — barramento de eventos estático para
// desacoplar a lógica de negócio da UI de partida.
// ============================================================
using System;
using BrainDuel.Network;

namespace BrainDuel.Match.Core
{
    public static class MatchEvents
    {
        public static event Action<RoundResultPayload> OnRoundReveal;
        public static event Action<int[]>              OnEliminateTwo;
        public static event Action<int, int>           OnHPChanged;    // (localHP, opponentHP)

        public static void NotifyRoundReveal(RoundResultPayload result) =>
            OnRoundReveal?.Invoke(result);

        public static void NotifyEliminateTwo(int[] indices) =>
            OnEliminateTwo?.Invoke(indices);

        public static void NotifyHPChanged(int localHP, int opponentHP) =>
            OnHPChanged?.Invoke(localHP, opponentHP);

        public static void ClearAllListeners()
        {
            OnRoundReveal  = null;
            OnEliminateTwo = null;
            OnHPChanged    = null;
        }
    }
}
