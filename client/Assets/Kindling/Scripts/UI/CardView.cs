using UnityEngine;
using UnityEngine.UI;
using Kindling.Sim.Catalog;
using Kindling.Sim.Model;

namespace Kindling.Client
{
    public sealed class CardView : MonoBehaviour
    {
        public Image Border;
        public Image Art;
        public Image Pattern;
        public Text NameLabel;
        public Text Stats;
        public Text Keys;
        public Text DepthLabel;
        public CardDrag Drag;
        public System.Action OnClicked;
        UnitInstance _unit;
        CaptainDef _cap;

        public static CardView Create(Transform parent, Vector2 size, CardZone zone, int index)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CardView), typeof(CardDrag), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            var cv = go.GetComponent<CardView>();
            cv.Border = go.GetComponent<Image>();
            cv.Border.color = HsUi.Gold;
            cv.Border.sprite = HsUi.Pixel(HsUi.Gold);
            cv.Border.raycastTarget = true;

            var inner = HsUi.Panel(rt, "inner", new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), HsUi.Wood);
            cv.Art = HsUi.Panel(inner, "art", new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.82f), HsUi.Felt).GetComponent<Image>();
            var pat = HsUi.Panel(cv.Art.transform, "pattern", Vector2.zero, Vector2.one, Color.white);
            cv.Pattern = pat.GetComponent<Image>();
            cv.Pattern.raycastTarget = false;
            cv.Pattern.enabled = false;
            cv.NameLabel = HsUi.Label(inner, "name", "", 16, TextAnchor.UpperCenter, HsUi.Cream);
            var nameRt = cv.NameLabel.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.05f, 0.82f);
            nameRt.anchorMax = new Vector2(0.95f, 0.98f);
            cv.Keys = HsUi.Label(inner, "keys", "", 12, TextAnchor.MiddleCenter, HsUi.Gold);
            var kRt = cv.Keys.GetComponent<RectTransform>();
            kRt.anchorMin = new Vector2(0.05f, 0.22f);
            kRt.anchorMax = new Vector2(0.95f, 0.34f);
            cv.Stats = HsUi.Label(inner, "stats", "0 / 0", 22, TextAnchor.MiddleCenter, Color.white);
            var sRt = cv.Stats.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0.05f, 0.02f);
            sRt.anchorMax = new Vector2(0.95f, 0.22f);
            cv.DepthLabel = HsUi.Label(inner, "d", "", 12, TextAnchor.UpperRight, HsUi.Gold);
            var dRt = cv.DepthLabel.GetComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0.6f, 0.70f);
            dRt.anchorMax = new Vector2(0.95f, 0.82f);

            cv.Drag = go.GetComponent<CardDrag>();
            cv.Drag.View = cv;
            cv.Drag.Zone = zone;
            cv.Drag.Index = index;
            cv.Drag.Clicked = _ => cv.OnClicked?.Invoke();
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;
            return cv;
        }

        public void BindUnit(UnitInstance u, Catalog cat, bool selected)
        {
            _unit = u;
            _cap = null;
            gameObject.SetActive(true);
            if (u == null)
            {
                NameLabel.text = "";
                Stats.text = "";
                Keys.text = "";
                DepthLabel.text = "";
                Art.color = new Color(0.12f, 0.08f, 0.05f, 0.45f);
                Border.color = new Color(0.45f, 0.32f, 0.12f, 0.4f);
                if (Pattern != null) Pattern.enabled = false;
                if (Drag != null) Drag.enabled = false;
                return;
            }
            if (Drag != null) Drag.enabled = true;
            UnitDef def = cat.GetUnit(u.CatalogId);
            string name = def != null ? def.Name : u.CatalogId.Value;
            NameLabel.text = name;
            Art.color = def != null ? HsUi.ChorusColor(def.Chorus) : HsUi.Felt;
            bool spell = def != null && def.Spell;
            Stats.text = spell ? "Spell" : (u.EffectiveAtk + " / " + u.Hp);
            Keys.text = spell ? "Play to cast" : HsUi.Keywords(u.Keywords, u.Awakened);
            DepthLabel.text = def != null ? (def.Chorus + " D" + def.Depth) : "";
            Border.color = selected ? HsUi.Selected : (spell ? HsUi.ChorusColor(Chorus.Spirit) : (u.Awakened ? Color.white : HsUi.Gold));
            PaintPattern(def, spell);
        }

        public void BindCaptain(CaptainDef def, bool selected)
        {
            _cap = def;
            _unit = null;
            if (def == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);
            NameLabel.text = def.Name;
            Art.color = HsUi.Ember;
            Stats.text = "Wick " + def.Wick;
            Keys.text = def.HasEdict ? ("Edict " + def.EdictCost) : "Passive";
            DepthLabel.text = "";
            Border.color = selected ? HsUi.Selected : HsUi.Gold;
            if (Pattern != null) Pattern.enabled = false;
        }

        public void BindPreview(UnitDef def)
        {
            _cap = null;
            _unit = null;
            gameObject.SetActive(def != null);
            if (def == null) return;
            if (Drag != null) Drag.enabled = false;
            NameLabel.text = def.Name;
            Art.color = HsUi.ChorusColor(def.Chorus);
            Stats.text = def.Spell ? "Spell" : (def.Atk + " / " + def.Hp);
            Keys.text = def.Spell ? "Play to cast" : HsUi.Keywords(def.Keywords, false);
            DepthLabel.text = def.Chorus + " D" + def.Depth;
            Border.color = def.Spell ? HsUi.ChorusColor(Chorus.Spirit) : HsUi.Gold;
            PaintPattern(def, def.Spell);
        }

        void PaintPattern(UnitDef def, bool spell)
        {
            if (Pattern == null) return;
            if (def == null || spell || def.Chorus == Chorus.Neutral)
            {
                Pattern.enabled = false;
                return;
            }
            Pattern.enabled = true;
            Pattern.sprite = HsUi.ChorusPattern(def.Chorus);
            Pattern.color = Color.white;
        }

        public UnitInstance Unit => _unit;
        public CaptainDef Captain => _cap;
    }
}
