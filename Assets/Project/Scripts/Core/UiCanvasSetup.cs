using UnityEngine;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.Core
{
    public static class UiCanvasSetup
    {
        public static readonly Vector2 ReferenceResolution = new(1080f, 1920f);
        public const float MatchWidthOrHeight = 0.5f;

        public static void ApplyToScene()
        {
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            Apply(canvas);
        }

        public static void Apply(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            UiBackgroundFit.FitNamedBackground();
        }

        public static void ApplySafeArea(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            var anchorMin = new Vector2(safeArea.x / Screen.width, safeArea.y / Screen.height);
            var anchorMax = new Vector2(
                (safeArea.x + safeArea.width) / Screen.width,
                (safeArea.y + safeArea.height) / Screen.height);

            root.anchorMin = anchorMin;
            root.anchorMax = anchorMax;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
        }
    }
}