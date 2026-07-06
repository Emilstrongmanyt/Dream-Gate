using System;
using System.Collections.Generic;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Core;

namespace DreamGate.Battlegrounds.Players
{
    [Serializable]
    public class PlayerState
    {
        public int playerId;
        public string displayName;
        public string heroId;
        public string heroName;
        public bool isHuman;
        public bool isEliminated;
        public int placement;

        public int heroHealth = MatchConfig.StartingHeroHealth;
        public int damageDealt;
        public int damageTaken;
        public int gold;
        public int tavernTier = MatchConfig.StartingTavernTier;

        public List<MinionInstance> board = new();
        public List<MinionInstance> hand = new();
        public List<string> shopCardIds = new();

        public List<MinionInstance> GetLivingBoard()
        {
            return board.FindAll(m => m != null && !m.isDead);
        }

        public int BoardCount => board.Count;
        public bool BoardFull => board.Count >= MatchConfig.BoardSize;
        public bool HandFull => hand.Count >= MatchConfig.MaxHandSize;

        public int CountCopies(string cardId, bool includeGolden = false)
        {
            var count = 0;
            foreach (var minion in board)
            {
                if (minion.cardId == cardId && (includeGolden || !minion.isGolden))
                {
                    count++;
                }
            }

            foreach (var minion in hand)
            {
                if (minion.cardId == cardId && (includeGolden || !minion.isGolden))
                {
                    count++;
                }
            }

            return count;
        }
    }
}