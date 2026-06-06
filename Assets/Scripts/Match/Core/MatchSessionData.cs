namespace BrainDuel.Match.Core
{
    public static class MatchSessionData
    {
        public static string MatchId          { get; set; }
        public static string LocalPlayerId    { get; set; }
        public static string LocalDisplayName { get; set; } = "Você";
        public static int    LocalLevel       { get; set; } = 1;
        // false = modo stub (bot local); true = partida real com oponente via CloudScript
        public static bool   IsRealMatch      { get; set; } = false;

        public static void Clear()
        {
            MatchId          = null;
            LocalPlayerId    = null;
            LocalDisplayName = "Você";
            LocalLevel       = 1;
            IsRealMatch      = false;
        }
    }
}
