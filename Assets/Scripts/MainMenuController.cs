using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Services;
using DreamGate.Battlegrounds.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button ratedButton;

    private void Start()
    {
        if (!DreamGateServices.IsInitialized)
        {
            DreamGateServices.Initialize();
        }

        UiCanvasSetup.ApplyToScene();
        EnsureFallingCards();
        SetupMenuButtons();
    }

    public void StartPracticeGame()
    {
        MatchSessionContext.BeginPractice();
        SceneNavigator.LoadPracticeGame();
    }

    public void StartRatedLobby()
    {
        if (!DreamGateServices.IsLoggedIn)
        {
            DreamGateServices.PendingRatedLobbyAfterLogin = true;
            SceneNavigator.LoadHome();
            return;
        }

        SceneNavigator.LoadRatedLobby();
    }

    public void BackToHome()
    {
        SceneNavigator.LoadHome();
    }

    private void EnsureFallingCards()
    {
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        var spawner = HomeFallingCardsSpawner.Create(canvas.transform);
        var background = canvas.transform.Find("Background");
        var insertIndex = background != null ? background.GetSiblingIndex() + 1 : 0;
        spawner.transform.SetSiblingIndex(insertIndex);
    }

    private void SetupMenuButtons()
    {
        CleanupRuntimeButtons();

        var practiceButton = FindPracticeButton();
        if (practiceButton == null)
        {
            return;
        }

        practiceButton.gameObject.name = "PracticeButton";
        practiceButton.onClick.RemoveAllListeners();
        practiceButton.onClick.AddListener(StartPracticeGame);

        DisableAnimator(practiceButton.gameObject);

        var practiceRect = practiceButton.GetComponent<RectTransform>();
        var parent = practiceButton.transform.parent;

        if (ratedButton == null)
        {
            ratedButton = CreateSpriteButton(
                "RatedButton",
                parent,
                practiceRect.anchoredPosition + new Vector2(0f, -260f),
                practiceRect.sizeDelta,
                LoadSprite("RatedButton"),
                StartRatedLobby);
        }
        else
        {
            ratedButton.onClick.RemoveAllListeners();
            ratedButton.onClick.AddListener(StartRatedLobby);
        }

        CreateBackButton(parent);
    }

    private void CreateBackButton(Transform parent)
    {
        var go = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(32f, -32f);
        rect.sizeDelta = new Vector2(220f, 72f);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.15f, 0.2f, 0.35f, 0.95f);

        var button = go.GetComponent<Button>();
        button.onClick.AddListener(BackToHome);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var label = textGo.GetComponent<TextMeshProUGUI>();
        label.text = "Back";
        label.fontSize = 28;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18;
        label.fontSizeMax = 28;
    }

    private static Button CreateSpriteButton(
        string name,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Sprite sprite,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        var image = go.GetComponent<Image>();
        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.15f, 0.2f, 0.35f, 0.95f);
        }

        var button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);
        return button;
    }

    private static void CleanupRuntimeButtons()
    {
        DestroyIfExists("RatedButton");
        DestroyIfExists("BackButton");
    }

    private static void DestroyIfExists(string objectName)
    {
        var existing = GameObject.Find(objectName);
        if (existing != null)
        {
            Destroy(existing);
        }
    }

    private static void DisableAnimator(GameObject target)
    {
        var animator = target.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = false;
        }
    }

    private static Sprite LoadSprite(string resourceName)
    {
        var sprites = Resources.LoadAll<Sprite>(resourceName);
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }

    private static Button FindPracticeButton()
    {
        var practiceObject = GameObject.Find("PracticeButton");
        if (practiceObject != null)
        {
            return practiceObject.GetComponent<Button>();
        }

        var buttons = FindObjectsByType<Button>();
        foreach (var button in buttons)
        {
            if (button.gameObject.name is "RatedButton" or "BackButton")
            {
                continue;
            }

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