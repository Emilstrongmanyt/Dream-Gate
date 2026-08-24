using UnityEngine;
using UnityEngine.UI;
using Kindling.Sim.Catalog;
using Kindling.Sim.Model;

namespace Kindling.Client
{
    public static class HsUi
    {
        public static readonly Color Felt = Hex("1a120c");
        public static readonly Color Wood = Hex("3a2414");
        public static readonly Color Gold = Hex("d4a017");
        public static readonly Color GoldDark = Hex("7a5a10");
        public static readonly Color Cream = Hex("f3e6c4");
        public static readonly Color Ember = Hex("e25822");
        public static readonly Color WickRed = Hex("c0392b");
        public static readonly Color AtkGem = Hex("c9a227");
        public static readonly Color HpGem = Hex("a33b2b");
        public static readonly Color Selected = Hex("ffe27a");

        public static Font Font;

        public static Color ChorusColor(Chorus c)
        {
            switch (c)
            {
                case Chorus.Cinderkin: return Hex("c45c12");
                case Chorus.Gearwights: return Hex("4a6d8c");
                case Chorus.Ashbound: return Hex("6b3a8c");
                case Chorus.Gutterlings: return Hex("3d7a3a");
                default: return Hex("6e5b3a");
            }
        }

        public static Sprite Pixel(Color color)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = color;
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString("#" + h, out Color c);
            return c;
        }

        public static Font GetFont()
        {
            if (Font != null) return Font;
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (Font == null) Font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return Font;
        }

        public static GameObject Canvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = Felt;
            return go;
        }

        public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            go.GetComponent<Image>().sprite = Pixel(color);
            return rt;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = GetFont();
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button MakeButton(Transform parent, string name, string caption, Vector2 anchorMin, Vector2 anchorMax, Color bg, System.Action onClick)
        {
            var rt = Panel(parent, name, anchorMin, anchorMax, bg);
            var img = rt.GetComponent<Image>();
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = Color.Lerp(bg, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(bg, Color.black, 0.2f);
            btn.colors = colors;
            Label(rt, "cap", caption, 22, TextAnchor.MiddleCenter, Cream);
            btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static string Keywords(Keyword k, bool awakened)
        {
            var s = "";
            if (awakened) s += "Awakened ";
            if ((k & Keyword.Ward) != 0) s += "Ward ";
            if ((k & Keyword.Aegis) != 0) s += "Aegis ";
            if ((k & Keyword.Afterglow) != 0) s += "Afterglow ";
            if ((k & Keyword.Venom) != 0) s += "Venom ";
            if ((k & Keyword.Latch) != 0) s += "Latch ";
            return s.Trim();
        }
    }
}
