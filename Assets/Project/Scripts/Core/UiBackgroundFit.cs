using UnityEngine;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.Core
{
    /// <summary>
    /// Fits portrait background sprites to the safe-area root on iOS without stretching.
    /// </summary>
    public static class UiBackgroundFit
    {
        public static Image CreateCoverBackground(Transform parent, Sprite sprite, string name = "Background")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
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

            image.preserveAspect = true;
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