using System.Collections.Generic;

namespace DreamGate.Battlegrounds.Networking
{
    public interface IMatchActionRelay
    {
        bool IsAuthoritative { get; }
        bool TryRelayAction(string action, int playerId, Dictionary<string, int> payload, out string message);
        bool TryRelayCompleteCombat(out string message);
    }
}