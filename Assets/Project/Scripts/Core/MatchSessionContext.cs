namespace DreamGate.Battlegrounds.Core
{
    /// <summary>
    /// Carries match configuration across scene loads (lobby → game).
    /// </summary>
    public static class MatchSessionContext
    {
        public static MatchMode Mode { get; private set; } = MatchMode.Practice;
        public static int MatchSeed { get; private set; } = -1;
        public static string LobbyId { get; private set; }
        public static int HumanPlayerMmr { get; private set; } = 1500;

        public static void BeginPractice()
        {
            Mode = MatchMode.Practice;
            MatchSeed = -1;
            LobbyId = null;
            HumanPlayerMmr = 1500;
        }

        public static void BeginRated(string lobbyId, int matchSeed, int playerMmr)
        {
            Mode = MatchMode.Rated;
            LobbyId = lobbyId;
            MatchSeed = matchSeed;
            HumanPlayerMmr = playerMmr;
        }

        public static void Clear()
        {
            Mode = MatchMode.Practice;
            MatchSeed = -1;
            LobbyId = null;
            HumanPlayerMmr = 1500;
        }
    }
}