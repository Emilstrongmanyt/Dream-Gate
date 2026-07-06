using System;

namespace DreamGate.Battlegrounds.Services
{
    [Serializable]
    public class PlayerProfile
    {
        public const int DefaultMmr = 1500;

        public string playerId;
        public string email;
        public string displayName = "Dreamer";
        public int mmr = DefaultMmr;
        public int ratedGamesPlayed;
        public int wins;
        public int top4Finishes;
        public int highestMmr = DefaultMmr;
    }
}