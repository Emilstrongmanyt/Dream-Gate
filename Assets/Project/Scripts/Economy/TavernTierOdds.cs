using System.Collections.Generic;
using DreamGate.Battlegrounds.Cards;

namespace DreamGate.Battlegrounds.Economy
{
    public static class TavernTierOdds
    {
        // BG-style shop odds: index 0 = tier 1 chance at tavern tier 1, etc.
        private static readonly int[][] OddsTable =
        {
            new[] { 100, 0, 0, 0, 0, 0 },
            new[] { 75, 25, 0, 0, 0, 0 },
            new[] { 55, 30, 15, 0, 0, 0 },
            new[] { 35, 30, 25, 10, 0, 0 },
            new[] { 20, 25, 30, 20, 5, 0 },
            new[] { 10, 20, 25, 25, 15, 5 }
        };

        public static MinionCardDefinition RollCard(int tavernTier, System.Random random)
        {
            var tierIndex = UnityEngine.Mathf.Clamp(tavernTier, 1, 6) - 1;
            var odds = OddsTable[tierIndex];
            var roll = random.Next(100);
            var cumulative = 0;
            var chosenMinionTier = 1;

            for (var i = 0; i < odds.Length; i++)
            {
                cumulative += odds[i];
                if (roll < cumulative)
                {
                    chosenMinionTier = i + 1;
                    break;
                }
            }

            var pool = CardRegistry.GetCardsAtTier(chosenMinionTier);
            if (pool.Count == 0)
            {
                pool = CardRegistry.GetPoolForTier(tavernTier);
            }

            if (pool.Count == 0)
            {
                return null;
            }

            return pool[random.Next(pool.Count)];
        }
    }
}