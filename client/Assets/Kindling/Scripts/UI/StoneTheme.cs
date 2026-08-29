using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kindling.Client
{
    public static class StoneTheme
    {
        static Dictionary<string, Sprite> Sprites;
        static bool Tried;

        public static Sprite Get(string name)
        {
            Ensure();
            if (string.IsNullOrEmpty(name) || Sprites == null) return null;
            Sprite s;
            if (Sprites.TryGetValue(name, out s)) return s;
            if (Sprites.TryGetValue(name.ToLowerInvariant(), out s)) return s;
            return null;
        }

        public static bool Skin(Image img, string spriteName, bool sliced = true)
        {
            if (img == null) return false;
            Sprite s = Get(spriteName);
            if (s == null) return false;
            img.sprite = s;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.preserveAspect = !sliced;
            img.color = Color.white;
            return true;
        }

        public static string ButtonSprite(Color bg)
        {
            if (bg.g > bg.r && bg.g > 0.3f) return "Button01_Green";
            if (bg.r > 0.5f && bg.g < 0.35f) return "Button01_Red";
            if (bg.b > bg.r && bg.b > 0.3f) return "Button01_Blue";
            if (bg.r > 0.4f && bg.g > 0.25f) return "Button01_Brown";
            return "Button01_Brown";
        }

        public static string PanelSprite()
        {
            return Get("PanelFrame01_Demo") != null ? "PanelFrame01_Demo" : "PanelFrame01_White";
        }

        public static string InputSprite()
        {
            return Get("InputField_Bg_Demo_Normal") != null ? "InputField_Bg_Demo_Normal" : "InputField_Bg_White_Bg";
        }

        static void Ensure()
        {
            if (Tried) return;
            Tried = true;
            Sprites = new Dictionary<string, Sprite>(256);
            Sprite[] all = Resources.LoadAll<Sprite>("Stone");
            if (all == null) return;
            for (int i = 0; i < all.Length; i++)
            {
                Sprite s = all[i];
                if (s == null || string.IsNullOrEmpty(s.name)) continue;
                Sprites[s.name] = s;
                string low = s.name.ToLowerInvariant();
                if (!Sprites.ContainsKey(low)) Sprites[low] = s;
            }
        }
    }
}
