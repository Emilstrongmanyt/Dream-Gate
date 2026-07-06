using System.Collections.Generic;
using DreamGate.Battlegrounds.Combat;
using DreamGate.Battlegrounds.Core;

namespace DreamGate.Battlegrounds.Cards
{
    public static class AbilitySystem
    {
        public static void OnMinionDeath(
            MinionInstance minion,
            List<MinionInstance> board,
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
            List<MinionInstance> board,
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
            List<MinionInstance> board,
            bool isAttackerBoard,
            List<string> log,
            List<CombatEvent> events)
        {
            for (var i = 0; i < board.Count; i++)
            {
                var minion = board[i];
                if (minion.isDead || minion.health <= 0)
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

        public static bool HasTaunt(MinionInstance minion)
        {
            var definition = CardRegistry.Get(minion.cardId);
            return definition != null && definition.abilityType == AbilityType.Taunt;
        }

        public static bool HasCleave(MinionInstance minion)
        {
            var definition = CardRegistry.Get(minion.cardId);
            return definition != null && definition.abilityType == AbilityType.Cleave;
        }

        private static void ResolveSummonDeathrattle(
            MinionInstance minion,
            MinionCardDefinition definition,
            List<MinionInstance> board,
            bool isAttackerBoard,
            List<string> log,
            List<CombatEvent> events)
        {
            var tokenDefinition = CardRegistry.Get(definition.summonCardId);
            if (tokenDefinition == null)
            {
                return;
            }

            if (board.Count >= MatchConfig.BoardSize)
            {
                var fizzle = $"{definition.displayName} deathrattle fizzled (board full).";
                log.Add(fizzle);
                events?.Add(new CombatEvent { type = CombatEventType.Deathrattle, message = fizzle });
                return;
            }

            var token = MinionInstance.FromDefinition(tokenDefinition);
            board.Add(token);
            var message = $"{definition.displayName} summoned {tokenDefinition.displayName}.";
            log.Add(message);
            events?.Add(new CombatEvent
            {
                type = CombatEventType.Deathrattle,
                message = message,
                isAttackerBoard = isAttackerBoard,
                abilityCardId = tokenDefinition.cardId
            });
        }

        private static void ResolveSummonCopy(
            MinionInstance minion,
            MinionCardDefinition definition,
            List<MinionInstance> board,
            bool isAttackerBoard,
            List<string> log,
            List<CombatEvent> events)
        {
            if (board.Count >= MatchConfig.BoardSize)
            {
                return;
            }

            var copy = minion.Clone();
            board.Add(copy);
            var message = $"{definition.displayName} summoned a copy.";
            log.Add(message);
            events?.Add(new CombatEvent
            {
                type = CombatEventType.Deathrattle,
                message = message,
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
    }
}