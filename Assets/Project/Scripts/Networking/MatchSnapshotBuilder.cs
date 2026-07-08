using System.Linq;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Combat;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Players;

namespace DreamGate.Battlegrounds.Networking
{
    public static class MatchSnapshotBuilder
    {
        public static MatchSnapshot Build(MatchManager manager, int version, int viewerSlotIndex)
        {
            var snapshot = new MatchSnapshot
            {
                version = version,
                turn = manager.Turn,
                phase = (int)manager.Phase,
                recruitTimeRemaining = manager.RecruitTimeRemaining,
                localSlotIndex = viewerSlotIndex,
                awaitingCombat = manager.IsAwaitingCombatPlayback,
                matchEnded = manager.Phase == MatchPhase.GameOver,
                players = manager.Players.Select(BuildPlayer).ToArray()
            };

            if (manager.IsAwaitingCombatPlayback && manager.PendingHumanCombat != null && manager.PendingOpponent != null)
            {
                snapshot.pendingCombat = BuildCombat(manager.PendingHumanCombat, manager.PendingOpponent);
            }

            if (manager.FinalResult != null)
            {
                snapshot.matchEnd = new MatchEndSnapshot
                {
                    playerWon = manager.FinalResult.playerWon,
                    placement = manager.FinalResult.placement,
                    turnsPlayed = manager.FinalResult.turnsPlayed,
                    finalHeroHealth = manager.FinalResult.finalHeroHealth,
                    damageDealt = manager.FinalResult.damageDealt,
                    damageTaken = manager.FinalResult.damageTaken,
                    heroName = manager.FinalResult.heroName
                };
            }

            return snapshot;
        }

        private static PlayerSnapshot BuildPlayer(PlayerState player)
        {
            return new PlayerSnapshot
            {
                playerId = player.playerId,
                displayName = player.displayName,
                heroId = player.heroId,
                heroName = player.heroName,
                isHuman = player.isHuman,
                isEliminated = player.isEliminated,
                placement = player.placement,
                heroHealth = player.heroHealth,
                damageDealt = player.damageDealt,
                damageTaken = player.damageTaken,
                gold = player.gold,
                tavernTier = player.tavernTier,
                doomNextCombat = player.doomNextCombat,
                board = CloneBoard(player.board),
                hand = player.hand.Select(m => m.Clone()).ToArray(),
                shopCardIds = player.shopCardIds.ToArray()
            };
        }

        private static MinionInstance[] CloneBoard(MinionInstance[] board)
        {
            var clone = new MinionInstance[board.Length];
            for (var i = 0; i < board.Length; i++)
            {
                clone[i] = board[i]?.Clone();
            }

            return clone;
        }

        private static CombatSnapshot BuildCombat(CombatResult combat, PlayerState opponent)
        {
            return new CombatSnapshot
            {
                opponentPlayerId = opponent.playerId,
                opponentDisplayName = opponent.displayName,
                opponentHeroName = opponent.heroName,
                outcome = (int)combat.outcome,
                damageToDefender = combat.damageToDefender,
                damageToAttacker = combat.damageToAttacker,
                events = combat.combatEvents.Select(e => new CombatEventSnapshot
                {
                    type = (int)e.type,
                    attackerSlot = e.attackerBoardIndex,
                    defenderSlot = e.defenderBoardIndex,
                    isRecoil = e.isRecoil,
                    damage = e.damageAmount
                }).ToArray()
            };
        }
    }
}