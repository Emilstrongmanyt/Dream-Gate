using UnityEngine;

namespace DreamGate.Battlegrounds.Core
{
    /// <summary>
    /// Loops GameBGM from Resources across gameplay scenes.
    /// </summary>
    public class GameMusicPlayer : MonoBehaviour
    {
        private const string BgmResource = "GameBGM";

        private static GameMusicPlayer instance;
        private AudioSource musicSource;

        public static void EnsurePlaying()
        {
            if (instance == null)
            {
                var go = new GameObject("GameMusicPlayer");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GameMusicPlayer>();
                instance.Initialize();
            }

            instance.PlayIfNeeded();
            ApplyVolume();
        }

        public static void ApplyVolume()
        {
            if (instance == null || instance.musicSource == null)
            {
                return;
            }

            instance.musicSource.volume = GameSettings.MusicVolume;
        }

        private void Initialize()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = Resources.Load<AudioClip>(BgmResource);
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            ApplyVolume();
        }

        private void PlayIfNeeded()
        {
            if (musicSource.clip == null || musicSource.isPlaying)
            {
                return;
            }

            musicSource.Play();
        }
    }
}