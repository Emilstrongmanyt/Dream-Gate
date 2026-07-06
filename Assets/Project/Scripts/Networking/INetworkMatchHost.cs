using DreamGate.Battlegrounds.Core;

namespace DreamGate.Battlegrounds.Networking
{
    /// <summary>
    /// Host abstraction for local practice vs future server-authoritative rated matches.
    /// </summary>
    public interface INetworkMatchHost
    {
        bool IsServer { get; }
        MatchMode Mode { get; }

        void InitializeMatch(MatchManager matchManager);
        void TickRecruitTimer(float deltaTime);
        void Dispose();
    }
}