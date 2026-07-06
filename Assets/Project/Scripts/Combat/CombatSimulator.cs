using System.Collections.Generic;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Players;

namespace DreamGate.Battlegrounds.Combat
{
    public static class CombatSimulator
    {
        public static CombatResult Simulate(PlayerState attacker, PlayerState defender)
        {
            var result = new CombatResult
            {
                attackerSnapshot = CloneForCombat(attacker),
                defenderSnapshot = CloneForCombat(defender)
            };

            var attackerBoard = result.attackerSnapshot.board;
            var defenderBoard = result.defenderSnapshot.board;

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

            if (attackerBoard.Count == 0 && defenderBoard.Count == 0)
            {
                result.outcome = CombatOutcome.Draw;
                result.combatLog.Add("Both boards empty. No combat.");
                AddEndEvent(result);
                return result;
            }

            if (attackerBoard.Count == 0)
            {
                result.outcome = CombatOutcome.DefenderWins;
                result.combatLog.Add("Attacker has no minions.");
                AddEndEvent(result);
                return result;
            }

            if (defenderBoard.Count == 0)
            {
                result.outcome = CombatOutcome.AttackerWins;
                result.combatLog.Add("Defender has no minions.");
                AddEndEvent(result);
                return result;
            }

            var round = 0;
            while (HasLivingMinions(attackerBoard) && HasLivingMinions(defenderBoard) && round < 200)
            {
                round++;
                var attackerIndex = GetNextLivingIndex(attackerBoard, round - 1);
                var defenderIndex = GetDefenderTargetIndex(defenderBoard, round - 1);

                if (attackerIndex < 0 || defenderIndex < 0)
                {
                    break;
                }

                var attackerMinion = attackerBoard[attackerIndex];
                var defenderMinion = defenderBoard[defenderIndex];

                var attackMessage =
                    $"{GetName(attackerMinion)} ({attackerMinion.attack}/{attackerMinion.health}) attacks " +
                    $"{GetName(defenderMinion)} ({defenderMinion.attack}/{defenderMinion.health}).";
                result.combatLog.Add(attackMessage);
                result.combatEvents.Add(new CombatEvent
                {
                    type = CombatEventType.Attack,
                    message = attackMessage,
                    attackerBoardIndex = attackerIndex,
                    defenderBoardIndex = defenderIndex
                });

                defenderMinion.health -= attackerMinion.attack;
                attackerMinion.health -= defenderMinion.attack;

                if (defenderMinion.health > 0)
                {
                    AbilitySystem.OnMinionDamaged(
                        defenderMinion,
                        defenderIndex,
                        defenderBoard,
                        false,
                        result.combatLog,
                        result.combatEvents);
                }

                if (attackerMinion.health > 0)
                {
                    AbilitySystem.OnMinionDamaged(
                        attackerMinion,
                        attackerIndex,
                        attackerBoard,
                        true,
                        result.combatLog,
                        result.combatEvents);
                }

                if (AbilitySystem.HasCleave(attackerMinion))
                {
                    ApplyCleave(attackerMinion, attackerBoard, attackerIndex, defenderBoard, defenderIndex, result);
                }

                ResolveDeath(defenderMinion, defenderBoard, false, result);
                ResolveDeath(attackerMinion, attackerBoard, true, result);
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

        private static void ApplyCleave(
            MinionInstance attackerMinion,
            List<MinionInstance> attackerBoard,
            int attackerIndex,
            List<MinionInstance> defenderBoard,
            int primaryIndex,
            CombatResult result)
        {
            var cleaveTargets = new List<int>();
            if (primaryIndex - 1 >= 0)
            {
                cleaveTargets.Add(primaryIndex - 1);
            }

            if (primaryIndex + 1 < defenderBoard.Count)
            {
                cleaveTargets.Add(primaryIndex + 1);
            }

            foreach (var index in cleaveTargets)
            {
                var target = defenderBoard[index];
                if (target.isDead || target.health <= 0)
                {
                    continue;
                }

                target.health -= attackerMinion.attack;
                var message = $"{GetName(attackerMinion)} cleaves {GetName(target)} for {attackerMinion.attack}.";
                result.combatLog.Add(message);
                result.combatEvents.Add(new CombatEvent
                {
                    type = CombatEventType.Attack,
                    message = message,
                    attackerBoardIndex = attackerIndex,
                    defenderBoardIndex = index,
                    isAttackerBoard = true,
                    isCleave = true
                });

                if (target.health > 0)
                {
                    AbilitySystem.OnMinionDamaged(
                        target,
                        index,
                        defenderBoard,
                        false,
                        result.combatLog,
                        result.combatEvents);
                }

                ResolveDeath(target, defenderBoard, false, result);
            }
        }

        private static void ResolveDeath(
            MinionInstance minion,
            List<MinionInstance> board,
            bool isAttackerBoard,
            CombatResult result)
        {
            if (minion.health > 0 || minion.isDead)
            {
                return;
            }

            minion.isDead = true;
            var boardIndex = board.IndexOf(minion);
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

        private static void AddEndEvent(CombatResult result)
        {
            var message = $"Combat result: {result.outcome}";
            result.combatLog.Add(message);
            result.combatEvents.Add(new CombatEvent { type = CombatEventType.End, message = message });
        }

        private static int GetDefenderTargetIndex(List<MinionInstance> board, int preferredIndex)
        {
            var tauntIndices = new List<int>();
            for (var i = 0; i < board.Count; i++)
            {
                var minion = board[i];
                if (!minion.isDead && minion.health > 0 && AbilitySystem.HasTaunt(minion))
                {
                    tauntIndices.Add(i);
                }
            }

            if (tauntIndices.Count > 0)
            {
                return tauntIndices[preferredIndex % tauntIndices.Count];
            }

            return GetNextLivingIndex(board, preferredIndex);
        }

        private static List<MinionInstance> CloneBoardMinions(List<MinionInstance> board)
        {
            var clone = new List<MinionInstance>(board.Count);
            foreach (var minion in board)
            {
                clone.Add(minion.Clone());
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

            foreach (var minion in source.board)
            {
                clone.board.Add(minion.Clone());
            }

            return clone;
        }

        private static bool HasLivingMinions(List<MinionInstance> board)
        {
            foreach (var minion in board)
            {
                if (!minion.isDead && minion.health > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetNextLivingIndex(List<MinionInstance> board, int preferredIndex)
        {
            if (board.Count == 0)
            {
                return -1;
            }

            var index = preferredIndex % board.Count;
            for (var i = 0; i < board.Count; i++)
            {
                var candidate = board[(index + i) % board.Count];
                if (!candidate.isDead && candidate.health > 0)
                {
                    return (index + i) % board.Count;
                }
            }

            return -1;
        }

        private static void CleanupDeadMinions(List<MinionInstance> board)
        {
            board.RemoveAll(m => m.isDead || m.health <= 0);
        }

        private static string GetName(MinionInstance minion)
        {
            var definition = CardRegistry.Get(minion.cardId);
            var name = definition != null ? definition.displayName : minion.cardId;
            return minion.isGolden ? $"Golden {name}" : name;
        }
    }
}