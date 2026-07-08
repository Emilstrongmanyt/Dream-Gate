using System.Collections.Generic;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Players;

namespace DreamGate.Battlegrounds.Combat
{
    public static class CombatSimulator
    {
        public static CombatResult Simulate(PlayerState attacker, PlayerState defender, int? seed = null)
        {
            var random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            var result = new CombatResult
            {
                attackerSnapshot = CloneForCombat(attacker),
                defenderSnapshot = CloneForCombat(defender)
            };

            var attackerBoard = result.attackerSnapshot.board;
            var defenderBoard = result.defenderSnapshot.board;

            if (attacker.doomNextCombat)
            {
                ApplyDoomTransform(defenderBoard, random, result);
                attacker.doomNextCombat = false;
            }

            result.combatEvents.Add(new CombatEvent
            {
                type = CombatEventType.Start,
                message = $"{attacker.displayName} vs {defender.displayName}"
            });
            result.combatLog.Add($"{attacker.displayName} vs {defender.displayName}");

            AbilitySystem.OnStartOfCombat(attackerBoard, true, result.combatLog, result.combatEvents);
            AbilitySystem.OnStartOfCombat(defenderBoard, false, result.combatLog, result.combatEvents);
            result.attackerBoardStart = CloneBoardMinions(attackerBoard);
            result.defenderBoardStart = CloneBoardMinions(defenderBoard);

            if (!HasLivingMinions(attackerBoard) && !HasLivingMinions(defenderBoard))
            {
                result.outcome = CombatOutcome.Draw;
                result.combatLog.Add("Both boards empty. No combat.");
                AddEndEvent(result);
                return result;
            }

            if (!HasLivingMinions(attackerBoard))
            {
                result.outcome = CombatOutcome.DefenderWins;
                result.combatLog.Add("Attacker has no minions.");
                AddEndEvent(result);
                return result;
            }

            if (!HasLivingMinions(defenderBoard))
            {
                result.outcome = CombatOutcome.AttackerWins;
                result.combatLog.Add("Defender has no minions.");
                AddEndEvent(result);
                return result;
            }

            var wave = 0;
            while (HasLivingMinions(attackerBoard) && HasLivingMinions(defenderBoard) && wave < 400)
            {
                wave++;
                for (var slot = 0; slot < MatchConfig.BoardSize; slot++)
                {
                    if (!HasLivingMinions(attackerBoard) || !HasLivingMinions(defenderBoard))
                    {
                        break;
                    }

                    ExecuteSlotAttacks(
                        attackerBoard,
                        defenderBoard,
                        slot,
                        strikingIsAttackerSide: true,
                        random,
                        result);

                    if (!HasLivingMinions(attackerBoard) || !HasLivingMinions(defenderBoard))
                    {
                        break;
                    }

                    ExecuteSlotAttacks(
                        defenderBoard,
                        attackerBoard,
                        slot,
                        strikingIsAttackerSide: false,
                        random,
                        result);
                }
            }

            var attackerAlive = HasLivingMinions(attackerBoard);
            var defenderAlive = HasLivingMinions(defenderBoard);

            if (attackerAlive && !defenderAlive)
            {
                result.outcome = CombatOutcome.AttackerWins;
            }
            else if (!attackerAlive && defenderAlive)
            {
                result.outcome = CombatOutcome.DefenderWins;
            }
            else
            {
                result.outcome = CombatOutcome.Draw;
            }

            CleanupDeadMinions(attackerBoard);
            CleanupDeadMinions(defenderBoard);
            AddEndEvent(result);
            return result;
        }

        private static void ExecuteSlotAttacks(
            MinionInstance[] strikingBoard,
            MinionInstance[] defendingBoard,
            int slot,
            bool strikingIsAttackerSide,
            System.Random random,
            CombatResult result)
        {
            var striker = strikingBoard[slot];
            if (!IsLiving(striker))
            {
                return;
            }

            var attacks = AbilitySystem.GetAttacksPerCombatTurn(striker);
            for (var attackIndex = 0; attackIndex < attacks; attackIndex++)
            {
                if (!IsLiving(striker) || !HasLivingMinions(defendingBoard))
                {
                    break;
                }

                var targetIndex = PickRandomTarget(defendingBoard, random);
                if (targetIndex < 0)
                {
                    break;
                }

                var target = defendingBoard[targetIndex];
                var targetIsAttackerSide = !strikingIsAttackerSide;
                PerformStrike(
                    striker,
                    strikerIndex: slot,
                    target,
                    targetIndex,
                    strikingIsAttackerSide,
                    targetIsAttackerSide,
                    strikingBoard,
                    defendingBoard,
                    random,
                    result);
            }
        }

        private static void PerformStrike(
            MinionInstance striker,
            int strikerIndex,
            MinionInstance target,
            int targetIndex,
            bool strikerIsAttackerSide,
            bool targetIsAttackerSide,
            MinionInstance[] strikingBoard,
            MinionInstance[] defendingBoard,
            System.Random random,
            CombatResult result)
        {
            var attackMessage =
                $"{GetName(striker)} ({striker.attack}/{striker.health}) attacks " +
                $"{GetName(target)} ({target.attack}/{target.health}).";
            result.combatLog.Add(attackMessage);
            result.combatEvents.Add(new CombatEvent
            {
                type = CombatEventType.Attack,
                message = attackMessage,
                attackerBoardIndex = strikerIndex,
                defenderBoardIndex = targetIndex,
                isAttackerBoard = strikerIsAttackerSide,
                damageAmount = striker.attack
            });

            var damageDealt = AbilitySystem.ApplyDamage(target, striker.attack, result.combatLog, result.combatEvents, targetIndex, targetIsAttackerSide);

            if (target.health > 0 && damageDealt > 0)
            {
                AbilitySystem.OnMinionDamaged(
                    target,
                    targetIndex,
                    defendingBoard,
                    targetIsAttackerSide,
                    damageDealt,
                    random,
                    result.combatLog,
                    result.combatEvents);
            }

            if (AbilitySystem.HasCleave(striker))
            {
                ApplyCleave(
                    striker,
                    strikerIndex,
                    strikerIsAttackerSide,
                    defendingBoard,
                    targetIndex,
                    targetIsAttackerSide,
                    random,
                    result);
            }

            ResolveDeath(target, defendingBoard, targetIndex, targetIsAttackerSide, result);

            var recoilDamage = target.attack;
            if (recoilDamage > 0 && IsLiving(striker))
            {
                var recoilDealt = AbilitySystem.ApplyDamage(striker, recoilDamage, result.combatLog, result.combatEvents, strikerIndex, strikerIsAttackerSide);
                var recoilMessage =
                    $"{GetName(striker)} takes {recoilDamage} recoil from {GetName(target)}.";
                result.combatLog.Add(recoilMessage);
                result.combatEvents.Add(new CombatEvent
                {
                    type = CombatEventType.Attack,
                    message = recoilMessage,
                    attackerBoardIndex = strikerIndex,
                    defenderBoardIndex = targetIndex,
                    isAttackerBoard = strikerIsAttackerSide,
                    isRecoil = true,
                    damageAmount = recoilDamage
                });

                if (striker.health > 0 && recoilDealt > 0)
                {
                    AbilitySystem.OnMinionDamaged(
                        striker,
                        strikerIndex,
                        strikingBoard,
                        strikerIsAttackerSide,
                        recoilDealt,
                        random,
                        result.combatLog,
                        result.combatEvents);
                }

                ResolveDeath(striker, strikingBoard, strikerIndex, strikerIsAttackerSide, result);
            }
        }

        private static void ApplyCleave(
            MinionInstance striker,
            int strikerIndex,
            bool strikerIsAttackerSide,
            MinionInstance[] defendingBoard,
            int primaryIndex,
            bool targetIsAttackerSide,
            System.Random random,
            CombatResult result)
        {
            var cleaveTargets = new List<int>();
            if (primaryIndex - 1 >= 0)
            {
                cleaveTargets.Add(primaryIndex - 1);
            }

            if (primaryIndex + 1 < defendingBoard.Length)
            {
                cleaveTargets.Add(primaryIndex + 1);
            }

            foreach (var index in cleaveTargets)
            {
                var target = defendingBoard[index];
                if (!IsLiving(target))
                {
                    continue;
                }

                var cleaveDamage = AbilitySystem.ApplyDamage(target, striker.attack, result.combatLog, result.combatEvents, index, targetIsAttackerSide);
                var message = $"{GetName(striker)} cleaves {GetName(target)} for {striker.attack}.";
                result.combatLog.Add(message);
                result.combatEvents.Add(new CombatEvent
                {
                    type = CombatEventType.Attack,
                    message = message,
                    attackerBoardIndex = strikerIndex,
                    defenderBoardIndex = index,
                    isAttackerBoard = strikerIsAttackerSide,
                    isCleave = true,
                    damageAmount = striker.attack
                });

                if (target.health > 0 && cleaveDamage > 0)
                {
                    AbilitySystem.OnMinionDamaged(
                        target,
                        index,
                        defendingBoard,
                        targetIsAttackerSide,
                        cleaveDamage,
                        random,
                        result.combatLog,
                        result.combatEvents);
                }

                ResolveDeath(target, defendingBoard, index, targetIsAttackerSide, result);
            }
        }

        private static void ResolveDeath(
            MinionInstance minion,
            MinionInstance[] board,
            int boardIndex,
            bool isAttackerBoard,
            CombatResult result)
        {
            if (minion == null || minion.health > 0 || minion.isDead)
            {
                return;
            }

            minion.isDead = true;
            var deathMessage = $"{GetName(minion)} died.";
            result.combatLog.Add(deathMessage);
            result.combatEvents.Add(new CombatEvent
            {
                type = CombatEventType.Death,
                message = deathMessage,
                boardIndex = boardIndex,
                isAttackerBoard = isAttackerBoard
            });
            AbilitySystem.OnMinionDeath(minion, board, isAttackerBoard, result.combatLog, result.combatEvents);
        }

        private static int PickRandomTarget(MinionInstance[] board, System.Random random)
        {
            var tauntIndices = new List<int>();
            var livingIndices = new List<int>();

            for (var i = 0; i < board.Length; i++)
            {
                var minion = board[i];
                if (!IsLiving(minion))
                {
                    continue;
                }

                livingIndices.Add(i);
                if (AbilitySystem.HasTaunt(minion))
                {
                    tauntIndices.Add(i);
                }
            }

            if (livingIndices.Count == 0)
            {
                return -1;
            }

            var pool = tauntIndices.Count > 0 ? tauntIndices : livingIndices;
            return pool[random.Next(pool.Count)];
        }

        private static void AddEndEvent(CombatResult result)
        {
            var message = $"Combat result: {result.outcome}";
            result.combatLog.Add(message);
            result.combatEvents.Add(new CombatEvent { type = CombatEventType.End, message = message });
        }

        private static MinionInstance[] CloneBoardMinions(MinionInstance[] board)
        {
            var clone = new MinionInstance[MatchConfig.BoardSize];
            for (var i = 0; i < board.Length; i++)
            {
                clone[i] = board[i] != null ? board[i].Clone() : null;
            }

            return clone;
        }

        private static PlayerState CloneForCombat(PlayerState source)
        {
            var clone = new PlayerState
            {
                playerId = source.playerId,
                displayName = source.displayName,
                heroId = source.heroId,
                heroName = source.heroName,
                isHuman = source.isHuman,
                heroHealth = source.heroHealth,
                gold = source.gold,
                tavernTier = source.tavernTier
            };

            for (var i = 0; i < source.board.Length; i++)
            {
                clone.board[i] = source.board[i] != null ? source.board[i].Clone() : null;
            }

            return clone;
        }

        private static bool IsLiving(MinionInstance minion)
        {
            return minion != null && !minion.isDead && minion.health > 0;
        }

        private static bool HasLivingMinions(MinionInstance[] board)
        {
            foreach (var minion in board)
            {
                if (IsLiving(minion))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CleanupDeadMinions(MinionInstance[] board)
        {
            for (var i = 0; i < board.Length; i++)
            {
                if (board[i] != null && (board[i].isDead || board[i].health <= 0))
                {
                    board[i] = null;
                }
            }
        }

        private static void ApplyDoomTransform(MinionInstance[] defenderBoard, System.Random random, CombatResult result)
        {
            var living = new List<int>();
            for (var i = 0; i < defenderBoard.Length; i++)
            {
                if (IsLiving(defenderBoard[i]))
                {
                    living.Add(i);
                }
            }

            if (living.Count == 0)
            {
                return;
            }

            var slot = living[random.Next(living.Count)];
            var snailDefinition = CardRegistry.Get("snail");
            if (snailDefinition == null)
            {
                return;
            }

            var doomed = MinionInstance.FromDefinition(snailDefinition);
            defenderBoard[slot] = doomed;
            var message = $"Doom transformed an enemy minion into {snailDefinition.displayName} ({doomed.attack}/{doomed.health}).";
            result.combatLog.Add(message);
            result.combatEvents.Add(new CombatEvent
            {
                type = CombatEventType.Start,
                message = message,
                boardIndex = slot,
                isAttackerBoard = false,
                abilityCardId = snailDefinition.cardId
            });
        }

        private static string GetName(MinionInstance minion)
        {
            var definition = CardRegistry.Get(minion.cardId);
            var name = definition != null ? definition.displayName : minion.cardId;
            return minion.isGolden ? $"Golden {name}" : name;
        }
    }
}