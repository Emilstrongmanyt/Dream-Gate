using System.Collections.Generic;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Players;

namespace DreamGate.Battlegrounds.Combat
{
    public enum CombatOutcome
    {
        Draw,
        AttackerWins,
        DefenderWins
    }

    public class CombatResult
    {
        public CombatOutcome outcome;
        public PlayerState attackerSnapshot;
        public PlayerState defenderSnapshot;
        public List<string> combatLog = new();
        public List<CombatEvent> combatEvents = new();
        public MinionInstance[] attackerBoardStart = new MinionInstance[MatchConfig.BoardSize];
        public MinionInstance[] defenderBoardStart = new MinionInstance[MatchConfig.BoardSize];
        public int damageToDefender;
        public int damageToAttacker;
    }
}