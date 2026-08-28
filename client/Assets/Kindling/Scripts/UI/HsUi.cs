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
        public static bool ForcePatterns;
        public static Color FrameColor = Gold;
        static Sprite[] CardShirts;

        public static Sprite CardShirt(Chorus chorus, bool captain)
        {
            EnsureCardShirts();
            if (CardShirts == null || CardShirts.Length == 0) return Pixel(Wood);
            int i;
            if (captain) i = CardShirts.Length - 1;
            else
            {
                switch (chorus)
                {
                    case Chorus.Humanoid: i = 0; break;
                    case Chorus.Beast: i = 1; break;
                    case Chorus.Dragon: i = 2; break;
                    case Chorus.Spirit: i = 3; break;
                    case Chorus.Undead: i = 4; break;
                    default: i = 0; break;
                }
            }
            if (i < 0) i = 0;
            if (i >= CardShirts.Length) i = CardShirts.Length - 1;
            return CardShirts[i] != null ? CardShirts[i] : Pixel(Wood);
        }

        static void EnsureCardShirts()
        {
            if (CardShirts != null) return;
            CardShirts = Resources.LoadAll<Sprite>("CardShirts");
            if (CardShirts == null) CardShirts = new Sprite[0];
            System.Array.Sort(CardShirts, (a, b) => string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : ""));
        }

        public static Color CosmeticFrame(string id)
        {
            if (id == "ember") return Ember;
            if (id == "spirit") return ChorusColor(Chorus.Spirit);
            if (id == "wick") return WickRed;
            if (id == "night") return Hex("2a2040");
            return Gold;
        }

        public static Font Font;
        static readonly Sprite[] PatternCache = new Sprite[8];

        public static Color CaptainTint(string id)
        {
            if (string.IsNullOrEmpty(id)) return Ember;
            int h = 0;
            for (int i = 0; i < id.Length; i++) h = (h * 33) ^ id[i];
            if (h < 0) h = -h;
            Color[] tints =
            {
                Ember, GoldDark, ChorusColor(Chorus.Undead), ChorusColor(Chorus.Beast),
                ChorusColor(Chorus.Humanoid), ChorusColor(Chorus.Dragon), ChorusColor(Chorus.Spirit),
                WickRed, Hex("8a5a2b"), Hex("4a6b8c"), Hex("6b4a2a"), Hex("3d5c4a"),
                Hex("7a3a4a"), Hex("5a4a7a"), Hex("7a6a20"), Hex("2a4a5a")
            };
            return tints[h % tints.Length];
        }

        public static Color ChorusColor(Chorus c)
        {
            switch (c)
            {
                case Chorus.Undead: return Hex("6b3a8c");
                case Chorus.Beast: return Hex("3d7a3a");
                case Chorus.Humanoid: return Hex("c45c12");
                case Chorus.Dragon: return Hex("a33b2b");
                case Chorus.Spirit: return Hex("5aa0c8");
                default: return Hex("6e5b3a");
            }
        }

        public static Sprite ChorusPattern(Chorus c)
        {
            int i = (int)c;
            if (i < 0 || i >= PatternCache.Length) return null;
            if (PatternCache[i] != null) return PatternCache[i];
            PatternCache[i] = MakePattern(c);
            return PatternCache[i];
        }

        static Sprite MakePattern(Chorus c)
        {
            const int n = 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color on = new Color(1f, 1f, 1f, 0.42f);
            Color off = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    bool lit = false;
                    switch (c)
                    {
                        case Chorus.Undead:
                            lit = ((x + y) % 4) == 0;
                            break;
                        case Chorus.Beast:
                            lit = (x % 5 == 2) && (y % 5 == 2);
                            break;
                        case Chorus.Humanoid:
                            lit = (x % 4) == 0;
                            break;
                        case Chorus.Dragon:
                            {
                                int dx = x - 7, dy = y - 7;
                                int d = dx * dx + dy * dy;
                                lit = d >= 20 && d <= 36;
                                break;
                            }
                        case Chorus.Spirit:
                            lit = (y % 4) == 0;
                            break;
                    }
                    tex.SetPixel(x, y, lit ? on : off);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
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

        public static InputField MakeInput(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string placeholder, bool password, int characterLimit = 16)
        {
            var rt = Panel(parent, name, anchorMin, anchorMax, Hex("2a1a10"));
            var img = rt.GetComponent<Image>();
            img.raycastTarget = true;
            var input = rt.gameObject.AddComponent<InputField>();
            input.targetGraphic = img;
            var text = Label(rt, "text", "", 22, TextAnchor.MiddleLeft, Cream);
            var trt = text.GetComponent<RectTransform>();
            trt.offsetMin = new Vector2(12, 4);
            trt.offsetMax = new Vector2(-12, -4);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.supportRichText = false;
            var ph = Label(rt, "ph", placeholder ?? "", 22, TextAnchor.MiddleLeft, new Color(0.72f, 0.62f, 0.45f, 0.55f));
            var prt = ph.GetComponent<RectTransform>();
            prt.offsetMin = new Vector2(12, 4);
            prt.offsetMax = new Vector2(-12, -4);
            input.textComponent = text;
            input.placeholder = ph;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = characterLimit > 0 ? characterLimit : (password ? 64 : 16);
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.caretColor = Gold;
            return input;
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

        public static string Inspect(UnitDef def, UnitInstance live)
        {
            if (def == null) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append(def.Name).AppendLine();
            sb.Append(def.Chorus);
            if (def.Spell) sb.Append("  Spell");
            sb.Append("  D").Append(def.Depth).AppendLine();
            if (def.Spell)
                sb.AppendLine("Play from hand to cast.");
            else
            {
                int atk = live != null ? live.EffectiveAtk : def.Atk;
                int hp = live != null ? live.Hp : def.Hp;
                sb.Append(atk).Append(" / ").Append(hp).AppendLine();
            }
            string kw = Keywords(live != null ? live.Keywords : def.Keywords, live != null && live.Awakened);
            if (kw.Length > 0) sb.AppendLine(kw);
            sb.Append(MechanicalLine(def));
            return sb.ToString();
        }

        public static string MechanicalLine(UnitDef def)
        {
            if (def == null) return "";
            if (def.Spell) return TriggersOf(def);
            string t = TriggersOf(def);
            if (t.Length == 0) return "Text comes later.";
            return t;
        }

        static string TriggersOf(UnitDef def)
        {
            if (def.Effects == null || def.Effects.Count == 0) return "";
            var seen = new System.Collections.Generic.List<string>();
            for (int i = 0; i < def.Effects.Count; i++)
            {
                string n = def.Effects[i].Trigger.ToString();
                bool hit = false;
                for (int s = 0; s < seen.Count; s++)
                    if (seen[s] == n) { hit = true; break; }
                if (!hit) seen.Add(n);
            }
            if (seen.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < seen.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(seen[i]);
            }
            return sb.ToString();
        }

        public static string ChorusTags(PlayerState p, Catalog cat)
        {
            if (p == null || cat == null) return "";
            var seen = new System.Collections.Generic.List<Chorus>();
            void add(UnitInstance u)
            {
                UnitDef d = cat.GetUnit(u.CatalogId);
                if (d == null || d.Chorus == Chorus.Neutral) return;
                for (int i = 0; i < seen.Count; i++)
                    if (seen[i] == d.Chorus) return;
                seen.Add(d.Chorus);
            }
            for (int i = 0; i < p.Board.Count; i++) add(p.Board[i]);
            if (seen.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < seen.Count; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append(seen[i]);
            }
            return sb.ToString();
        }
    }
}
