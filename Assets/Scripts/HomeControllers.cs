using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.UI;
using UnityEngine;
using UnityEngine.UI;

public class HomeMenuController : MonoBehaviour
{
    private Transform pageRoot;
    private SettingsPageView settingsPage;
    private SupportPageView supportPage;

    private void Start()
    {
        GameSettings.ApplyAudio();
        pageRoot = EnsureUiRoot();
        settingsPage = MenuPageUI.BuildSettingsPage(pageRoot, CloseOverlays);
        supportPage = MenuPageUI.BuildSupportPage(pageRoot, CloseOverlays);

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
    }

    public void GoToMainMenu()
    {
        SceneNavigator.LoadMainMenu();
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
    }
}