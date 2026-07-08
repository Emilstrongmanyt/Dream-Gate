using System;
using System.Collections.Generic;
using DreamGate.Battlegrounds.Combat;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Players;

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

            switch (definition.abilityType)
            {
                case AbilityType.DeathrattleSummon:
                    ResolveSummonDeathrattle(minion, definition, board, isAttackerBoard, log, events);
                    break;
                case AbilityType.DeathrattleBuffAllAttack:
                    ResolveDeathrattleBuffAllAttack(minion, definition, board, isAttackerBoard, log, events);
                    break;
            }
        }

        public static void OnMinionDamaged(
            MinionInstance minion,
            int boardIndex,
            MinionInstance[] board,
            bool isAttackerBoard,
            int damageTaken,
            System.Random random,
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
                case AbilityType.OnDamageDodge:
                    if (damageTaken > 0 && random != null && RollChance(random, definition.abilityValue))
                    {
                        minion.health += damageTaken;
                        var dodgeMessage = $"{definition.displayName} dodged the hit.";
                        log?.Add(dodgeMessage);
                        events?.Add(new CombatEvent
                        {
                            type = CombatEventType.Attack,
                            message = dodgeMessage,
                            boardIndex = boardIndex,
                            isAttackerBoard = isAttackerBoard
                        });
                    }

                    break;

                case AbilityType.OnDamageSummonCopy:
                case AbilityType.OnDamageSummonCopyChance:
                    if (definition.abilityType == AbilityType.OnDamageSummonCopyChance &&
                        (random == null || !RollChance(random, definition.abilityValue)))
                    {
                        break;
                    }

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
            RefreshDivineShields(board);

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

        public static string OnBattlecry(MinionInstance minion, int boardIndex, PlayerState player)
        {
            var definition = CardRegistry.Get(minion.cardId);
            if (definition == null)
            {
                return string.Empty;
            }

            switch (definition.abilityType)
            {
                case AbilityType.BattlecryDamageHero:
                    player.heroHealth = Math.Max(0, player.heroHealth - Math.Max(1, definition.abilityValue));
                    return $"{definition.displayName} dealt {definition.abilityValue} damage to your hero.";

                case AbilityType.BattlecryBuffTribe:
                    BuffTribeMinions(player, definition.cardTribe, definition.abilityValue, definition.abilityValue, minion, includeSelf: true);
                    return $"{definition.displayName} buffed {definition.cardTribe} minions +{definition.abilityValue}/+{definition.abilityValue}.";

                case AbilityType.BattlecryBuffOtherTribeHealth:
                    BuffTribeMinions(player, definition.cardTribe, 0, definition.abilityValue, minion, includeSelf: false);
                    return $"{definition.displayName} buffed other {definition.cardTribe} minions +{definition.abilityValue} HP.";

                case AbilityType.BattlecryBuffTribeAttack:
                    BuffTribeMinions(player, definition.cardTribe, definition.abilityValue, 0, minion, includeSelf: true);
                    return $"{definition.displayName} buffed {definition.cardTribe} minions +{definition.abilityValue} Attack.";

                case AbilityType.Battlecry:
                    if (!string.IsNullOrEmpty(definition.summonCardId))
                    {
                        SummonTokenToBoard(definition.summonCardId, player.board);
                        return $"{definition.displayName} summoned a token.";
                    }

                    if (!string.IsNullOrEmpty(definition.abilityText) &&
                        definition.abilityText.IndexOf("adjacent", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        BuffAdjacent(player.board, boardIndex, definition.abilityValue > 0 ? definition.abilityValue : 1);
                        return $"{definition.displayName} buffed adjacent minions.";
                    }

                    if (definition.abilityValue > 0)
                    {
                        minion.attack += definition.abilityValue;
                        minion.health += definition.abilityValue;
                        minion.maxHealth += definition.abilityValue;
                        return $"{definition.displayName} gained +{definition.abilityValue}/+{definition.abilityValue}.";
                    }

                    break;
            }

            return string.Empty;
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

        public static bool HasDivineShieldKeyword(MinionInstance minion)
        {
            return HasAbility(minion, AbilityType.DivineShield) || minion.divineShieldGranted;
        }

        public static void RefreshDivineShields(MinionInstance[] board)
        {
            foreach (var minion in board)
            {
                if (minion == null || minion.isDead)
                {
                    continue;
                }

                if (HasDivineShieldKeyword(minion))
                {
                    minion.hasDivineShield = true;
                }
            }
        }

        public static int ApplyDamage(
            MinionInstance target,
            int damage,
            List<string> log,
            List<CombatEvent> events,
            int boardIndex,
            bool isAttackerBoard)
        {
            if (target == null || damage <= 0)
            {
                return 0;
            }

            if (target.hasDivineShield)
            {
                target.hasDivineShield = false;
                var shieldMessage = $"{CardRegistry.Get(target.cardId)?.displayName ?? target.cardId}'s Divine Shield absorbed the hit.";
                log?.Add(shieldMessage);
                events?.Add(new CombatEvent
                {
                    type = CombatEventType.Attack,
                    message = shieldMessage,
                    boardIndex = boardIndex,
                    isAttackerBoard = isAttackerBoard,
                    damageAmount = 0
                });
                return 0;
            }

            target.health -= damage;
            return damage;
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

        public static bool MatchesTribe(MinionCardDefinition definition, string tribe)
        {
            return definition != null &&
                   !string.IsNullOrEmpty(tribe) &&
                   string.Equals(definition.cardTribe, tribe, StringComparison.OrdinalIgnoreCase);
        }

        private static bool RollChance(System.Random random, int percent)
        {
            var chance = percent < 0 ? 0 : percent > 100 ? 100 : percent;
            return random.Next(100) < chance;
        }

        private static void BuffTribeMinions(
            PlayerState player,
            string tribe,
            int attackDelta,
            int healthDelta,
            MinionInstance source,
            bool includeSelf)
        {
            foreach (var boardMinion in player.board)
            {
                if (boardMinion == null || (!includeSelf && ReferenceEquals(boardMinion, source)))
                {
                    continue;
                }

                var definition = CardRegistry.Get(boardMinion.cardId);
                if (!MatchesTribe(definition, tribe))
                {
                    continue;
                }

                ApplyBuff(boardMinion, attackDelta, healthDelta);
            }

            foreach (var handMinion in player.hand)
            {
                if (!includeSelf && ReferenceEquals(handMinion, source))
                {
                    continue;
                }

                var definition = CardRegistry.Get(handMinion.cardId);
                if (!MatchesTribe(definition, tribe))
                {
                    continue;
                }

                ApplyBuff(handMinion, attackDelta, healthDelta);
            }
        }

        private static void ResolveDeathrattleBuffAllAttack(
            MinionInstance minion,
            MinionCardDefinition definition,
            MinionInstance[] board,
            bool isAttackerBoard,
            List<string> log,
            List<CombatEvent> events)
        {
            var buffed = 0;
            for (var i = 0; i < board.Length; i++)
            {
                var ally = board[i];
                if (ally == null || ReferenceEquals(ally, minion) || ally.isDead || ally.health <= 0)
                {
                    continue;
                }

                ally.attack += definition.abilityValue;
                buffed++;
            }

            if (buffed == 0)
            {
                return;
            }

            var message = $"{definition.displayName} gave {buffed} minion(s) +{definition.abilityValue} Attack.";
            log.Add(message);
            events?.Add(new CombatEvent
            {
                type = CombatEventType.Deathrattle,
                message = message,
                isAttackerBoard = isAttackerBoard,
                attackDelta = definition.abilityValue
            });
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

            var summonCount = Math.Max(1, definition.abilityValue);
            for (var summonIndex = 0; summonIndex < summonCount; summonIndex++)
            {
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
                ApplyBuff(board[boardIndex - 1], amount, amount);
            }

            if (boardIndex < board.Length - 1 && board[boardIndex + 1] != null)
            {
                ApplyBuff(board[boardIndex + 1], amount, amount);
            }
        }

        private static void ApplyBuff(MinionInstance minion, int attackDelta, int healthDelta)
        {
            minion.attack += attackDelta;
            minion.health += healthDelta;
            minion.maxHealth += healthDelta;
        }
    }
}