using DreamGate.Battlegrounds.Core;
using UnityEngine;

namespace DreamGate.Battlegrounds.Networking
{
    /// <summary>
    /// Placeholder for UGS Relay + Netcode host. Falls back to local simulation until wired.
    /// </summary>
    public class NetworkMatchHostStub : INetworkMatchHost
    {
        private readonly LocalMatchHost fallback = new();
        private MatchManager matchManager;

        public bool IsServer => fallback.IsServer;
        public MatchMode Mode => MatchMode.Rated;

        public void InitializeMatch(MatchManager manager)
        {
            matchManager = manager;
            fallback.InitializeMatch(manager);
            Debug.Log("NetworkMatchHostStub active — using local simulation until UGS Relay is configured.");
        }

        public void TickRecruitTimer(float deltaTime)
        {
            fallback.TickRecruitTimer(deltaTime);
        }

        public void Dispose()
        {
            fallback.Dispose();
            matchManager = null;
        }
    }
}