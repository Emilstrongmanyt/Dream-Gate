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

            var attackerLiving = CountLivingMinions(attackerBoard);
            var defenderLiving = CountLivingMinions(defenderBoard);
            var attackerSideTurn = attackerLiving > defenderLiving;

            var strikeRound = 0;
            while (HasLivingMinions(attackerBoard) && HasLivingMinions(defenderBoard) && strikeRound < 400)
            {
                strikeRound++;

                List<MinionInstance> strikingBoard;
                List<MinionInstance> defendingBoard;
                bool strikingIsAttackerSide;

                if (attackerSideTurn)
                {
                    strikingBoard = attackerBoard;
                    defendingBoard = defenderBoard;
                    strikingIsAttackerSide = true;
                }
                else
                {
                    strikingBoard = defenderBoard;
                    defendingBoard = attackerBoard;
                    strikingIsAttackerSide = false;
                }

                var strikerIndex = GetLeftmostLivingIndex(strikingBoard);
                var targetIndex = GetDefenderTargetIndex(defendingBoard, strikeRound - 1);

                if (strikerIndex < 0 || targetIndex < 0)
                {
                    break;
                }

                var striker = strikingBoard[strikerIndex];
                var target = defendingBoard[targetIndex];
                var targetIsAttackerSide = !strikingIsAttackerSide;

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
                    isAttackerBoard = strikingIsAttackerSide
                });

                target.health -= striker.attack;

                if (target.health > 0)
                {
                    AbilitySystem.OnMinionDamaged(
                        target,
                        targetIndex,
                        defendingBoard,
                        targetIsAttackerSide,
                        result.combatLog,
                        result.combatEvents);
                }

                if (AbilitySystem.HasCleave(striker))
                {
                    ApplyCleave(
                        striker,
                        strikerIndex,
                        strikingIsAttackerSide,
                        defendingBoard,
                        targetIndex,
                        targetIsAttackerSide,
                        result);
                }

                ResolveDeath(target, defendingBoard, targetIsAttackerSide, result);
                attackerSideTurn = !attackerSideTurn;
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
            MinionInstance striker,
            int strikerIndex,
            bool strikerIsAttackerSide,
            List<MinionInstance> defendingBoard,
            int primaryIndex,
            bool targetIsAttackerSide,
            CombatResult result)
        {
            var cleaveTargets = new List<int>();
            if (primaryIndex - 1 >= 0)
            {
                cleaveTargets.Add(primaryIndex - 1);
            }

            if (primaryIndex + 1 < defendingBoard.Count)
            {
                cleaveTargets.Add(primaryIndex + 1);
            }

            foreach (var index in cleaveTargets)
            {
                var target = defendingBoard[index];
                if (target.isDead || target.health <= 0)
                {
                    continue;
                }

                target.health -= striker.attack;
                var message = $"{GetName(striker)} cleaves {GetName(target)} for {striker.attack}.";
                result.combatLog.Add(message);
                result.combatEvents.Add(new CombatEvent
                {
                    type = CombatEventType.Attack,
                    message = message,
                    attackerBoardIndex = strikerIndex,
                    defenderBoardIndex = index,
                    isAttackerBoard = strikerIsAttackerSide,
                    isCleave = true
                });

                if (target.health > 0)
                {
                    AbilitySystem.OnMinionDamaged(
                        target,
                        index,
                        defendingBoard,
                        targetIsAttackerSide,
                        result.combatLog,
                        result.combatEvents);
                }

                ResolveDeath(target, defendingBoard, targetIsAttackerSide, result);
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

        private static int GetLeftmostLivingIndex(List<MinionInstance> board)
        {
            return GetNextLivingIndex(board, 0);
        }

        private static int CountLivingMinions(List<MinionInstance> board)
        {
            var count = 0;
            foreach (var minion in board)
            {
                if (!minion.isDead && minion.health > 0)
                {
                    count++;
                }
            }

            return count;
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