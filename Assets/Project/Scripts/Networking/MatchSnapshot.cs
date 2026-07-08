using System;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Core;

namespace DreamGate.Battlegrounds.Networking
{
    [Serializable]
    public class MatchSnapshot
    {
        public int version;
        public int turn;
        public int phase;
        public float recruitTimeRemaining;
        public int localSlotIndex;
        public bool awaitingCombat;
        public bool matchEnded;
        public PlayerSnapshot[] players = Array.Empty<PlayerSnapshot>();
        public CombatSnapshot pendingCombat;
        public MatchEndSnapshot matchEnd;
    }

    [Serializable]
    public class PlayerSnapshot
    {
        public int playerId;
        public string displayName;
        public string heroId;
        public string heroName;
        public bool isHuman;
        public bool isEliminated;
        public int placement;
        public int heroHealth;
        public int damageDealt;
        public int damageTaken;
        public int gold;
        public int tavernTier;
        public bool doomNextCombat;
        public MinionInstance[] board = new MinionInstance[MatchConfig.BoardSize];
        public MinionInstance[] hand = Array.Empty<MinionInstance>();
        public string[] shopCardIds = Array.Empty<string>();
    }

    [Serializable]
    public class CombatSnapshot
    {
        public int opponentPlayerId;
        public string opponentDisplayName;
        public string opponentHeroName;
        public int outcome;
        public int damageToDefender;
        public int damageToAttacker;
        public CombatEventSnapshot[] events = Array.Empty<CombatEventSnapshot>();
    }

    [Serializable]
    public class CombatEventSnapshot
    {
        public int type;
        public int attackerSlot;
        public int defenderSlot;
        public bool isRecoil;
        public int damage;
    }

    [Serializable]
    public class MatchEndSnapshot
    {
        public bool playerWon;
        public int placement;
        public int turnsPlayed;
        public int finalHeroHealth;
        public int damageDealt;
        public int damageTaken;
        public string heroName;
    }
}