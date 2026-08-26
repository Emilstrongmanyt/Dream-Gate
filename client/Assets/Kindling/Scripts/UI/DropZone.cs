using UnityEngine;
using UnityEngine.UI;

namespace Kindling.Client
{
    public enum CardZone
    {
        Stall,
        Board,
        Hand,
        Offer
    }

    public sealed class DropZone : MonoBehaviour
    {
        public CardZone Zone;
        Image _img;
        Color _base;

        public void Init(CardZone zone, Image img)
        {
            Zone = zone;
            _img = img;
            _base = img != null ? img.color : Color.clear;
        }

        public void SetHot(bool hot)
        {
            if (_img == null) return;
            _img.color = hot ? Color.Lerp(_base, HsUi.Gold, 0.35f) : _base;
        }
    }
}
