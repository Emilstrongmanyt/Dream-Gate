namespace DreamGate.Battlegrounds.Campaign
{
    public sealed class CampaignMissionDefinition
    {
        public int level;
        public string missionId;
        public string bossHeroId;
        public string bossDisplayName;
        public int opponentCount = 1;
        public string allyBossHeroId;
        public string allyBossDisplayName;
        public int bonusTavernTier;
        public int bonusGoldPerTurn;
        public int bonusUpgradeChancePercent;
        public string unlockHeroId;
    }
}