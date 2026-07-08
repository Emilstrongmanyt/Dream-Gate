using System;
using DreamGate.Battlegrounds.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.UI
{
    public class RatedLobbyUI : MonoBehaviour
    {
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI profileText;
        private TextMeshProUGUI queueText;
        private Button findMatchButton;
        private Button cancelButton;
        private Button backButton;

        public event Action FindMatchClicked;
        public event Action CancelClicked;
        public event Action BackClicked;

        public void Build(Transform root)
        {
            var panel = CreatePanel(root, "RatedLobbyPanel", new Color(0.04f, 0.06f, 0.12f, 0.98f));
            profileText = CreateText(panel.transform, "ProfileText", new Vector2(0, 760), 28, TextAlignmentOptions.Center);
            profileText.rectTransform.sizeDelta = new Vector2(900, 80);

            statusText = CreateText(panel.transform, "StatusText", new Vector2(0, 640), 24, TextAlignmentOptions.Center);
            statusText.rectTransform.sizeDelta = new Vector2(900, 60);

            queueText = CreateText(panel.transform, "QueueText", new Vector2(0, 500), 32, TextAlignmentOptions.Center);
            queueText.rectTransform.sizeDelta = new Vector2(900, 120);
            queueText.text = "Ready to queue";

            findMatchButton = CreateActionButton(panel.transform, "Find Match", new Vector2(0, 280), () => FindMatchClicked?.Invoke());
            cancelButton = CreateActionButton(panel.transform, "Cancel", new Vector2(0, 180), () => CancelClicked?.Invoke());
            backButton = CreateActionButton(panel.transform, "Back", new Vector2(0, 80), () => BackClicked?.Invoke());

            SetSearching(false);
            RefreshProfile();
        }

        public void RefreshProfile()
        {
            if (!DreamGateServices.IsInitialized || !DreamGateServices.IsLoggedIn || DreamGateServices.Profile == null)
            {
                profileText.text = "Sign in from the home screen to track rated MMR.";
                statusText.text = "Not signed in";
                return;
            }

            var profile = DreamGateServices.Profile;
            profileText.text =
                $"{profile.displayName}\n" +
                $"MMR {profile.mmr} | W {profile.wins} / L {profile.losses} | Streak {profile.currentWinStreak} | Top 4 {profile.top4Finishes}";
            statusText.text = DreamGateServices.GetStatusLine();
        }

        public void SetSearching(bool searching)
        {
            findMatchButton.interactable = !searching;
            cancelButton.interactable = searching;
        }

        public void SetQueueStatus(string message)
        {
            queueText.text = message;
        }

        public void SetStatus(string message)
        {
            statusText.text = message;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 pos, int fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(900, 120);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(14, fontSize - 10);
            text.fontSizeMax = fontSize;
            text.alignment = align;
            text.color = Color.white;
            return text;
        }

        private static Button CreateActionButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(320, 72);
            go.GetComponent<Image>().color = new Color(0.15f, 0.2f, 0.35f, 0.95f);

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.fontSize = 22;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.text = label;
            return button;
        }
    }
}