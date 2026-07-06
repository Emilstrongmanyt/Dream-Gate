using DreamGate.Battlegrounds.Core;
using UnityEngine;

public class HomePlayButton : MonoBehaviour
{
    public void gotoGame()
    {
        SceneNavigator.LoadMainMenu();
    }

    public void gotoGame1()
    {
        SceneNavigator.LoadPracticeGame();
    }
}
