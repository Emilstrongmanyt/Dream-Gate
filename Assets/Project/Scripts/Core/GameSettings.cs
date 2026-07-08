using UnityEngine;

namespace DreamGate.Battlegrounds.Core
{
    public static class GameSettings
    {
        private const string MusicVolumeKey = "dreamgate_music_volume";
        private const string SfxVolumeKey = "dreamgate_sfx_volume";
        private const string HapticsKey = "dreamgate_haptics_enabled";

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f);
            set
            {
                PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxVolumeKey, 0.8f);
            set
            {
                PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        public static bool HapticsEnabled
        {
            get => PlayerPrefs.GetInt(HapticsKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(HapticsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static void ApplyAudio()
        {
            AudioListener.volume = 1f;
            GameSfxPlayer.EnsureInitialized();
            GameMusicPlayer.ApplyVolume();
        }
    }
}