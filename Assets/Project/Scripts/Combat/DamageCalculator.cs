using System.Collections.Generic;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Players;

namespace DreamGate.Battlegrounds.Combat
{
    public static class DamageCalculator
    {
        public static int CalculateSurvivorTierSum(IEnumerable<MinionInstance> survivors)
        {
            var total = 0;
            foreach (var minion in survivors)
            {
                if (minion == null || minion.isDead)
                {
                    continue;
                }

                var definition = CardRegistry.Get(minion.cardId);
                total += definition != null ? definition.tier : 1;
            }

            return total;
        }

        public static void ApplyCombatDamage(PlayerState winner, PlayerState loser, CombatResult result)
        {
            var winnerBoard = winner.GetLivingBoard();
            var loserBoard = loser.GetLivingBoard();

            if (winnerBoard.Count > 0 && loserBoard.Count == 0)
            {
                result.damageToDefender = CalculateSurvivorTierSum(winnerBoard);
                loser.heroHealth -= result.damageToDefender;
            }
            else if (loserBoard.Count > 0 && winnerBoard.Count == 0)
            {
                result.damageToAttacker = CalculateSurvivorTierSum(loserBoard);
                winner.heroHealth -= result.damageToAttacker;
            }
            else
            {
                result.outcome = CombatOutcome.Draw;
            }

            if (winner.heroHealth <= 0)
            {
                winner.isEliminated = true;
            }

            if (loser.heroHealth <= 0)
            {
                loser.isEliminated = true;
            }
        }
    }
}