using DreamGate.Battlegrounds.Core;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button ratedButton;

    private void Start()
    {
        EnsureRatedButton();
        EnsureBackButton();
    }

    public void StartPracticeGame()
    {
        MatchSessionContext.BeginPractice();
        SceneNavigator.LoadPracticeGame();
    }

    public void StartRatedLobby()
    {
        SceneNavigator.LoadRatedLobby();
    }

    public void BackToHome()
    {
        SceneNavigator.LoadHome();
    }

    private void EnsureRatedButton()
    {
        if (ratedButton != null)
        {
            ratedButton.onClick.AddListener(StartRatedLobby);
            return;
        }

        var practiceButton = FindPracticeButton();
        if (practiceButton == null)
        {
            return;
        }

        var ratedGo = Instantiate(practiceButton.gameObject, practiceButton.transform.parent);
        ratedGo.name = "RatedButton";
        var rect = ratedGo.GetComponent<RectTransform>();
        var practiceRect = practiceButton.GetComponent<RectTransform>();
        rect.anchoredPosition = practiceRect.anchoredPosition + new Vector2(0f, -240f);

        var image = ratedGo.GetComponent<Image>();
        var ratedSprites = Resources.LoadAll<Sprite>("RatedButton");
        if (ratedSprites != null && ratedSprites.Length > 0)
        {
            image.sprite = ratedSprites[0];
        }

        var label = ratedGo.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (label != null)
        {
            label.text = string.Empty;
        }

        ratedButton = ratedGo.GetComponent<Button>();
        ratedButton.onClick.RemoveAllListeners();
        ratedButton.onClick.AddListener(StartRatedLobby);
    }

    private void EnsureBackButton()
    {
        var practiceButton = FindPracticeButton();
        if (practiceButton == null)
        {
            return;
        }

        var backGo = Instantiate(practiceButton.gameObject, practiceButton.transform.parent);
        backGo.name = "BackButton";
        var rect = backGo.GetComponent<RectTransform>();
        var practiceRect = practiceButton.GetComponent<RectTransform>();
        rect.anchoredPosition = practiceRect.anchoredPosition + new Vector2(-320f, 0f);

        var image = backGo.GetComponent<Image>();
        image.color = new Color(0.15f, 0.2f, 0.35f, 0.95f);

        var label = backGo.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (label != null)
        {
            label.text = "Back";
            label.fontSize = 28;
        }

        var backButton = backGo.GetComponent<Button>();
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(BackToHome);
    }

    private static Button FindPracticeButton()
    {
        var buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (var button in buttons)
        {
            var image = button.GetComponent<Image>();
            if (image == null || image.sprite == null)
            {
                continue;
            }

            if (image.sprite.name.Contains("Practice") || button.gameObject.name == "Button")
            {
                return button;
            }
        }

        return buttons.Length > 0 ? buttons[0] : null;
    }
}