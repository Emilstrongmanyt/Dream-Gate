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
        public int losses;
        public int top4Finishes;
        public int currentWinStreak;
        public int bestWinStreak;
        public int totalDamageDealt;
        public int highestMmr = DefaultMmr;
        public string selectedHeroId = "hero_art_Warrior";
        public string unlockedHeroIdsCsv = "hero_art_Warrior";
        public int campaignHighestLevel;
    }
}