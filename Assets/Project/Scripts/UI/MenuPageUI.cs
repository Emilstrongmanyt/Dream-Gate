using System;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.UI
{
    public static class MenuPageUI
    {
        private static readonly Color PanelColor = new(0.04f, 0.06f, 0.12f, 0.98f);
        private static readonly Color ButtonColor = new(0.15f, 0.2f, 0.35f, 0.95f);

        public static GameObject CreateOverlay(Transform parent, string name)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = PanelColor;
            return panel;
        }

        public static TextMeshProUGUI CreateTitle(Transform parent, string text, float y = 760f)
        {
            var label = CreateText(parent, "Title", text, y, 42, TextAlignmentOptions.Center);
            label.rectTransform.sizeDelta = new Vector2(900, 80);
            return label;
        }

        public static TextMeshProUGUI CreateBody(Transform parent, string name, string text, float y, float height = 520f)
        {
            var label = CreateText(parent, name, text, y, 24, TextAlignmentOptions.TopLeft);
            label.rectTransform.sizeDelta = new Vector2(860, height);
            return label;
        }

        public static Button CreateBackButton(Transform parent, Action onBack, float y = -760f)
        {
            return CreateActionButton(parent, "Back", new Vector2(0, y), () => onBack?.Invoke());
        }

        public static TextMeshProUGUI CreateStatusText(Transform parent, float y)
        {
            var label = CreateText(parent, "StatusText", string.Empty, y, 20, TextAlignmentOptions.Center);
            label.rectTransform.sizeDelta = new Vector2(860, 80);
            label.color = new Color(0.85f, 0.9f, 1f, 1f);
            return label;
        }

        public static TMP_InputField CreateInputField(Transform parent, string name, string placeholder, float y, bool isPassword = false)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image));
            row.transform.SetParent(parent, false);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchoredPosition = new Vector2(0, y);
            rowRect.sizeDelta = new Vector2(860, 72);
            row.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.2f, 1f);

            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(row.transform, false);
            var textAreaRect = textArea.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(16, 8);
            textAreaRect.offsetMax = new Vector2(-16, -8);

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            placeholderGo.transform.SetParent(textArea.transform, false);
            var placeholderRect = placeholderGo.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            var placeholderText = placeholderGo.GetComponent<TextMeshProUGUI>();
            placeholderText.text = placeholder;
            placeholderText.fontSize = 22;
            placeholderText.color = new Color(1f, 1f, 1f, 0.45f);
            placeholderText.alignment = TextAlignmentOptions.Left;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(textArea.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.fontSize = 22;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;

            var input = row.AddComponent<TMP_InputField>();
            input.textViewport = textAreaRect;
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;

            return input;
        }

        public static Slider CreateSliderRow(
            Transform parent,
            string label,
            float y,
            float value,
            Action<float> onChanged)
        {
            CreateText(parent, $"{label}Label", label, y + 28f, 22, TextAlignmentOptions.Left)
                .rectTransform.sizeDelta = new Vector2(860, 36);

            var row = new GameObject($"{label}Slider", typeof(RectTransform), typeof(Slider));
            row.transform.SetParent(parent, false);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchoredPosition = new Vector2(0, y);
            rowRect.sizeDelta = new Vector2(860, 36);

            var slider = row.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(row.transform, false);
            var bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.2f, 1f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(row.transform, false);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(8, 8);
            fillAreaRect.offsetMax = new Vector2(-8, -8);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.28f, 0.45f, 0.78f, 1f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(row.transform, false);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(8, 0);
            handleAreaRect.offsetMax = new Vector2(-8, 0);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(24, 24);
            handle.GetComponent<Image>().color = Color.white;

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            return slider;
        }

        public static Toggle CreateToggleRow(Transform parent, string label, float y, bool value, Action<bool> onChanged)
        {
            var row = new GameObject($"{label}Toggle", typeof(RectTransform), typeof(Toggle));
            row.transform.SetParent(parent, false);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchoredPosition = new Vector2(0, y);
            rowRect.sizeDelta = new Vector2(860, 48);

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(row.transform, false);
            var bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(0, 0.5f);
            bgRect.anchoredPosition = new Vector2(24, 0);
            bgRect.sizeDelta = new Vector2(36, 36);
            background.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.2f, 1f);

            var checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmark.transform.SetParent(background.transform, false);
            var checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = new Vector2(6, 6);
            checkRect.offsetMax = new Vector2(-6, -6);
            checkmark.GetComponent<Image>().color = new Color(0.35f, 0.75f, 1f, 1f);

            var toggle = row.GetComponent<Toggle>();
            toggle.targetGraphic = background.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(v => onChanged?.Invoke(v));

            var labelText = CreateText(row.transform, "Label", label, 0, 22, TextAlignmentOptions.Left);
            var labelRect = labelText.rectTransform;
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.offsetMin = new Vector2(72, 0);
            labelRect.offsetMax = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;
            return toggle;
        }

        public static SettingsPageView BuildSettingsPage(Transform parent, Action onBack, Action onLogout = null)
        {
            return SettingsPageView.Create(parent, onBack, onLogout);
        }

        public static SupportPageView BuildSupportPage(Transform parent, Action onBack)
        {
            return SupportPageView.Create(parent, onBack);
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string text,
            float y,
            int fontSize,
            TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = new Vector2(900, 120);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.alignment = align;
            label.color = Color.white;
            label.text = text;
            return label;
        }

        public static Button CreateActionButton(Transform parent, string label, Vector2 pos, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(320, 72);
            go.GetComponent<Image>().color = ButtonColor;

            var button = go.GetComponent<Button>();
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

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

    public sealed class SettingsPageView
    {
        private readonly GameObject root;
        private readonly GameObject logoutButton;

        private SettingsPageView(GameObject root, GameObject logoutButton)
        {
            this.root = root;
            this.logoutButton = logoutButton;
        }

        public static SettingsPageView Create(Transform parent, Action onBack, Action onLogout = null)
        {
            var root = MenuPageUI.CreateOverlay(parent, "SettingsPage");
            MenuPageUI.CreateTitle(root.transform, "Settings");
            MenuPageUI.CreateBody(
                root.transform,
                "SettingsDescription",
                "Adjust audio and feedback preferences. Changes are saved automatically.",
                620f,
                80f);

            MenuPageUI.CreateSliderRow(root.transform, "Music Volume", 420f, GameSettings.MusicVolume, v =>
            {
                GameSettings.MusicVolume = v;
                GameSettings.ApplyAudio();
            });
            MenuPageUI.CreateSliderRow(root.transform, "SFX Volume", 300f, GameSettings.SfxVolume, v =>
            {
                GameSettings.SfxVolume = v;
                GameSettings.ApplyAudio();
            });
            MenuPageUI.CreateToggleRow(root.transform, "Haptic Feedback", 180f, GameSettings.HapticsEnabled, v =>
            {
                GameSettings.HapticsEnabled = v;
            });

            GameObject logoutButton = null;
            if (onLogout != null)
            {
                logoutButton = MenuPageUI.CreateActionButton(root.transform, "Log Out", new Vector2(0, -40), onLogout).gameObject;
            }

            MenuPageUI.CreateBackButton(root.transform, () => onBack?.Invoke());
            root.SetActive(false);
            return new SettingsPageView(root, logoutButton);
        }

        public void Show()
        {
            GameSettings.ApplyAudio();
            if (logoutButton != null)
            {
                logoutButton.SetActive(DreamGateServices.IsLoggedIn);
            }

            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide() => root.SetActive(false);
    }

    public sealed class SupportPageView
    {
        private readonly GameObject root;

        private SupportPageView(GameObject root)
        {
            this.root = root;
        }

        public static SupportPageView Create(Transform parent, Action onBack)
        {
            var root = MenuPageUI.CreateOverlay(parent, "SupportPage");
            MenuPageUI.CreateTitle(root.transform, "Support");
            MenuPageUI.CreateBody(
                root.transform,
                "SupportBody",
                "Need help with Dream Gate?\n\n" +
                "• Email: emilstrongmanyt@gmail.com\n" +
                "• Include your device model and a short description of the issue\n" +
                "• Screenshots or screen recordings help us fix bugs faster\n\n" +
                "Feedback on balance, shop feel, and UI is welcome while we polish gameplay.\n\n" +
                $"App version: {Application.version}",
                360f,
                620f);

            MenuPageUI.CreateBackButton(root.transform, () => onBack?.Invoke());
            root.SetActive(false);
            return new SupportPageView(root);
        }

        public void Show()
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide() => root.SetActive(false);
    }
}