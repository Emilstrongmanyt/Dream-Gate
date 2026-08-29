using UnityEngine;
using UnityEngine.UI;

namespace Kindling.Client
{
    public static class BannerFx
    {
        static Image Img;
        static float Until;

        public static void Build(Transform parent)
        {
            var rt = HsUi.Panel(parent, "banner", new Vector2(0.22f, 0.38f), new Vector2(0.78f, 0.62f), Color.clear);
            var img = rt.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = Color.clear;
            Img = img;
            rt.gameObject.SetActive(false);
        }

        public static void Show(string spriteName, float seconds = 1.35f)
        {
            if (Img == null) return;
            Sprite s = StoneTheme.Get(spriteName);
            if (s == null) return;
            Img.sprite = s;
            Img.color = Color.white;
            Img.type = Image.Type.Simple;
            Img.preserveAspect = true;
            Img.gameObject.SetActive(true);
            Img.transform.SetAsLastSibling();
            Until = Time.unscaledTime + seconds;
        }

        public static void Tick()
        {
            if (Img == null || !Img.gameObject.activeSelf) return;
            float left = Until - Time.unscaledTime;
            if (left <= 0f)
            {
                Img.gameObject.SetActive(false);
                return;
            }
            float a = left < 0.35f ? left / 0.35f : 1f;
            var c = Img.color;
            c.a = a;
            Img.color = c;
        }
    }
}
