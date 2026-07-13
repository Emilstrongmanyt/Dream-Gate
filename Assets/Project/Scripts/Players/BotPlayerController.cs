using System.Collections.Generic;
using DreamGate.Battlegrounds.Campaign;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Economy;

namespace DreamGate.Battlegrounds.Players
{
    public static class BotPlayerController
    {
        public static void TakeRecruitTurn(
            PlayerState bot,
            int turn,
            int botSeed,
            CampaignMissionDefinition campaignMission = null)
        {
            var random = new System.Random(botSeed + turn * 997 + bot.playerId * 131);
            bot.gold = MatchConfig.GetGoldIncomeForTurn(turn);
            ApplyCampaignBonuses(bot, campaignMission, random);

            var refreshAttempts = 0;
            while (refreshAttempts < 2 && bot.gold >= MatchConfig.ShopRefreshCost && !HasStrongBuy(bot))
            {
                ShopSystem.TryRefreshShop(bot, out _);
                refreshAttempts++;
            }

            var actions = 0;
            while (bot.gold >= MatchConfig.MinionCost && actions < 10 && !bot.HandFull)
            {
                var affordableSlots = GetAffordableShopSlots(bot, turn);
                if (affordableSlots.Count == 0)
                {
                    if (bot.gold >= MatchConfig.ShopRefreshCost)
                    {
                        ShopSystem.TryRefreshShop(bot, out _);
                        affordableSlots = GetAffordableShopSlots(bot, turn);
                        if (affordableSlots.Count == 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                var slot = PickBestShopSlot(bot, affordableSlots, random);
                ShopSystem.TryBuy(bot, slot, out _);
                actions++;
            }

            PlayHand(bot, random);
            TrySellWeakMinions(bot, random);

            if (bot.tavernTier < MatchConfig.MaxTavernTier &&
                bot.gold >= MatchConfig.GetTavernUpgradeCost(bot.tavernTier) &&
                ShouldUpgrade(bot, random, campaignMission))
            {
                ShopSystem.TryUpgradeTavern(bot, out _);
            }

            PlayHand(bot, random);
        }

        private static void ApplyCampaignBonuses(
            PlayerState bot,
            CampaignMissionDefinition mission,
            System.Random random)
        {
            if (mission == null)
            {
                return;
            }

            bot.gold += mission.bonusGoldPerTurn;
            var targetTier = MatchConfig.StartingTavernTier + mission.bonusTavernTier;
            while (bot.tavernTier < targetTier && bot.tavernTier < MatchConfig.MaxTavernTier)
            {
                bot.tavernTier++;
            }
        }

        private static bool ShouldUpgrade(PlayerState bot, System.Random random, CampaignMissionDefinition mission)
        {
            var baseChance = bot.BoardCount >= 3 ? 0.55 : 0.35;
            if (mission != null)
            {
                baseChance = mission.bonusUpgradeChancePercent / 100f;
            }

            return random.NextDouble() < baseChance;
        }

        private static List<int> GetAffordableShopSlots(PlayerState bot, int turn)
        {
            var slots = new List<int>();
            for (var i = 0; i < bot.shopCardIds.Count; i++)
            {
                if (string.IsNullOrEmpty(bot.shopCardIds[i]) || bot.gold < MatchConfig.MinionCost)
                {
                    continue;
                }

                if (turn == 1 && IsSpell(bot.shopCardIds[i]))
                {
                    continue;
                }

                slots.Add(i);
            }

            return slots;
        }

        private static int PickBestShopSlot(PlayerState bot, List<int> affordableSlots, System.Random random)
        {
            var bestSlot = affordableSlots[0];
            var bestScore = int.MinValue;
            foreach (var slot in affordableSlots)
            {
                var card = CardRegistry.Get(bot.shopCardIds[slot]);
                if (card == null)
                {
                    continue;
                }

                var score = card.attack + card.health + card.tier * 2;
                if (card.cardKind == CardKind.Spell)
                {
                    score -= 2;
                }

                if (CountsTowardTriple(bot, card.cardId))
                {
                    score += 8;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = slot;
                }
            }

            return random.NextDouble() < 0.2 ? affordableSlots[random.Next(affordableSlots.Count)] : bestSlot;
        }

        private static bool CountsTowardTriple(PlayerState bot, string cardId)
        {
            var count = 0;
            foreach (var boardMinion in bot.board)
            {
                if (boardMinion != null && boardMinion.cardId == cardId)
                {
                    count++;
                }
            }

            foreach (var handMinion in bot.hand)
            {
                if (handMinion.cardId == cardId)
                {
                    count++;
                }
            }

            return count >= 1;
        }

        private static bool HasStrongBuy(PlayerState bot)
        {
            foreach (var cardId in bot.shopCardIds)
            {
                var card = CardRegistry.Get(cardId);
                if (card != null && card.tier >= bot.tavernTier && card.attack + card.health >= 5)
                {
                    return true;
                }
            }

            return false;
        }

        private static void PlayHand(PlayerState bot, System.Random random)
        {
            var safety = 0;
            while (bot.hand.Count > 0 && safety < 12)
            {
                safety++;
                var played = false;
                for (var i = 0; i < bot.hand.Count; i++)
                {
                    var card = CardRegistry.Get(bot.hand[i].cardId);
                    if (card != null && card.cardKind == CardKind.Spell)
                    {
                        if (TryCastBestSpell(bot, i))
                        {
                            played = true;
                            break;
                        }

                        continue;
                    }

                    if (bot.BoardFull)
                    {
                        break;
                    }

                    if (ShopSystem.TryPlayFromHand(bot, i, out _))
                    {
                        played = true;
                        break;
                    }
                }

                if (!played)
                {
                    break;
                }
            }
        }

        private static bool TryCastBestSpell(PlayerState bot, int handIndex)
        {
            var card = CardRegistry.Get(bot.hand[handIndex].cardId);
            if (card == null || card.cardKind != CardKind.Spell)
            {
                return false;
            }

            if (!SpellSystem.RequiresBoardTarget(card.spellEffect))
            {
                return SpellSystem.TryCast(bot, handIndex, -1, out _);
            }

            var bestIndex = -1;
            var bestScore = int.MinValue;
            for (var i = 0; i < bot.board.Length; i++)
            {
                if (bot.board[i] == null)
                {
                    continue;
                }

                var score = bot.board[i].attack + bot.board[i].health;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex >= 0 && SpellSystem.TryCast(bot, handIndex, bestIndex, out _);
        }

        private static void TrySellWeakMinions(PlayerState bot, System.Random random)
        {
            if (bot.BoardCount <= 2 || bot.hand.Count >= bot.board.Length)
            {
                return;
            }

            var sellIndex = -1;
            var weakest = int.MaxValue;
            for (var i = 0; i < bot.board.Length; i++)
            {
                if (bot.board[i] == null)
                {
                    continue;
                }

                var score = bot.board[i].attack + bot.board[i].health;
                if (score < weakest)
                {
                    weakest = score;
                    sellIndex = i;
                }
            }

            if (sellIndex >= 0 && weakest <= 4 && random.NextDouble() < 0.65)
            {
                ShopSystem.TrySellFromBoard(bot, sellIndex, out _);
            }
        }

        private static bool IsSpell(string cardId)
        {
            var definition = CardRegistry.Get(cardId);
            return definition != null && definition.cardKind == CardKind.Spell;
        }
    }
}