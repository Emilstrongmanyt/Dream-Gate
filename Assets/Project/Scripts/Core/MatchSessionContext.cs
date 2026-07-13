using DreamGate.Battlegrounds.Campaign;
using DreamGate.Battlegrounds.Services;

namespace DreamGate.Battlegrounds.Core
{
    /// <summary>
    /// Carries match configuration across scene loads (lobby → game).
    /// </summary>
    public static class MatchSessionContext
    {
        public static MatchMode Mode { get; private set; } = MatchMode.Practice;
        public static int MatchSeed { get; private set; } = -1;
        public static string LobbyId { get; private set; }
        public static int HumanPlayerMmr { get; private set; } = 1500;
        public static int HumanSlotIndex { get; private set; }
        public static int HumanCount { get; private set; } = 1;
        public static bool UsedBotFill { get; private set; }
        public static string MatchServerUrl { get; private set; }
        public static MatchSlot[] Slots { get; private set; } = System.Array.Empty<MatchSlot>();
        public static CampaignMissionDefinition ActiveCampaignMission { get; private set; }

        public static void BeginPractice()
        {
            Mode = MatchMode.Practice;
            MatchSeed = -1;
            LobbyId = null;
            HumanPlayerMmr = 1500;
            HumanSlotIndex = 0;
            HumanCount = 1;
            UsedBotFill = false;
            MatchServerUrl = null;
            Slots = System.Array.Empty<MatchSlot>();
            ActiveCampaignMission = null;
        }

        public static void BeginCampaign(CampaignMissionDefinition mission)
        {
            Mode = MatchMode.Campaign;
            MatchSeed = mission != null ? mission.level * 10007 : -1;
            LobbyId = null;
            HumanPlayerMmr = 1500;
            HumanSlotIndex = 0;
            HumanCount = 1;
            UsedBotFill = false;
            MatchServerUrl = null;
            ActiveCampaignMission = mission;
            Slots = System.Array.Empty<MatchSlot>();
        }

        public static void BeginRated(MatchmakingResult result, int playerMmr)
        {
            Mode = MatchMode.Rated;
            LobbyId = result?.lobbyId;
            MatchSeed = result?.matchSeed ?? -1;
            HumanPlayerMmr = playerMmr;
            HumanSlotIndex = result?.humanSlotIndex ?? 0;
            HumanCount = result?.humanCount ?? 1;
            UsedBotFill = result?.usedBotFill ?? true;
            MatchServerUrl = result?.matchServerUrl;
            Slots = result?.slots ?? System.Array.Empty<MatchSlot>();
            ActiveCampaignMission = null;
        }

        public static void Clear()
        {
            BeginPractice();
        }
    }
}