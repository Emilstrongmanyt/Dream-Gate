using DreamGate.Battlegrounds.Players;
using UnityEngine;

namespace DreamGate.Battlegrounds.Core
{
    public static class GameSfxPlayer
    {
        private const string SfxRoot = "Sound/Sound FX/";

        public static bool SuppressFeedback { get; set; }

        private static AudioSource sfxSource;

        public static void EnsureInitialized()
        {
            if (sfxSource != null)
            {
                return;
            }

            var go = new GameObject("GameSfxPlayer");
            Object.DontDestroyOnLoad(go);
            sfxSource = go.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }

        public static void Play(string clipName)
        {
            if (SuppressFeedback)
            {
                return;
            }

            EnsureInitialized();
            var clip = Resources.Load<AudioClip>(SfxRoot + clipName);
            if (clip == null || sfxSource == null)
            {
                return;
            }

            sfxSource.PlayOneShot(clip, GameSettings.SfxVolume);
        }

        /// <summary>Recruit-phase economy SFX — only the human player's actions.</summary>
        public static void PlayRecruit(PlayerState player, System.Action playClip)
        {
            if (SuppressFeedback)
            {
                return;
            }

            if (player != null && player.isHuman)
            {
                playClip();
            }
        }

        public static void PlayBuyCard() => Play("BuyCard");
        public static void PlaySellCard() => Play("SellCard");
        public static void PlayDropCard() => Play("DropCard");
        public static void PlayTierUp() => Play("TierUp");
        public static void PlayHit() => Play("Hit");
        public static void PlayVictory() => Play("Victory");
        public static void PlayFailed() => Play("Failed");
    }
}