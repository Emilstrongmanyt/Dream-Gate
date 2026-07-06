using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreamGate.Battlegrounds.Core
{
    public static class SceneNavigator
    {
        public static void LoadHome() => SceneManager.LoadScene(SceneData.Home);
        public static void LoadMainMenu() => SceneManager.LoadScene(SceneData.MainMenu);
        public static void LoadRatedLobby() => SceneManager.LoadScene(SceneData.RatedLobby);
        public static void LoadPracticeGame() => SceneManager.LoadScene(SceneData.PracticeGame);
    }
}