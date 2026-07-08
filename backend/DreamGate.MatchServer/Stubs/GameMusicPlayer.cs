namespace DreamGate.Battlegrounds.Core
{
    public class GameMusicPlayer
    {
        public static bool SuppressFeedback { get; set; }
        public static void PlayMenuMusic() { }
        public static void StartMatchMusic(MatchMode mode) { }
        public static void UpdateMatchMusic(int alivePlayers, bool playerWon = false) { }
        public static void ApplyVolume() { }
    }
}