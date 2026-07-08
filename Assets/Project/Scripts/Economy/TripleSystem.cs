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
            var rewardDefinition = ResolveTripleReward(definition);
            var golden = rewardDefinition != null
                ? MinionInstance.FromDefinition(rewardDefinition, golden: true)
                : new MinionInstance { cardId = cardId };

            golden.isGolden = true;
            if (rewardDefinition != null && rewardDefinition.cardId != cardId)
            {
                golden.attack = rewardDefinition.attack;
                golden.health = rewardDefinition.health;
                golden.maxHealth = rewardDefinition.health;
            }
            else
            {
                golden.attack = totalAttack;
                golden.health = totalHealth;
                golden.maxHealth = totalHealth;
            }

            if (!player.HandFull)
            {
                player.hand.Add(golden);
            }
            else if (!player.BoardFull)
            {
                var slot = player.GetDefaultPlaySlot();
                player.board[slot] = golden;
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

        private static MinionCardDefinition ResolveTripleReward(MinionCardDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(definition.tripleRewardCardId))
            {
                return CardRegistry.Get(definition.tripleRewardCardId) ?? definition;
            }

            return definition;
        }

        public static string GetTripleRewardDisplayName(string cardId)
        {
            var definition = CardRegistry.Get(cardId);
            var reward = ResolveTripleReward(definition);
            if (reward == null)
            {
                return cardId;
            }

            if (reward.cardId != cardId)
            {
                return reward.displayName;
            }

            return $"Golden {reward.displayName}";
        }

        private static void CollectCopies(MinionInstance[] source, string cardId, List<MinionInstance> output)
        {
            foreach (var minion in source)
            {
                if (minion != null && minion.cardId == cardId && !minion.isGolden)
                {
                    output.Add(minion);
                }
            }
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

        private static void RemoveInstance(MinionInstance[] board, List<MinionInstance> toRemove)
        {
            for (var i = 0; i < board.Length; i++)
            {
                if (board[i] != null && toRemove.Contains(board[i]))
                {
                    board[i] = null;
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