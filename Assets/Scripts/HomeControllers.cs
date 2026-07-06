using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Services;
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

    private void Start()
    {
        GameSettings.ApplyAudio();
        DreamGateServices.Initialize();
        pageRoot = EnsureUiRoot();
        accountStatusText = CreateAccountStatusBanner(pageRoot);

        settingsPage = MenuPageUI.BuildSettingsPage(pageRoot, CloseOverlays, OnLogout);
        supportPage = MenuPageUI.BuildSupportPage(pageRoot, CloseOverlays);
        loginPage = LoginPageView.Create(pageRoot, CloseOverlays, RefreshAccountStatus, ShowCreateAccount);
        createAccountPage = CreateAccountPageView.Create(pageRoot, CloseOverlays, RefreshAccountStatus, ShowLogin);

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
    }

    public void GoToMainMenu()
    {
        if (!DreamGateServices.IsLoggedIn)
        {
            ShowLogin();
            return;
        }

        SceneNavigator.LoadMainMenu();
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
        rect.anchoredPosition = new Vector2(0, -40);
        rect.sizeDelta = new Vector2(900, 80);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = 22;
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
        settingsPage?.Hide();
        supportPage?.Hide();
        loginPage?.Hide();
        createAccountPage?.Hide();
    }
}