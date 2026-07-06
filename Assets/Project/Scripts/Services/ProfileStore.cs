using UnityEngine;

namespace DreamGate.Battlegrounds.Services
{
    public static class ProfileStore
    {
        private const string LegacyProfileKey = "dreamgate.player.profile";

        public static PlayerProfile Load(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return CreateDefaultProfile();
            }

            var key = GetProfileKey(playerId);
            if (!PlayerPrefs.HasKey(key))
            {
                return CreateNewProfile(playerId);
            }

            var json = PlayerPrefs.GetString(key);
            if (string.IsNullOrEmpty(json))
            {
                return CreateNewProfile(playerId);
            }

            var profile = JsonUtility.FromJson<PlayerProfile>(json);
            return profile ?? CreateNewProfile(playerId);
        }

        public static void Save(PlayerProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.playerId))
            {
                return;
            }

            profile.highestMmr = Mathf.Max(profile.highestMmr, profile.mmr);
            PlayerPrefs.SetString(GetProfileKey(profile.playerId), JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }

        public static PlayerProfile CreateNewProfile(string playerId, string displayName = "Dreamer", string email = null)
        {
            return new PlayerProfile
            {
                playerId = playerId,
                email = email,
                displayName = displayName,
                mmr = PlayerProfile.DefaultMmr,
                highestMmr = PlayerProfile.DefaultMmr
            };
        }

        private static PlayerProfile CreateDefaultProfile()
        {
            return CreateNewProfile(System.Guid.NewGuid().ToString("N"));
        }

        private static string GetProfileKey(string playerId) => $"dreamgate.profile.{playerId}";

        public static void MigrateLegacyProfileIfNeeded(string playerId)
        {
            if (!PlayerPrefs.HasKey(LegacyProfileKey) || PlayerPrefs.HasKey(GetProfileKey(playerId)))
            {
                return;
            }

            var json = PlayerPrefs.GetString(LegacyProfileKey);
            var profile = JsonUtility.FromJson<PlayerProfile>(json);
            if (profile == null)
            {
                return;
            }

            profile.playerId = playerId;
            Save(profile);
            PlayerPrefs.DeleteKey(LegacyProfileKey);
            PlayerPrefs.Save();
        }
    }
}