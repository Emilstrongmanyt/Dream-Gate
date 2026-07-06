using System.Collections.Generic;

namespace DreamGate.Battlegrounds.Core
{
    public class MatchResult
    {
        public MatchMode matchMode;
        public bool playerWon;
        public int placement;
        public int turnsPlayed;
        public int finalHeroHealth;
        public int damageDealt;
        public int damageTaken;
        public string heroName;
        public int mmrBefore;
        public int mmrAfter;
        public int mmrDelta;
        public readonly List<string> eliminationOrder = new();
    }
}