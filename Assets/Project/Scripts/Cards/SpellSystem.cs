using DreamGate.Battlegrounds.Players;

namespace DreamGate.Battlegrounds.Cards
{
    public static class SpellSystem
    {
        public static bool RequiresBoardTarget(SpellEffect effect)
        {
            return effect == SpellEffect.DivineShield || effect == SpellEffect.OnyxApple;
        }

        public static bool TryCast(
            PlayerState player,
            int handIndex,
            int targetBoardIndex,
            out string message)
        {
            message = string.Empty;
            if (handIndex < 0 || handIndex >= player.hand.Count)
            {
                message = "Invalid hand slot.";
                return false;
            }

            var minion = player.hand[handIndex];
            var definition = CardRegistry.Get(minion.cardId);
            if (definition == null || definition.cardKind != CardKind.Spell)
            {
                message = "Not a spell.";
                return false;
            }

            if (RequiresBoardTarget(definition.spellEffect))
            {
                if (targetBoardIndex < 0 || targetBoardIndex >= player.board.Length)
                {
                    message = "Select a friendly minion on the board.";
                    return false;
                }

                if (player.board[targetBoardIndex] == null)
                {
                    message = "Target slot is empty.";
                    return false;
                }
            }

            switch (definition.spellEffect)
            {
                case SpellEffect.DivineShield:
                    player.board[targetBoardIndex].divineShieldGranted = true;
                    player.board[targetBoardIndex].hasDivineShield = true;
                    message = $"Divine Shield cast on {CardRegistry.Get(player.board[targetBoardIndex].cardId)?.displayName}.";
                    break;

                case SpellEffect.OnyxApple:
                {
                    var target = player.board[targetBoardIndex];
                    target.attack *= 2;
                    var name = CardRegistry.Get(target.cardId)?.displayName ?? target.cardId;
                    message = $"Onyx Apple doubled {name}'s Attack ({target.attack}).";
                    break;
                }

                case SpellEffect.Rage:
                    BuffAllFriendly(player, 1, 0);
                    message = "Rage gave all friendly minions +1 Attack.";
                    break;

                case SpellEffect.Doom:
                    player.doomNextCombat = true;
                    message = "Doom will turn a random enemy minion into a Snail next combat.";
                    break;

                default:
                    message = "Unknown spell.";
                    return false;
            }

            player.hand.RemoveAt(handIndex);
            return true;
        }

        private static void BuffAllFriendly(PlayerState player, int attackDelta, int healthDelta)
        {
            foreach (var boardMinion in player.board)
            {
                if (boardMinion == null)
                {
                    continue;
                }

                boardMinion.attack += attackDelta;
                boardMinion.health += healthDelta;
                boardMinion.maxHealth += healthDelta;
            }

            foreach (var handMinion in player.hand)
            {
                handMinion.attack += attackDelta;
                handMinion.health += healthDelta;
                handMinion.maxHealth += healthDelta;
            }
        }
    }
}