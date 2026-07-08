using System;

namespace DreamGate.Battlegrounds.Services
{
    [Serializable]
    public class MatchSlot
    {
        public int slotIndex;
        public bool isBot;
        public string playerId;
        public string displayName;
    }
}