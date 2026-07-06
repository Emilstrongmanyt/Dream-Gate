using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Economy;
using UnityEngine;

namespace DreamGate.Battlegrounds.Players
{
    public static class BotPlayerController
    {
        public static void TakeRecruitTurn(PlayerState bot, int turn, int botSeed)
        {
            var random = new System.Random(botSeed + turn * 997 + bot.playerId * 131);
            bot.gold = MatchConfig.GetGoldIncomeForTurn(turn);
            ShopSystem.RefreshShop(bot, random.Next());

            var actions = 0;
            while (bot.gold >= MatchConfig.MinionCost && actions < 8 && !bot.HandFull)
            {
                var affordableSlots = new System.Collections.Generic.List<int>();
                for (var i = 0; i < bot.shopCardIds.Count; i++)
                {
                    if (!string.IsNullOrEmpty(bot.shopCardIds[i]))
                    {
                        affordableSlots.Add(i);
                    }
                }

                if (affordableSlots.Count == 0)
                {
                    break;
                }

                var slot = affordableSlots[random.Next(affordableSlots.Count)];
                ShopSystem.TryBuy(bot, slot, out _);
                actions++;
            }

            while (bot.hand.Count > 0 && !bot.BoardFull)
            {
                ShopSystem.TryPlayFromHand(bot, 0, out _);
            }

            if (bot.tavernTier < MatchConfig.MaxTavernTier &&
                bot.gold >= MatchConfig.TavernUpgradeCost &&
                random.NextDouble() < 0.35)
            {
                ShopSystem.TryUpgradeTavern(bot, out _);
            }
        }
    }
}