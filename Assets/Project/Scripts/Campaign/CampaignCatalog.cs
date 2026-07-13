using System.Collections.Generic;
using DreamGate.Battlegrounds.Heroes;

namespace DreamGate.Battlegrounds.Campaign
{
    public static class CampaignCatalog
    {
        private static readonly CampaignMissionDefinition[] Missions =
        {
            Mission(1, "Ayan", "Ayan", 1, 0, 0, 10),
            Mission(2, "Evan", "Evan", 1, 0, 0, 15),
            Mission(3, "Garnox", "Garnox", 1, 0, 1, 20),
            Mission(4, "GrandmaYeonHero", "Grandma Yeon", 1, 1, 1, 25),
            Mission(5, "Grendel", "Grendel", 1, 1, 1, 30),
            Mission(6, "HeenaHero", "Heena", 2, "KentaHero", "Kenta", 1, 1, 35),
            Mission(7, "KentaHero", "Kenta", 1, 1, 2, 40),
            Mission(8, "Luke the Security Guard", "Luke", 1, 2, 2, 45),
            Mission(9, "Magician", "Magician", 2, "Marco", "Marco", 2, 2, 50),
            Mission(10, "Marco", "Marco", 1, 2, 2, 55),
            Mission(11, "Olaf", "Olaf", 2, "RudiHero", "Rudi", 2, 3, 60),
            Mission(12, "RudiHero", "Rudi", 1, 3, 3, 65),
            Mission(13, "Warrior", "Warrior", 2, "Grendel", "Grendel", 3, 3, 70)
        };

        public static IReadOnlyList<CampaignMissionDefinition> All => Missions;

        public static CampaignMissionDefinition GetByLevel(int level)
        {
            if (level < 1 || level > Missions.Length)
            {
                return null;
            }

            return Missions[level - 1];
        }

        public static CampaignMissionDefinition GetById(string missionId)
        {
            foreach (var mission in Missions)
            {
                if (mission.missionId == missionId)
                {
                    return mission;
                }
            }

            return null;
        }

        public static bool IsMissionUnlocked(int campaignHighestLevel, int missionLevel) =>
            missionLevel <= 1 || campaignHighestLevel >= missionLevel - 1;

        private static CampaignMissionDefinition Mission(
            int level,
            string portraitAsset,
            string displayName,
            int opponentCount,
            int bonusTier,
            int bonusGold,
            int upgradeChancePercent)
        {
            var heroId = HeroRegistry.BuildPortraitHeroId(portraitAsset);
            return new CampaignMissionDefinition
            {
                level = level,
                missionId = portraitAsset,
                bossHeroId = heroId,
                bossDisplayName = displayName,
                opponentCount = opponentCount,
                bonusTavernTier = bonusTier,
                bonusGoldPerTurn = bonusGold,
                bonusUpgradeChancePercent = upgradeChancePercent,
                unlockHeroId = heroId
            };
        }

        private static CampaignMissionDefinition Mission(
            int level,
            string portraitAsset,
            string displayName,
            int opponentCount,
            string allyPortraitAsset,
            string allyDisplayName,
            int bonusTier,
            int bonusGold,
            int upgradeChancePercent)
        {
            var mission = Mission(level, portraitAsset, displayName, opponentCount, bonusTier, bonusGold, upgradeChancePercent);
            mission.allyBossHeroId = HeroRegistry.BuildPortraitHeroId(allyPortraitAsset);
            mission.allyBossDisplayName = allyDisplayName;
            return mission;
        }
    }
}