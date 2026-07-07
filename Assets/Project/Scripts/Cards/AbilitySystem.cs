using System;
using System.Collections.Generic;
using DreamGate.Battlegrounds.Combat;
using DreamGate.Battlegrounds.Core;

namespace DreamGate.Battlegrounds.Cards
{
    public static class AbilitySystem
    {
        public static void OnMinionDeath(
            MinionInstance minion,
            MinionInstance[] board,
            bool isAttackerBoard,
            List<string> log,
            List<CombatEvent> events)
        {
            var definition = CardRegistry.Get(minion.cardId);
            if (definition == null)
            {
                return;
            }

            if (definition.abilityType == AbilityType.DeathrattleSummon)
            {
                ResolveSummonDeathrattle(minion, definition, board, isAttackerBoard, log, events);
            }
        }

        public static void OnMinionDamaged(
            MinionInstance minion,
            int boardIndex,
            MinionInstance[] board,
            bool isAttackerBoard,
            List<string> log,
            List<CombatEvent> events)
        {
            if (minion.isDead || minion.health <= 0)
            {
                return;
            }

            var definition = CardRegistry.Get(minion.cardId);
            if (definition == null)
            {
                return;
            }

            switch (definition.abilityType)
            {
                case AbilityType.OnDamageSummonCopy:
                    ResolveSummonCopy(minion, definition, board, isAttackerBoard, log, events);
                    break;
                case AbilityType.OnDamageTransform:
                    ResolveTransform(minion, boardIndex, isAttackerBoard, definition, log, events);
                    break;
            }
        }

        public static void OnStartOfCombat(
            MinionInstance[] board,
            bool isAttackerBoard,
            List<string> log,
            List<CombatEvent> events)
        {
            for (var i = 0; i < board.Length; i++)
            {
                var minion = board[i];
                if (minion == null || minion.isDead || minion.health <= 0)
                {
                    continue;
                }

                var definition = CardRegistry.Get(minion.cardId);
                if (definition == null || definition.abilityType != AbilityType.StartOfCombatBuffSelf)
                {
                    continue;
                }

                minion.attack += definition.abilityValue;
                var message = $"{definition.displayName} gains +{definition.abilityValue} Attack.";
                log.Add(message);
                events?.Add(new CombatEvent
                {
                    type = CombatEventType.Start,
                    message = message,
                    boardIndex = i,
                    isAttackerBoard = isAttackerBoard,
                    attackDelta = definition.abilityValue
                });
            }
        }

        public static void OnBattlecry(MinionInstance minion, int boardIndex, MinionInstance[] board)
        {
            var definition = CardRegistry.Get(minion.cardId);
            if (definition == null || definition.abilityType != AbilityType.Battlecry)
            {
                return;
            }

            if (!string.IsNullOrEmpty(definition.summonCardId))
            {
                SummonTokenToBoard(definition.summonCardId, board);
                return;
            }

            if (!string.IsNullOrEmpty(definition.abilityText) &&
                definition.abilityText.IndexOf("adjacent", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                BuffAdjacent(board, boardIndex, definition.abilityValue > 0 ? definition.abilityValue : 1);
                return;
            }

            if (definition.abilityValue > 0)
            {
                minion.attack += definition.abilityValue;
                minion.health += definition.abilityValue;
                minion.maxHealth += definition.abilityValue;
            }
        }

        public static bool HasAbility(MinionInstance minion, AbilityType abilityType)
        {
            var definition = CardRegistry.Get(minion.cardId);
            return definition != null && definition.abilityType == abilityType;
        }

        public static bool HasTaunt(MinionInstance minion)
        {
            return HasAbility(minion, AbilityType.Taunt);
        }

        public static bool HasCleave(MinionInstance minion)
        {
            return HasAbility(minion, AbilityType.Cleave);
        }

        public static bool HasWindfury(MinionInstance minion)
        {
            return HasAbility(minion, AbilityType.Windfury) || HasAbility(minion, AbilityType.MegaWindfury);
        }

        public static bool HasMegaWindfury(MinionInstance minion)
        {
            return HasAbility(minion, AbilityType.MegaWindfury);
        }

        public static int GetAttacksPerCombatTurn(MinionInstance minion)
        {
            if (HasMegaWindfury(minion))
            {
                return 3;
            }

            if (HasWindfury(minion))
            {
                return 2;
            }

            return 1;
        }

        public static int CountBoardMinions(MinionInstance[] board)
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

        public static int FindFirstEmptySlot(MinionInstance[] board)
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

        private static void ResolveSummonDeathrattle(
            MinionInstance minion,
            MinionCardDefinition definition,
            MinionInstance[] board,
            bool isAttackerBoard,
            List<string> log,
            List<CombatEvent> events)
        {
            var tokenDefinition = CardRegistry.Get(definition.summonCardId);
            if (tokenDefinition == null)
            {
                return;
            }

            var slot = FindFirstEmptySlot(board);
            if (slot < 0)
            {
                var fizzle = $"{definition.displayName} deathrattle fizzled (board full).";
                log.Add(fizzle);
                events?.Add(new CombatEvent { type = CombatEventType.Deathrattle, message = fizzle });
                return;
            }

            var token = MinionInstance.FromDefinition(tokenDefinition);
            board[slot] = token;
            var message = $"{definition.displayName} summoned {tokenDefinition.displayName}.";
            log.Add(message);
            events?.Add(new CombatEvent
            {
                type = CombatEventType.Deathrattle,
                message = message,
                boardIndex = slot,
                isAttackerBoard = isAttackerBoard,
                abilityCardId = tokenDefinition.cardId
            });
        }

        private static void ResolveSummonCopy(
            MinionInstance minion,
            MinionCardDefinition definition,
            MinionInstance[] board,
            bool isAttackerBoard,
            List<string> log,
            List<CombatEvent> events)
        {
            var slot = FindFirstEmptySlot(board);
            if (slot < 0)
            {
                return;
            }

            var copy = minion.Clone();
            board[slot] = copy;
            var message = $"{definition.displayName} summoned a copy.";
            log.Add(message);
            events?.Add(new CombatEvent
            {
                type = CombatEventType.Deathrattle,
                message = message,
                boardIndex = slot,
                isAttackerBoard = isAttackerBoard,
                abilityCardId = copy.cardId
            });
        }

        private static void ResolveTransform(
            MinionInstance minion,
            int boardIndex,
            bool isAttackerBoard,
            MinionCardDefinition definition,
            List<string> log,
            List<CombatEvent> events)
        {
            var transformed = CardRegistry.Get(definition.summonCardId);
            if (transformed == null || minion.cardId == transformed.cardId)
            {
                return;
            }

            minion.cardId = transformed.cardId;
            minion.attack = transformed.attack;
            minion.health = transformed.health;
            minion.maxHealth = transformed.health;
            var message = $"{definition.displayName} became {transformed.displayName} ({minion.attack}/{minion.health}).";
            log.Add(message);
            events?.Add(new CombatEvent
            {
                type = CombatEventType.Deathrattle,
                message = message,
                boardIndex = boardIndex,
                isAttackerBoard = isAttackerBoard,
                abilityCardId = transformed.cardId
            });
        }

        private static void SummonTokenToBoard(string summonCardId, MinionInstance[] board)
        {
            var tokenDefinition = CardRegistry.Get(summonCardId);
            if (tokenDefinition == null)
            {
                return;
            }

            var slot = FindFirstEmptySlot(board);
            if (slot < 0)
            {
                return;
            }

            board[slot] = MinionInstance.FromDefinition(tokenDefinition);
        }

        private static void BuffAdjacent(MinionInstance[] board, int boardIndex, int amount)
        {
            if (boardIndex > 0 && board[boardIndex - 1] != null)
            {
                ApplyBuff(board[boardIndex - 1], amount);
            }

            if (boardIndex < board.Length - 1 && board[boardIndex + 1] != null)
            {
                ApplyBuff(board[boardIndex + 1], amount);
            }
        }

        private static void ApplyBuff(MinionInstance minion, int amount)
        {
            minion.attack += amount;
            minion.health += amount;
            minion.maxHealth += amount;
        }
    }
}