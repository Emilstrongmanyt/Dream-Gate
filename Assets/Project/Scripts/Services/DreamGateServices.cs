using DreamGate.Battlegrounds.Core;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services
{
    /// <summary>
    /// Local-first player services. UGS auth and cloud save can be layered on when linked in the dashboard.
    /// </summary>
    public static class DreamGateServices
    {
        public static bool IsInitialized { get; private set; }
        public static bool IsCloudLinked { get; private set; }
        public static PlayerProfile Profile { get; private set; }

        public static void Initialize()
        {
            Profile = ProfileStore.Load();
            IsInitialized = true;
            IsCloudLinked = false;
        }

        public static void ApplyRatedResult(MatchResult result)
        {
            if (!IsInitialized || result == null || result.matchMode != MatchMode.Rated)
            {
                return;
            }

            var mmrBefore = Profile.mmr;
            var delta = MmrCalculator.CalculateDelta(result.placement, mmrBefore);
            Profile.mmr = Mathf.Max(0, mmrBefore + delta);
            Profile.ratedGamesPlayed++;
            Profile.wins += result.placement == 1 ? 1 : 0;
            Profile.top4Finishes += result.placement <= 4 ? 1 : 0;
            ProfileStore.Save(Profile);

            result.mmrBefore = mmrBefore;
            result.mmrAfter = Profile.mmr;
            result.mmrDelta = delta;
        }

        public static string GetStatusLine()
        {
            if (!IsInitialized)
            {
                return "Services not ready";
            }

            return IsCloudLinked
                ? $"Cloud linked | MMR {Profile.mmr}"
                : $"Local profile | MMR {Profile.mmr}";
        }
    }
}