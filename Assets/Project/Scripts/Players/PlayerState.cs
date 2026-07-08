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

        public MinionInstance[] board = new MinionInstance[MatchConfig.BoardSize];
        public List<MinionInstance> hand = new();
        public List<string> shopCardIds = new();
        public bool doomNextCombat;

        public List<MinionInstance> GetLivingBoard()
        {
            var living = new List<MinionInstance>();
            foreach (var minion in board)
            {
                if (minion != null && !minion.isDead)
                {
                    living.Add(minion);
                }
            }

            return living;
        }

        public int BoardCount
        {
            get
            {
                var count = 0;
                foreach (var minion in board)
                {
                    if (minion != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool BoardFull => BoardCount >= MatchConfig.BoardSize;
        public bool HandFull => hand.Count >= MatchConfig.MaxHandSize;

        public int FindEmptyBoardSlot()
        {
            for (var i = 0; i < board.Length; i++)
            {
                if (board[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        private static readonly int[] DefaultPlaySlotOrder = { 2, 1, 3, 0, 4 };

        public int GetDefaultPlaySlot()
        {
            foreach (var slot in DefaultPlaySlotOrder)
            {
                if (slot >= 0 && slot < board.Length && board[slot] == null)
                {
                    return slot;
                }
            }

            return FindEmptyBoardSlot();
        }

        public int CountCopies(string cardId, bool includeGolden = false)
        {
            var count = 0;
            foreach (var minion in board)
            {
                if (minion != null && minion.cardId == cardId && (includeGolden || !minion.isGolden))
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