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
        public Text NameLabel;
        public Text Stats;
        public Text Keys;
        public Text DepthLabel;
        public System.Action OnClicked;
        UnitInstance _unit;
        CaptainDef _cap;

        public static CardView Create(Transform parent, Vector2 size)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(CardView));
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

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = cv.Border;
            btn.onClick.AddListener(() => cv.OnClicked?.Invoke());
            return cv;
        }

        public void BindUnit(UnitInstance u, Catalog cat, bool selected)
        {
            _unit = u;
            _cap = null;
            if (u == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);
            UnitDef def = cat.GetUnit(u.CatalogId);
            string name = def != null ? def.Name : u.CatalogId.Value;
            NameLabel.text = name;
            Art.color = def != null ? HsUi.ChorusColor(def.Chorus) : HsUi.Felt;
            Stats.text = u.EffectiveAtk + " / " + u.Hp;
            Keys.text = HsUi.Keywords(u.Keywords, u.Awakened);
            DepthLabel.text = def != null ? ("D" + def.Depth) : "";
            Border.color = selected ? HsUi.Selected : (u.Awakened ? Color.white : HsUi.Gold);
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
        }

        public UnitInstance Unit => _unit;
        public CaptainDef Captain => _cap;
    }
}
