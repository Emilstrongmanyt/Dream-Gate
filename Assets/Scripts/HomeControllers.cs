using System.Collections;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Services;
using DreamGate.Battlegrounds.Services.Backend;
using DreamGate.Battlegrounds.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HomeMenuController : MonoBehaviour
{
    private Transform pageRoot;
    private TextMeshProUGUI accountStatusText;
    private SettingsPageView settingsPage;
    private SupportPageView supportPage;
    private LoginPageView loginPage;
    private CreateAccountPageView createAccountPage;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private TextMeshProUGUI authSmokeTestStatus;
#endif

    private void Start()
    {
        GameSettings.ApplyAudio();
        GameMusicPlayer.PlayMenuMusic();
        UiCanvasSetup.ApplyToScene();
        DreamGateServices.Initialize();
        pageRoot = EnsureUiRoot();
        accountStatusText = CreateAccountStatusBanner(pageRoot);

        settingsPage = MenuPageUI.BuildSettingsPage(pageRoot, CloseOverlays, OnLogout);
        supportPage = MenuPageUI.BuildSupportPage(pageRoot, CloseOverlays);
        loginPage = LoginPageView.Create(pageRoot, CloseOverlays, OnAuthSuccess, ShowCreateAccount);
        createAccountPage = CreateAccountPageView.Create(pageRoot, CloseOverlays, OnAuthSuccess, ShowLogin);

        BindButton("Settings", () =>
        {
            CloseOverlays();
            settingsPage.Show();
        });
        BindButton("Support", () =>
        {
            CloseOverlays();
            supportPage.Show();
        });
        BindButton("Login", ShowLogin);
        BindButton("CreateAccount", ShowCreateAccount);
        RefreshAccountStatus();

        if (DreamGateServices.PendingRatedLobbyAfterLogin)
        {
            ShowLogin();
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        CreateAuthSmokeTestUi(pageRoot);
#endif
    }

    public void GoToMainMenu()
    {
        SceneNavigator.LoadMainMenu();
    }

    private void OnAuthSuccess()
    {
        RefreshAccountStatus();
        var goToRatedLobby = DreamGateServices.PendingRatedLobbyAfterLogin;
        DreamGateServices.PendingRatedLobbyAfterLogin = false;
        CloseOverlaysWithoutClearingRatedIntent();

        if (!DreamGateServices.IsLoggedIn)
        {
            return;
        }

        if (goToRatedLobby)
        {
            SceneNavigator.LoadRatedLobby();
        }
    }

    private void ShowLogin()
    {
        CloseOverlays();
        createAccountPage.Hide();
        loginPage.Show();
    }

    private void ShowCreateAccount()
    {
        CloseOverlays();
        loginPage.Hide();
        createAccountPage.Show();
    }

    private void OnLogout()
    {
        DreamGateServices.Logout();
        CloseOverlays();
        RefreshAccountStatus();
    }

    private void RefreshAccountStatus()
    {
        if (accountStatusText != null)
        {
            accountStatusText.text = DreamGateServices.GetHomeStatusLine();
        }
    }

    private TextMeshProUGUI CreateAccountStatusBanner(Transform parent)
    {
        var go = new GameObject("AccountStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -12);
        rect.sizeDelta = new Vector2(980, 72);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = 22;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16;
        text.fontSizeMax = 24;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.85f, 0.92f, 1f, 1f);
        return text;
    }

    private Transform EnsureUiRoot()
    {
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            return transform;
        }

        var uiRoot = new GameObject("HomeMenuUI", typeof(RectTransform));
        uiRoot.transform.SetParent(canvas.transform, false);
        var rect = uiRoot.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        UiCanvasSetup.ApplySafeArea(rect);
        transform.SetParent(uiRoot.transform, false);
        return uiRoot.transform;
    }

    private void BindButton(string objectName, UnityEngine.Events.UnityAction onClick)
    {
        var buttonObject = GameObject.Find(objectName);
        if (buttonObject == null)
        {
            Debug.LogWarning($"Home menu button not found: {objectName}");
            return;
        }

        var button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
    }

    private void CloseOverlays()
    {
        CloseOverlaysWithoutClearingRatedIntent();
        DreamGateServices.PendingRatedLobbyAfterLogin = false;
    }

    private void CloseOverlaysWithoutClearingRatedIntent()
    {
        settingsPage?.Hide();
        supportPage?.Hide();
        loginPage?.Hide();
        createAccountPage?.Hide();
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private void CreateAuthSmokeTestUi(Transform parent)
    {
        var panel = new GameObject("AuthSmokeTest", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(18f, 18f);
        rect.sizeDelta = new Vector2(360f, 180f);

        var buttonGo = new GameObject("RunSmokeTest", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(panel.transform, false);
        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 1f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(0f, 44f);
        buttonGo.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.3f, 0.92f);

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(buttonGo.transform, false);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = "Auth Smoke Test";
        label.fontSize = 18;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.85f, 0.92f, 1f, 1f);

        var statusGo = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusGo.transform.SetParent(panel.transform, false);
        var statusRect = statusGo.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.offsetMin = new Vector2(0f, 0f);
        statusRect.offsetMax = new Vector2(0f, -52f);
        authSmokeTestStatus = statusGo.GetComponent<TextMeshProUGUI>();
        authSmokeTestStatus.fontSize = 14;
        authSmokeTestStatus.alignment = TextAlignmentOptions.BottomLeft;
        authSmokeTestStatus.color = new Color(0.75f, 0.82f, 0.95f, 1f);

        buttonGo.GetComponent<Button>().onClick.AddListener(RunAuthSmokeTest);
    }

    private void RunAuthSmokeTest()
    {
        if (authSmokeTestStatus != null)
        {
            authSmokeTestStatus.text = "Running auth smoke test...";
        }

        CloudCoroutineHost.Instance.Run(AuthSmokeTestRoutine());
    }

    private IEnumerator AuthSmokeTestRoutine()
    {
        var report = string.Empty;
        yield return DreamGateServices.CoRunAuthSmokeTest(message => report = message);
        if (authSmokeTestStatus != null)
        {
            authSmokeTestStatus.text = report;
        }
    }
#endif
}