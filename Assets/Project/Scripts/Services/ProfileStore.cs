using UnityEngine;

namespace DreamGate.Battlegrounds.Services
{
    public static class ProfileStore
    {
        private const string ProfileKey = "dreamgate.player.profile";

        public static PlayerProfile Load()
        {
            if (!PlayerPrefs.HasKey(ProfileKey))
            {
                return CreateDefaultProfile();
            }

            var json = PlayerPrefs.GetString(ProfileKey);
            if (string.IsNullOrEmpty(json))
            {
                return CreateDefaultProfile();
            }

            var profile = JsonUtility.FromJson<PlayerProfile>(json);
            return profile ?? CreateDefaultProfile();
        }

        public static void Save(PlayerProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            profile.highestMmr = Mathf.Max(profile.highestMmr, profile.mmr);
            PlayerPrefs.SetString(ProfileKey, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }

        private static PlayerProfile CreateDefaultProfile()
        {
            return new PlayerProfile
            {
                playerId = System.Guid.NewGuid().ToString("N"),
                displayName = "Dreamer",
                mmr = PlayerProfile.DefaultMmr,
                highestMmr = PlayerProfile.DefaultMmr
            };
        }
    }
}