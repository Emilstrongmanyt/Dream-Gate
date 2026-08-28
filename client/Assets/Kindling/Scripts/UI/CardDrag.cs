using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kindling.Client
{
    public sealed class CardDrag : MonoBehaviour,
        IPointerDownHandler, IInitializePotentialDragHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public CardView View;
        public CardZone Zone;
        public int Index;
        public System.Action<CardDrag> Clicked;
        public System.Action<CardDrag, PointerEventData> DragBegan;
        public System.Action<CardDrag, PointerEventData> DragMoved;
        public System.Action<CardDrag, PointerEventData> DragEnded;

        CanvasGroup _group;
        bool _dragging;
        bool _moved;
        bool _clicked;

        void Awake()
        {
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _moved = false;
            _clicked = false;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (Zone == CardZone.Offer)
                eventData.pointerDrag = null;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Zone == CardZone.Offer)
            {
                FireClick();
                return;
            }
            if (View == null || View.Unit == null) return;
            if (Zone != CardZone.Stall && Zone != CardZone.Hand && Zone != CardZone.Board) return;
            _dragging = true;
            _moved = true;
            if (_group != null) _group.blocksRaycasts = false;
            DragBegan?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            DragMoved?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_group != null) _group.blocksRaycasts = true;
            DragEnded?.Invoke(this, eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_moved) return;
            FireClick();
        }

        void FireClick()
        {
            if (_clicked) return;
            _clicked = true;
            Clicked?.Invoke(this);
        }

        public static DropZone HitZone(PointerEventData eventData)
        {
            if (EventSystem.current == null) return null;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                var z = hits[i].gameObject.GetComponentInParent<DropZone>();
                if (z != null) return z;
            }
            return null;
        }

        public static CardView HitCard(PointerEventData eventData, GameObject ignore)
        {
            if (EventSystem.current == null) return null;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                var cv = hits[i].gameObject.GetComponentInParent<CardView>();
                if (cv == null) continue;
                if (ignore != null && (cv.gameObject == ignore || cv.transform.IsChildOf(ignore.transform)))
                    continue;
                if (cv.Unit == null) continue;
                return cv;
            }
            return null;
        }
    }
}
