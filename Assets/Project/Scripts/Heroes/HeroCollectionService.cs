using System;
using System.Collections.Generic;
using System.Linq;
using DreamGate.Battlegrounds.Services;

namespace DreamGate.Battlegrounds.Heroes
{
    public static class HeroCollectionService
    {
        public const string DefaultHeroId = "hero_art_Warrior";

        public static string SelectedHeroId
        {
            get => NormalizeHeroId(ReadProfile()?.selectedHeroId);
            set
            {
                var profile = EnsureProfile();
                profile.selectedHeroId = NormalizeHeroId(value);
                SaveProfile(profile);
            }
        }

        public static int CampaignHighestLevel => ReadProfile()?.campaignHighestLevel ?? 0;

        public static IReadOnlyList<string> GetUnlockedHeroIds()
        {
            var profile = ReadProfile();
            if (profile == null)
            {
                return new[] { DefaultHeroId };
            }

            return ParseUnlocked(profile.unlockedHeroIdsCsv);
        }

        public static bool IsHeroUnlocked(string heroId)
        {
            var normalized = NormalizeHeroId(heroId);
            return GetUnlockedHeroIds().Any(id => string.Equals(id, normalized, StringComparison.Ordinal));
        }

        public static void UnlockHero(string heroId)
        {
            var normalized = NormalizeHeroId(heroId);
            var profile = EnsureProfile();
            var unlocked = new HashSet<string>(ParseUnlocked(profile.unlockedHeroIdsCsv), StringComparer.Ordinal);
            if (!unlocked.Add(normalized))
            {
                return;
            }

            profile.unlockedHeroIdsCsv = string.Join(",", unlocked);
            SaveProfile(profile);
        }

        public static void CompleteCampaignMission(int missionLevel, string unlockHeroId)
        {
            var profile = EnsureProfile();
            if (missionLevel > profile.campaignHighestLevel)
            {
                profile.campaignHighestLevel = missionLevel;
            }

            UnlockHero(unlockHeroId);
            SaveProfile(profile);
        }

        public static void EnsureStarterCollection()
        {
            var profile = EnsureProfile();
            var changed = false;
            if (string.IsNullOrWhiteSpace(profile.selectedHeroId))
            {
                profile.selectedHeroId = DefaultHeroId;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(profile.unlockedHeroIdsCsv))
            {
                profile.unlockedHeroIdsCsv = DefaultHeroId;
                changed = true;
            }
            else if (!IsHeroUnlocked(DefaultHeroId))
            {
                UnlockHero(DefaultHeroId);
                changed = true;
            }

            if (changed)
            {
                SaveProfile(profile);
            }
        }

        public static string GetPortraitDisplayName(string heroId)
        {
            var asset = HeroRegistry.GetPortraitAssetName(heroId);
            if (string.IsNullOrEmpty(asset))
            {
                return "Hero";
            }

            return asset.Replace("Hero", string.Empty).Trim();
        }

        private static PlayerProfile EnsureProfile()
        {
            if (DreamGateServices.Profile == null)
            {
                var guestId = PlayerPrefsGuestId.GetOrCreate();
                DreamGateServices.SetGuestProfile(ProfileStore.CreateNewProfile(guestId));
            }

            return DreamGateServices.Profile;
        }

        private static PlayerProfile ReadProfile() => DreamGateServices.Profile;

        private static void SaveProfile(PlayerProfile profile)
        {
            if (DreamGateServices.UseCloudBackend && DreamGateServices.IsLoggedIn)
            {
                DreamGateServices.SaveHeroCollection(profile);
            }
            else if (!string.IsNullOrEmpty(profile.playerId))
            {
                ProfileStore.Save(profile);
            }

            DreamGateServices.NotifyProfileChangedPublic();
        }

        private static string NormalizeHeroId(string heroId) =>
            string.IsNullOrWhiteSpace(heroId) ? DefaultHeroId : heroId.Trim();

        private static IReadOnlyList<string> ParseUnlocked(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return new[] { DefaultHeroId };
            }

            return csv.Split(',')
                .Select(part => part.Trim())
                .Where(part => !string.IsNullOrEmpty(part))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }

    internal static class PlayerPrefsGuestId
    {
        private const string Key = "dreamgate.guest.player_id";

        public static string GetOrCreate()
        {
            var existing = UnityEngine.PlayerPrefs.GetString(Key, string.Empty);
            if (!string.IsNullOrEmpty(existing))
            {
                return existing;
            }

            var created = Guid.NewGuid().ToString("N");
            UnityEngine.PlayerPrefs.SetString(Key, created);
            UnityEngine.PlayerPrefs.Save();
            return created;
        }
    }
}