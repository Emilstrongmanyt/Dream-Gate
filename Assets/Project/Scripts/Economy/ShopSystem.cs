using System.Collections.Generic;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Players;

namespace DreamGate.Battlegrounds.Economy
{
    public static class ShopSystem
    {
        public static void RefreshShop(PlayerState player, int? seed = null)
        {
            player.shopCardIds.Clear();

            var random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            for (var i = 0; i < MatchConfig.ShopSlotCount; i++)
            {
                var card = TavernTierOdds.RollCard(player.tavernTier, random);
                player.shopCardIds.Add(card != null ? card.cardId : string.Empty);
            }
        }

        public static bool TryBuy(PlayerState player, int shopIndex, out string message)
        {
            message = string.Empty;
            if (shopIndex < 0 || shopIndex >= player.shopCardIds.Count)
            {
                message = "Invalid shop slot.";
                return false;
            }

            if (player.gold < MatchConfig.MinionCost)
            {
                message = "Not enough gold.";
                return false;
            }

            if (player.HandFull)
            {
                message = "Hand is full.";
                return false;
            }

            var cardId = player.shopCardIds[shopIndex];
            var definition = CardRegistry.Get(cardId);
            if (definition == null)
            {
                message = "Card not found.";
                return false;
            }

            player.gold -= MatchConfig.MinionCost;
            player.shopCardIds[shopIndex] = string.Empty;

            var minion = MinionInstance.FromDefinition(definition);
            player.hand.Add(minion);
            GameSfxPlayer.PlayRecruit(player, GameSfxPlayer.PlayBuyCard);

            if (definition.cardKind == CardKind.Spell)
            {
                message = $"Purchased {definition.displayName}.";
                return true;
            }

            var triple = TripleSystem.TryCombine(player, cardId);
            if (triple.triggered)
            {
                message =
                    $"Triple! {TripleSystem.GetTripleRewardDisplayName(cardId)} created (+{triple.goldRewarded} gold).";
            }
            else
            {
                message = $"Purchased {definition.displayName}.";
            }

            return true;
        }

        public static bool TrySell(PlayerState player, int boardIndex, out string message)
        {
            message = string.Empty;
            if (boardIndex < 0 || boardIndex >= player.board.Length)
            {
                message = "Invalid board slot.";
                return false;
            }

            if (player.board[boardIndex] == null)
            {
                message = "Board slot is empty.";
                return false;
            }

            player.board[boardIndex] = null;
            player.gold += MatchConfig.SellValue;
            GameSfxPlayer.PlayRecruit(player, GameSfxPlayer.PlaySellCard);
            message = $"Sold minion (+{MatchConfig.SellValue} gold).";
            return true;
        }

        public static bool TryRefreshShop(PlayerState player, out string message)
        {
            message = string.Empty;
            if (player.gold < MatchConfig.ShopRefreshCost)
            {
                message = "Not enough gold to refresh.";
                return false;
            }

            player.gold -= MatchConfig.ShopRefreshCost;
            RefreshShop(player);
            message = $"Shop refreshed (-{MatchConfig.ShopRefreshCost} gold).";
            return true;
        }

        public static bool TryUpgradeTavern(PlayerState player, out string message)
        {
            message = string.Empty;
            if (player.tavernTier >= MatchConfig.MaxTavernTier)
            {
                message = "Tavern is already max tier.";
                return false;
            }

            if (player.gold < MatchConfig.TavernUpgradeCost)
            {
                message = "Not enough gold to upgrade.";
                return false;
            }

            player.gold -= MatchConfig.TavernUpgradeCost;
            player.tavernTier++;
            RefreshShop(player);
            GameSfxPlayer.PlayRecruit(player, GameSfxPlayer.PlayTierUp);
            message = $"Tavern upgraded to tier {player.tavernTier}.";
            return true;
        }

        public static bool TryReorderBoard(PlayerState player, int fromIndex, int toIndex, out string message)
        {
            message = string.Empty;
            if (fromIndex < 0 || fromIndex >= player.board.Length)
            {
                message = "Invalid board slot.";
                return false;
            }

            if (toIndex < 0 || toIndex >= MatchConfig.BoardSize)
            {
                message = "Invalid target slot.";
                return false;
            }

            if (fromIndex == toIndex)
            {
                return true;
            }

            if (player.board[fromIndex] == null)
            {
                message = "Source slot is empty.";
                return false;
            }

            var minion = player.board[fromIndex];
            player.board[fromIndex] = player.board[toIndex];
            player.board[toIndex] = minion;
            message = "Board rearranged.";
            return true;
        }

        public static bool TryPlayFromHand(PlayerState player, int handIndex, out string message)
        {
            var slot = player.GetDefaultPlaySlot();
            return TryPlayFromHandToSlot(player, handIndex, slot, out message);
        }

        public static bool TryPlayFromHandToSlot(PlayerState player, int handIndex, int boardIndex, out string message)
        {
            message = string.Empty;
            if (handIndex < 0 || handIndex >= player.hand.Count)
            {
                message = "Invalid hand slot.";
                return false;
            }

            if (boardIndex < 0 || boardIndex >= player.board.Length)
            {
                message = "Invalid board slot.";
                return false;
            }

            if (player.BoardFull)
            {
                message = "Board is full.";
                return false;
            }

            if (player.board[boardIndex] != null)
            {
                message = "Board slot is occupied.";
                return false;
            }

            var minion = player.hand[handIndex];
            var definition = CardRegistry.Get(minion.cardId);
            if (definition != null && definition.cardKind == CardKind.Spell)
            {
                message = "Spells must be cast by tapping and selecting a target.";
                return false;
            }

            player.hand.RemoveAt(handIndex);
            player.board[boardIndex] = minion;
            var battlecryMessage = AbilitySystem.OnBattlecry(minion, boardIndex, player);
            GameSfxPlayer.PlayRecruit(player, GameSfxPlayer.PlayDropCard);
            message = string.IsNullOrEmpty(battlecryMessage) ? "Minion played to board." : battlecryMessage;
            return true;
        }
    }
}