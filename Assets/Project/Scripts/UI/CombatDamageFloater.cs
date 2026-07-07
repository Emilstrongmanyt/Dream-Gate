using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DreamGate.Battlegrounds.UI
{
    public enum CombatDamageStyle
    {
        Incoming,
        Recoil,
        Cleave,
        Hero
    }

    public class CombatDamageFloater : MonoBehaviour
    {
        private const int PoolSize = 12;
        private const float FloatDistance = 72f;
        private const float Duration = 0.85f;

        private readonly Queue<TextMeshProUGUI> pool = new();
        private Transform poolRoot;

        public void Initialize(Transform parent)
        {
            poolRoot = parent;
            for (var i = 0; i < PoolSize; i++)
            {
                pool.Enqueue(CreateLabel());
            }
        }

        public IEnumerator Show(RectTransform anchor, int amount, CombatDamageStyle style)
        {
            if (anchor == null || amount <= 0 || pool.Count == 0)
            {
                yield break;
            }

            var label = pool.Dequeue();
            var rect = label.rectTransform;
            rect.SetParent(poolRoot, false);
            rect.position = anchor.position + new Vector3(0f, 18f, 0f);
            rect.localScale = Vector3.one;

            label.text = style == CombatDamageStyle.Hero ? $"-{amount}" : amount.ToString();
            label.fontSize = GetFontSize(style);
            label.color = GetColor(style);
            label.gameObject.SetActive(true);

            var startPos = rect.anchoredPosition;
            var elapsed = 0f;
            var popScale = 1.35f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / Duration);
                var rise = Mathf.SmoothStep(0f, 1f, t);
                rect.anchoredPosition = startPos + new Vector2(0f, FloatDistance * rise);

                var scaleT = t < 0.18f ? t / 0.18f : 1f;
                var scale = Mathf.Lerp(popScale, 1f, scaleT);
                rect.localScale = Vector3.one * scale;

                var alpha = t < 0.55f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.55f) / 0.45f);
                var color = label.color;
                color.a = alpha;
                label.color = color;

                yield return null;
            }

            label.gameObject.SetActive(false);
            pool.Enqueue(label);
        }

        private TextMeshProUGUI CreateLabel()
        {
            var go = new GameObject("DamageFloat", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(poolRoot, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120f, 72f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.outlineWidth = 0.28f;
            text.outlineColor = new Color(0.05f, 0.05f, 0.08f, 0.95f);
            text.raycastTarget = false;
            go.SetActive(false);
            return text;
        }

        private static float GetFontSize(CombatDamageStyle style)
        {
            return style switch
            {
                CombatDamageStyle.Hero => 56f,
                CombatDamageStyle.Incoming => 50f,
                CombatDamageStyle.Cleave => 44f,
                CombatDamageStyle.Recoil => 42f,
                _ => 48f
            };
        }

        private static Color GetColor(CombatDamageStyle style)
        {
            return style switch
            {
                CombatDamageStyle.Hero => new Color(1f, 0.32f, 0.28f),
                CombatDamageStyle.Incoming => new Color(1f, 0.38f, 0.22f),
                CombatDamageStyle.Cleave => new Color(1f, 0.58f, 0.18f),
                CombatDamageStyle.Recoil => new Color(0.82f, 0.45f, 1f),
                _ => Color.white
            };
        }
    }
}