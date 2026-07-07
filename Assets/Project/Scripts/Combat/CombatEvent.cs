namespace DreamGate.Battlegrounds.Combat
{
    public enum CombatEventType
    {
        Start,
        Attack,
        Death,
        Deathrattle,
        End
    }

    public struct CombatEvent
    {
        public CombatEventType type;
        public string message;
        public int attackerBoardIndex;
        public int defenderBoardIndex;
        public int boardIndex;
        public bool isAttackerBoard;
        public bool isCleave;
        public bool isRecoil;
        public int damageAmount;
        public int attackDelta;
        public int healthDelta;
        public string abilityCardId;
    }
}