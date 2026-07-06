using System.Collections.Generic;
using DreamGate.Battlegrounds.Cards;
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
        public List<MinionInstance> attackerBoardStart = new();
        public List<MinionInstance> defenderBoardStart = new();
        public int damageToDefender;
        public int damageToAttacker;
    }
}