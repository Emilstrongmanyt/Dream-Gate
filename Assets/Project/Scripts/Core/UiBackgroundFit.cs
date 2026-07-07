using UnityEngine;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.Core
{
    /// <summary>
    /// Fits portrait background sprites edge-to-edge on the canvas (outside safe-area insets).
    /// </summary>
    public static class UiBackgroundFit
    {
        public static Image CreateCanvasCoverBackground(Canvas canvas, Sprite sprite, string name = "Background")
        {
            if (canvas == null)
            {
                return null;
            }

            return CreateCoverBackground(canvas.transform, sprite, name);
        }

        public static Image CreateCoverBackground(Transform parent, Sprite sprite, string name = "Background")
        {
            if (parent == null || sprite == null)
            {
                return null;
            }

            var backdrop = new GameObject($"{name}Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.SetAsFirstSibling();

            var backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            var backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0.02f, 0.04f, 0.08f, 1f);
            backdropImage.raycastTarget = false;

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(backdrop.transform.GetSiblingIndex() + 1);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = false;
            image.raycastTarget = false;
            image.color = Color.white;

            var fitter = go.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = GetSpriteAspect(sprite);

            return image;
        }

        public static void ApplyToExisting(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.preserveAspect = false;
            var fitter = image.GetComponent<AspectRatioFitter>();
            if (fitter == null)
            {
                fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            }

            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = GetSpriteAspect(image.sprite);

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void FitNamedBackground(string objectName = "Background")
        {
            var go = GameObject.Find(objectName);
            if (go == null)
            {
                return;
            }

            var image = go.GetComponent<Image>();
            if (image != null && image.sprite != null)
            {
                ApplyToExisting(image);
            }
        }

        private static float GetSpriteAspect(Sprite sprite)
        {
            if (sprite == null || sprite.rect.height <= 0f)
            {
                return UiCanvasSetup.ReferenceResolution.x / UiCanvasSetup.ReferenceResolution.y;
            }

            return sprite.rect.width / sprite.rect.height;
        }
    }
}