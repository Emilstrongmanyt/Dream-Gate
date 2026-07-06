using DreamGate.Battlegrounds.Core;

namespace DreamGate.Battlegrounds.Networking
{
    public class LocalMatchHost : INetworkMatchHost
    {
        private MatchManager matchManager;

        public bool IsServer => true;
        public MatchMode Mode => matchManager?.Mode ?? MatchMode.Practice;

        public void InitializeMatch(MatchManager manager)
        {
            matchManager = manager;
        }

        public void TickRecruitTimer(float deltaTime)
        {
            matchManager?.TickRecruitTimer(deltaTime);
        }

        public void Dispose()
        {
            matchManager = null;
        }
    }
}