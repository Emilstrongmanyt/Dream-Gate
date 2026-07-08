namespace DreamGate.Battlegrounds.Services
{
    public class MatchmakingResult
    {
        public string lobbyId;
        public int matchSeed;
        public int playersFound;
        public int humanCount;
        public bool usedBotFill;
        public string matchServerUrl;
        public int humanSlotIndex;
        public MatchSlot[] slots = System.Array.Empty<MatchSlot>();
    }
}