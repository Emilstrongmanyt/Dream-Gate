using System.Collections.Generic;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Players;

namespace DreamGate.Battlegrounds.Economy
{
    public static class TripleSystem
    {
        public struct TripleResult
        {
            public bool triggered;
            public MinionInstance goldenMinion;
            public int goldRewarded;
        }

        public static TripleResult TryCombine(PlayerState player, string cardId)
        {
            var result = new TripleResult();
            if (player.CountCopies(cardId) < 3)
            {
                return result;
            }

            var copies = new List<MinionInstance>();
            CollectCopies(player.board, cardId, copies);
            CollectCopies(player.hand, cardId, copies);

            if (copies.Count < 3)
            {
                return result;
            }

            var toCombine = copies.GetRange(0, 3);
            var totalAttack = 0;
            var totalHealth = 0;
            foreach (var copy in toCombine)
            {
                totalAttack += copy.attack;
                totalHealth += copy.health;
            }

            RemoveInstance(player.board, toCombine);
            RemoveInstance(player.hand, toCombine);

            var definition = CardRegistry.Get(cardId);
            var golden = definition != null
                ? MinionInstance.FromDefinition(definition, golden: true)
                : new MinionInstance { cardId = cardId };

            golden.attack = totalAttack;
            golden.health = totalHealth;
            golden.maxHealth = totalHealth;
            golden.isGolden = true;

            if (!player.HandFull)
            {
                player.hand.Add(golden);
            }
            else if (!player.BoardFull)
            {
                player.board.Add(golden);
            }
            else
            {
                player.hand.Add(golden);
            }

            player.gold += MatchConfig.TripleGoldReward;

            result.triggered = true;
            result.goldenMinion = golden;
            result.goldRewarded = MatchConfig.TripleGoldReward;
            return result;
        }

        private static void CollectCopies(List<MinionInstance> source, string cardId, List<MinionInstance> output)
        {
            foreach (var minion in source)
            {
                if (minion.cardId == cardId && !minion.isGolden)
                {
                    output.Add(minion);
                }
            }
        }

        private static void RemoveInstance(List<MinionInstance> list, List<MinionInstance> toRemove)
        {
            foreach (var minion in toRemove)
            {
                list.Remove(minion);
            }
        }
    }
}