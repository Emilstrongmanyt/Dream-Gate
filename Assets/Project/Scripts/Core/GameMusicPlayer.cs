using UnityEngine;

namespace DreamGate.Battlegrounds.Core
{
    public enum MusicContext
    {
        Menu,
        PracticeMatch,
        RankedMatch,
        ThreeRemaining,
        TwoRemaining,
        Victory
    }

    /// <summary>
    /// Context-aware BGM director for menu and match flows.
    /// </summary>
    public class GameMusicPlayer : MonoBehaviour
    {
        private const string BgmRoot = "Sound/BGM/";

        private static GameMusicPlayer instance;
        private AudioSource musicSource;
        private MusicContext currentContext;
        private int lastAliveTier = -1;
        private int menuTrackIndex;
        private int threeRemainingTrackIndex;
        private int twoRemainingTrackIndex;

        public static void PlayMenuMusic()
        {
            EnsureInstance();
            instance.SwitchContext(MusicContext.Menu);
        }

        public static void StartMatchMusic(MatchMode mode)
        {
            EnsureInstance();
            instance.lastAliveTier = -1;
            instance.SwitchContext(mode == MatchMode.Rated ? MusicContext.RankedMatch : MusicContext.PracticeMatch);
        }

        public static void UpdateMatchMusic(int alivePlayers, bool playerWon = false)
        {
            EnsureInstance();
            if (playerWon)
            {
                instance.SwitchContext(MusicContext.Victory);
                return;
            }

            var tier = alivePlayers <= 2 ? 2 : alivePlayers <= 3 ? 3 : 0;
            if (tier == instance.lastAliveTier)
            {
                return;
            }

            instance.lastAliveTier = tier;
            if (tier == 2)
            {
                instance.SwitchContext(MusicContext.TwoRemaining);
            }
            else if (tier == 3)
            {
                instance.SwitchContext(MusicContext.ThreeRemaining);
            }
        }

        public static void EnsurePlaying()
        {
            EnsureInstance();
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

        private static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            var go = new GameObject("GameMusicPlayer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameMusicPlayer>();
            instance.Initialize();
        }

        private void Initialize()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            menuTrackIndex = PlayerPrefs.GetInt("dreamgate_menu_bgm_index", 0);
            threeRemainingTrackIndex = PlayerPrefs.GetInt("dreamgate_three_bgm_index", 0);
            twoRemainingTrackIndex = PlayerPrefs.GetInt("dreamgate_two_bgm_index", 0);
            ApplyVolume();
        }

        private void SwitchContext(MusicContext context)
        {
            currentContext = context;
            var clipName = ResolveClipName(context);
            var clip = Resources.Load<AudioClip>(BgmRoot + clipName);
            if (clip == null)
            {
                clip = Resources.Load<AudioClip>("GameBGM");
            }

            if (clip == null || musicSource == null)
            {
                return;
            }

            if (musicSource.clip != clip)
            {
                musicSource.clip = clip;
                musicSource.Play();
            }
            else if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }

            ApplyVolume();
        }

        private string ResolveClipName(MusicContext context)
        {
            switch (context)
            {
                case MusicContext.Menu:
                    var menuClip = menuTrackIndex == 0 ? "MenuBGM" : "MenuBGM.2";
                    menuTrackIndex = 1 - menuTrackIndex;
                    PlayerPrefs.SetInt("dreamgate_menu_bgm_index", menuTrackIndex);
                    PlayerPrefs.Save();
                    return menuClip;

                case MusicContext.PracticeMatch:
                    return "PracticeBGM";

                case MusicContext.RankedMatch:
                    return "RankedBGM";

                case MusicContext.ThreeRemaining:
                    var threeClip = threeRemainingTrackIndex == 0 ? "3RemainingBGM" : "3RemainingBGM.2";
                    threeRemainingTrackIndex = 1 - threeRemainingTrackIndex;
                    PlayerPrefs.SetInt("dreamgate_three_bgm_index", threeRemainingTrackIndex);
                    PlayerPrefs.Save();
                    return threeClip;

                case MusicContext.TwoRemaining:
                    var twoClip = twoRemainingTrackIndex == 0 ? "2RemainingBGM" : "2RemainingBGM.2";
                    twoRemainingTrackIndex = 1 - twoRemainingTrackIndex;
                    PlayerPrefs.SetInt("dreamgate_two_bgm_index", twoRemainingTrackIndex);
                    PlayerPrefs.Save();
                    return twoClip;

                case MusicContext.Victory:
                    return "WinBGM";

                default:
                    return "PracticeBGM";
            }
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