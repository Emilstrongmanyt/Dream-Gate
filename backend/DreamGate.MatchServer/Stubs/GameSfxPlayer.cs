using DreamGate.Battlegrounds.Players;

namespace DreamGate.Battlegrounds.Core
{
    public static class GameSfxPlayer
    {
        public static bool SuppressFeedback { get; set; }
        public static void EnsureInitialized() { }
        public static void Play(string clipName) { }
        public static void PlayRecruit(PlayerState player, Action playClip) { }
        public static void PlayBuyCard() { }
        public static void PlaySellCard() { }
        public static void PlayDropCard() { }
        public static void PlayTierUp() { }
        public static void PlayHit() { }
        public static void PlayVictory() { }
        public static void PlayFailed() { }
    }
}