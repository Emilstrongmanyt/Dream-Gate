using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Kindling.Sim;
using Kindling.Sim.Catalog;
using Kindling.Sim.Match;
using Kindling.Sim.Model;

namespace Kindling.Client
{
    public sealed class OfflineMatchApp : MonoBehaviour
    {
        MatchLoop _loop;
        Catalog _cat;
        enum SelKind { None, Stall, Board, Hand, CaptainOffer }
        SelKind _sel;
        int _selIndex;
        string _toast;
        float _toastUntil;
        bool _showingCombat;

        readonly List<CardView> _stallCards = new List<CardView>();
        readonly List<CardView> _boardCards = new List<CardView>();
        readonly List<CardView> _handCards = new List<CardView>();
        readonly List<CardView> _offerCards = new List<CardView>();
        Text _hud;
        Text _log;
        Text _toastLabel;
        CombatPlayback _playback;
        GameObject _pickPanel;
        Text _infoLabel;
        GameObject _recruitRoot;
        Canvas _canvas;
        DropZone _handZone;
        DropZone _boardZone;
        CardDrag _activeDrag;
        Transform _dragOriginParent;
        int _dragOriginSibling;
        Text _timerLabel;
        Button _edictBtn;
        GameObject _glimpsePanel;
        readonly List<CardView> _glimpseCards = new List<CardView>();
        float _timerEnd;
        bool _timerArmed;
        bool _edictTargeting;
        int _awakenSeen;
        bool _lowTimeWarned;
        GameObject _helpPanel;
        Text _helpText;
        int _helpStep;
        NetMatchClient _net;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindObjectOfType<OfflineMatchApp>() != null) return;
            var go = new GameObject("KindlingApp");
            DontDestroyOnLoad(go);
            go.AddComponent<OfflineMatchApp>();
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(es);
            }
        }

        void Start()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Application.targetFrameRate = 60;
            if (Camera.main != null) Camera.main.backgroundColor = HsUi.Felt;

            string content = FindContent();
            if (content == null)
            {
                Debug.LogError("Kindling content/ not found");
                return;
            }
            _cat = Catalog.LoadFromDirectory(content);
            _loop = MatchLoop.Create(_cat, Guid.NewGuid(), (uint)Environment.TickCount, humanSeats: 1);
            _loop.AutoPickBotCaptains();
            BuildUi();
            ArmTimer(Rules.CaptainPickSeconds);
            string host = NetMatchClient.ResolveHost();
            if (!string.IsNullOrEmpty(host))
            {
                _net = gameObject.AddComponent<NetMatchClient>();
                _net.OnSnapshot = OnNetSnapshot;
                StartCoroutine(_net.Connect(host));
                Toast("Connecting " + host);
            }
            Refresh();
        }

        static string FindContent()
        {
            string fromAssets = Catalog.FindContentRoot(Application.dataPath);
            if (fromAssets != null) return fromAssets;
            string sa = Path.Combine(Application.streamingAssetsPath, "content");
            if (Directory.Exists(Path.Combine(sa, "units"))) return sa;
            return Catalog.FindContentRoot(Directory.GetCurrentDirectory());
        }

        void BuildUi()
        {
            var canvasGo = HsUi.Canvas("KindlingCanvas");
            _canvas = canvasGo.GetComponent<Canvas>();
            var canvas = canvasGo;
            HsUi.Panel(canvas.transform, "felt", Vector2.zero, Vector2.one, HsUi.Felt);

            HsUi.Label(HsUi.Panel(canvas.transform, "title", new Vector2(0.01f, 0.94f), new Vector2(0.40f, 0.995f), Color.clear),
                "t", "KINDLING  ·  The Ember Exchange", 26, TextAnchor.MiddleLeft, HsUi.Gold);

            _hud = HsUi.Label(HsUi.Panel(canvas.transform, "hud", new Vector2(0.40f, 0.94f), new Vector2(0.99f, 0.995f), Color.clear),
                "hud", "", 22, TextAnchor.MiddleRight, HsUi.Cream);

            // Stall
            var stallBar = HsUi.Panel(canvas.transform, "stallBar", new Vector2(0.02f, 0.70f), new Vector2(0.72f, 0.93f), HsUi.Wood);
            HsUi.Label(stallBar, "sl", "STALL  ·  drag into hand to buy  ·  timer auto-starts combat", 16, TextAnchor.UpperLeft, HsUi.Gold);
            var stallRow = HsUi.Panel(stallBar, "row", new Vector2(0.01f, 0.02f), new Vector2(0.99f, 0.88f), Color.clear);
            var hlg = stallRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(8, 8, 4, 4);
            for (int i = 0; i < 7; i++)
            {
                int idx = i;
                var cv = CardView.Create(stallRow, new Vector2(130, 180), CardZone.Stall, idx);
                WireDrag(cv, idx);
                cv.OnClicked = () => Select(SelKind.Stall, idx);
                _stallCards.Add(cv);
            }

            // Board
            var boardBar = HsUi.Panel(canvas.transform, "boardBar", new Vector2(0.02f, 0.38f), new Vector2(0.72f, 0.69f), new Color(0.12f, 0.08f, 0.05f, 1));
            HsUi.Label(boardBar, "bl", "WARBAND  ·  drop from hand to set position", 16, TextAnchor.UpperLeft, HsUi.Gold);
            _boardZone = boardBar.gameObject.AddComponent<DropZone>();
            _boardZone.Init(CardZone.Board, boardBar.GetComponent<Image>());
            var boardRow = HsUi.Panel(boardBar, "row", new Vector2(0.01f, 0.02f), new Vector2(0.99f, 0.88f), Color.clear);
            var hlg2 = boardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg2.spacing = 8; hlg2.childAlignment = TextAnchor.MiddleCenter;
            hlg2.childForceExpandWidth = false; hlg2.childForceExpandHeight = true;
            for (int i = 0; i < 7; i++)
            {
                int idx = i;
                var cv = CardView.Create(boardRow, new Vector2(130, 180), CardZone.Board, idx);
                WireDrag(cv, idx);
                cv.OnClicked = () => Select(SelKind.Board, idx);
                _boardCards.Add(cv);
            }

            // Hand
            var handBar = HsUi.Panel(canvas.transform, "handBar", new Vector2(0.02f, 0.14f), new Vector2(0.72f, 0.37f), HsUi.Wood);
            HsUi.Label(handBar, "hl", "HAND  ·  drop from stall to buy", 16, TextAnchor.UpperLeft, HsUi.Gold);
            _handZone = handBar.gameObject.AddComponent<DropZone>();
            _handZone.Init(CardZone.Hand, handBar.GetComponent<Image>());
            var handRow = HsUi.Panel(handBar, "row", new Vector2(0.01f, 0.02f), new Vector2(0.99f, 0.88f), Color.clear);
            var hlg3 = handRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg3.spacing = 6; hlg3.childAlignment = TextAnchor.MiddleLeft;
            hlg3.childForceExpandWidth = false; hlg3.childForceExpandHeight = true;
            for (int i = 0; i < 10; i++)
            {
                int idx = i;
                var cv = CardView.Create(handRow, new Vector2(110, 150), CardZone.Hand, idx);
                WireDrag(cv, idx);
                cv.OnClicked = () => Select(SelKind.Hand, idx);
                _handCards.Add(cv);
            }

            // Leaderboard
            var lb = HsUi.Panel(canvas.transform, "lb", new Vector2(0.735f, 0.38f), new Vector2(0.99f, 0.93f), HsUi.Wood);
            HsUi.Label(lb, "lbt", "THE CROWN", 16, TextAnchor.UpperCenter, HsUi.Gold);
            _log = HsUi.Label(lb, "log", "", 16, TextAnchor.UpperLeft, HsUi.Cream);
            var lrt = _log.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.04f, 0.02f);
            lrt.anchorMax = new Vector2(0.96f, 0.90f);
            _log.alignment = TextAnchor.UpperLeft;
            _log.horizontalOverflow = HorizontalWrapMode.Wrap;
            _log.verticalOverflow = VerticalWrapMode.Overflow;

            // Actions
            var act = HsUi.Panel(canvas.transform, "act", new Vector2(0.02f, 0.01f), new Vector2(0.72f, 0.13f), HsUi.Wood);
            HsUi.MakeButton(act, "buyH", "Buy  3", new Vector2(0.01f, 0.15f), new Vector2(0.16f, 0.85f), HsUi.GoldDark, BuyToHand);
            HsUi.MakeButton(act, "sell", "Sell  +1", new Vector2(0.17f, 0.15f), new Vector2(0.32f, 0.85f), HsUi.WickRed, Sell);
            _edictBtn = HsUi.MakeButton(act, "edict", "Edict", new Vector2(0.33f, 0.15f), new Vector2(0.49f, 0.85f), HsUi.ChorusColor(Chorus.Spirit), Edict);
            HsUi.MakeButton(act, "reroll", "Reroll  1", new Vector2(0.50f, 0.15f), new Vector2(0.66f, 0.85f), HsUi.Ember, Reroll);
            HsUi.MakeButton(act, "hold", "Hold", new Vector2(0.67f, 0.15f), new Vector2(0.82f, 0.85f), HsUi.ChorusColor(Chorus.Humanoid), Hold);
            HsUi.MakeButton(act, "up", "Upgrade", new Vector2(0.83f, 0.15f), new Vector2(0.99f, 0.85f), HsUi.Gold, Upgrade);

            HsUi.MakeButton(canvas.transform, "ready", "READY", new Vector2(0.735f, 0.01f), new Vector2(0.99f, 0.13f), new Color(0.15f, 0.45f, 0.18f), Ready);
            _timerLabel = HsUi.Label(HsUi.Panel(canvas.transform, "timer", new Vector2(0.735f, 0.14f), new Vector2(0.99f, 0.20f), Color.clear),
                "timer", "COMBAT IN 0:15", 24, TextAnchor.MiddleCenter, HsUi.Gold);
            _infoLabel = HsUi.Label(HsUi.Panel(canvas.transform, "info", new Vector2(0.735f, 0.21f), new Vector2(0.99f, 0.37f), HsUi.Wood),
                "info", "Tap a card for tribe and keywords.\nAuthored text comes later.", 14, TextAnchor.UpperLeft, HsUi.Cream);
            var irt = _infoLabel.GetComponent<RectTransform>();
            irt.offsetMin = new Vector2(8, 6);
            irt.offsetMax = new Vector2(-8, -6);
            _infoLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _infoLabel.verticalOverflow = VerticalWrapMode.Overflow;

            _toastLabel = HsUi.Label(HsUi.Panel(canvas.transform, "toast", new Vector2(0.25f, 0.48f), new Vector2(0.75f, 0.56f), new Color(0, 0, 0, 0.0f)),
                "toast", "", 24, TextAnchor.MiddleCenter, HsUi.Selected);

            _recruitRoot = canvas;

            // Captain pick overlay
            _pickPanel = HsUi.Panel(canvas.transform, "pick", Vector2.zero, Vector2.one, new Color(0.05f, 0.03f, 0.02f, 0.92f)).gameObject;
            HsUi.Label(_pickPanel.transform, "pt", "Choose your Captain", 36, TextAnchor.UpperCenter, HsUi.Gold)
                .GetComponent<RectTransform>().anchorMin = new Vector2(0.1f, 0.78f);
            var offerRow = HsUi.Panel(_pickPanel.transform, "offers", new Vector2(0.12f, 0.28f), new Vector2(0.88f, 0.72f), Color.clear);
            var oh = offerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            oh.spacing = 24; oh.childAlignment = TextAnchor.MiddleCenter;
            oh.childForceExpandWidth = false; oh.childForceExpandHeight = true;
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var cv = CardView.Create(offerRow, new Vector2(220, 320), CardZone.Offer, idx);
                cv.OnClicked = () => PickCaptain(idx);
                var le = cv.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 220; le.preferredHeight = 320;
                _offerCards.Add(cv);
            }
            HsUi.Label(_pickPanel.transform, "hint", "Empty seats are filled with bots. Art is placeholder — import later.", 18, TextAnchor.LowerCenter, HsUi.Cream)
                .GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.22f);

            _playback = new CombatPlayback();
            _playback.Build(canvas.transform, NextAfterCombat);

            _glimpsePanel = HsUi.Panel(canvas.transform, "glimpse", Vector2.zero, Vector2.one, new Color(0.06f, 0.03f, 0.08f, 0.93f)).gameObject;
            HsUi.Label(_glimpsePanel.transform, "gt", "GLIMPSE  ·  choose one", 32, TextAnchor.UpperCenter, HsUi.Gold)
                .GetComponent<RectTransform>().anchorMin = new Vector2(0.1f, 0.78f);
            var gRow = HsUi.Panel(_glimpsePanel.transform, "goffers", new Vector2(0.10f, 0.28f), new Vector2(0.90f, 0.74f), Color.clear);
            var gh = gRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            gh.spacing = 20; gh.childAlignment = TextAnchor.MiddleCenter;
            gh.childForceExpandWidth = false; gh.childForceExpandHeight = true;
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var cv = CardView.Create(gRow, new Vector2(200, 300), CardZone.Offer, idx);
                cv.OnClicked = () => PickGlimpse(idx);
                var le = cv.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 200; le.preferredHeight = 300;
                _glimpseCards.Add(cv);
            }
            HsUi.Label(_glimpsePanel.transform, "gh", "Timer does not pause.", 18, TextAnchor.LowerCenter, HsUi.Cream)
                .GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.22f);
            _glimpsePanel.SetActive(false);

            _helpPanel = HsUi.Panel(canvas.transform, "help", new Vector2(0.18f, 0.40f), new Vector2(0.82f, 0.62f), new Color(0.08f, 0.05f, 0.02f, 0.94f)).gameObject;
            _helpText = HsUi.Label(_helpPanel.transform, "ht", "", 20, TextAnchor.UpperLeft, HsUi.Cream);
            var hrt = _helpText.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.04f, 0.28f);
            hrt.anchorMax = new Vector2(0.96f, 0.94f);
            _helpText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _helpText.verticalOverflow = VerticalWrapMode.Overflow;
            HsUi.MakeButton(_helpPanel.transform, "next", "GOT IT", new Vector2(0.55f, 0.06f), new Vector2(0.96f, 0.26f), HsUi.GoldDark, AdvanceHelp);
            HsUi.MakeButton(_helpPanel.transform, "skip", "SKIP", new Vector2(0.04f, 0.06f), new Vector2(0.48f, 0.26f), HsUi.WickRed, SkipHelp);
            if (PlayerPrefs.GetInt("kindling.help.v1", 0) == 1)
                _helpPanel.SetActive(false);
            else
            {
                _helpStep = 0;
                PaintHelp();
            }
        }

        void Select(SelKind kind, int index)
        {
            if (_loop.State.Phase != Phase.Recruit) return;
            if (_edictTargeting && kind == SelKind.Board)
            {
                FireEdict(index);
                return;
            }
            if (_sel == SelKind.Board && kind == SelKind.Board && _selIndex != index)
            {
                TryReorder(_selIndex, index);
                _sel = SelKind.None;
                Refresh();
                return;
            }
            _sel = kind;
            _selIndex = index;
            Refresh();
        }

        void TryReorder(int from, int to)
        {
            var p = _loop.Human;
            if (p == null || from < 0 || to < 0 || from >= p.Board.Count || to >= p.Board.Count) return;
            int[] perm = new int[p.Board.Count];
            for (int i = 0; i < perm.Length; i++) perm[i] = i;
            int tmp = perm[from];
            perm[from] = perm[to];
            perm[to] = tmp;
            Apply(new RecruitAction { Op = RecruitOp.Reorder, Seat = p.Seat, BoardPerm = perm });
        }

        void PickCaptain(int offerIndex)
        {
            var p = _loop.Human;
            if (p == null) return;
            var r = Apply(new RecruitAction { Op = RecruitOp.CaptainPick, Seat = p.Seat, OfferIndex = offerIndex });
            if (r.Ok)
            {
                _loop.StartFromCaptainPick();
                _pickPanel.SetActive(false);
                _awakenSeen = _loop.State.AwakenEvents;
                ArmTimer(Rules.RecruitSeconds(_loop.State.Round));
                Refresh();
            }
        }

        void WireDrag(CardView cv, int index)
        {
            if (cv.Drag == null) return;
            cv.Drag.Index = index;
            cv.Drag.DragBegan = OnCardDragBegan;
            cv.Drag.DragMoved = OnCardDragMoved;
            cv.Drag.DragEnded = OnCardDragEnded;
        }

        void OnCardDragBegan(CardDrag drag, PointerEventData ev)
        {
            if (_loop == null || _loop.State.Phase != Phase.Recruit) return;
            _activeDrag = drag;
            _dragOriginParent = drag.transform.parent;
            _dragOriginSibling = drag.transform.GetSiblingIndex();
            drag.transform.SetParent(_canvas.transform, true);
            drag.transform.SetAsLastSibling();
        }

        void OnCardDragMoved(CardDrag drag, PointerEventData ev)
        {
            drag.transform.position = ev.position;
            DropZone z = CardDrag.HitZone(ev);
            if (_handZone != null) _handZone.SetHot(z == _handZone);
            if (_boardZone != null) _boardZone.SetHot(z == _boardZone);
        }

        void OnCardDragEnded(CardDrag drag, PointerEventData ev)
        {
            DropZone z = CardDrag.HitZone(ev);
            if (_handZone != null) _handZone.SetHot(false);
            if (_boardZone != null) _boardZone.SetHot(false);

            bool handled = false;
            if (z != null && _loop != null && _loop.State.Phase == Phase.Recruit)
            {
                if (drag.Zone == CardZone.Stall && z.Zone == CardZone.Hand)
                {
                    BuyStallToHand(drag.Index, InsertIndex(_handCards, Occupied(_handCards), ev));
                    handled = true;
                }
                else if (drag.Zone == CardZone.Stall && z.Zone == CardZone.Board)
                {
                    Toast("Bought Kindled go to your hand first");
                }
                else if (drag.Zone == CardZone.Hand && z.Zone == CardZone.Board)
                {
                    CardView host = CardDrag.HitCard(ev, drag.gameObject);
                    if (TryLatchDrop(drag, host))
                        handled = true;
                    else
                    {
                        PlayHandToBoard(drag.Index, InsertIndex(_boardCards, Occupied(_boardCards), ev));
                        handled = true;
                    }
                }
                else if (drag.Zone == CardZone.Board && z.Zone == CardZone.Board)
                {
                    CardView host = CardDrag.HitCard(ev, drag.gameObject);
                    if (TryLatchDrop(drag, host))
                        handled = true;
                    else
                    {
                        int to = InsertIndex(_boardCards, Occupied(_boardCards), ev);
                        if (to > drag.Index) to--;
                        TryReorder(drag.Index, Mathf.Clamp(to, 0, Mathf.Max(0, Occupied(_boardCards) - 1)));
                        handled = true;
                    }
                }
            }

            if (_dragOriginParent != null)
            {
                drag.transform.SetParent(_dragOriginParent, false);
                drag.transform.SetSiblingIndex(_dragOriginSibling);
            }
            _activeDrag = null;
            _dragOriginParent = null;
            Refresh();
            if (!handled && z == null) { /* snap back via Refresh */ }
        }

        static int Occupied(List<CardView> cards)
        {
            int n = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && cards[i].Unit != null) n++;
            }
            return n;
        }

        static int InsertIndex(List<CardView> cards, int occupied, PointerEventData ev)
        {
            if (occupied <= 0) return 0;
            int limit = occupied < cards.Count ? occupied : cards.Count;
            for (int i = 0; i < limit; i++)
            {
                var rt = cards[i].GetComponent<RectTransform>();
                Vector3[] c = new Vector3[4];
                rt.GetWorldCorners(c);
                float mid = (c[0].x + c[2].x) * 0.5f;
                if (ev.position.x < mid) return i;
            }
            return occupied;
        }

        void BuyToHand()
        {
            var p = _loop.Human;
            if (p == null || _sel != SelKind.Stall) { Toast("Drag a stall card into your hand"); return; }
            BuyStallToHand(_selIndex, p.Hand.Count);
        }

        void BuyStallToHand(int stallIndex, int handIndex)
        {
            var p = _loop.Human;
            if (p == null) return;
            Apply(new RecruitAction
            {
                Op = RecruitOp.Buy,
                Seat = p.Seat,
                StallIndex = stallIndex,
                Dest = DestLoc.Hand,
                DestIndex = handIndex
            });
            _sel = SelKind.None;
        }

        void PlayHandToBoard(int handIndex, int boardIndex)
        {
            var p = _loop.Human;
            if (p == null) return;
            Apply(new RecruitAction
            {
                Op = RecruitOp.Play,
                Seat = p.Seat,
                HandIndex = handIndex,
                DestIndex = boardIndex,
                Dest = DestLoc.Board
            });
            _sel = SelKind.None;
        }

        void Sell()
        {
            var p = _loop.Human;
            if (p == null) return;
            if (_sel == SelKind.Board)
                Apply(new RecruitAction { Op = RecruitOp.Sell, Seat = p.Seat, Loc = DestLoc.Board, Index = _selIndex });
            else if (_sel == SelKind.Hand)
                Apply(new RecruitAction { Op = RecruitOp.Sell, Seat = p.Seat, Loc = DestLoc.Hand, Index = _selIndex });
            else { Toast("Select a board or hand card to sell"); return; }
            _sel = SelKind.None;
            Refresh();
        }

        bool TryLatchDrop(CardDrag drag, CardView hostCard)
        {
            if (hostCard == null || hostCard.Unit == null || hostCard.Drag == null) return false;
            if (hostCard.Drag.Zone != CardZone.Board) return false;
            if (drag.View == null || drag.View.Unit == null || !drag.View.Unit.Has(Keyword.Latch))
                return false;
            DestLoc from = drag.Zone == CardZone.Hand ? DestLoc.Hand : DestLoc.Board;
            SimResult r = Apply(new RecruitAction
            {
                Op = RecruitOp.Latch,
                Seat = _loop.Human.Seat,
                From = from,
                FromIndex = drag.Index,
                HostIndex = hostCard.Drag.Index
            });
            return r.Ok;
        }

        void Edict()
        {
            var p = _loop.Human;
            if (p == null) return;
            CaptainDef def = _cat.GetCaptain(p.Captain);
            if (def == null || !def.HasEdict) { Toast("No edict"); return; }
            if (p.Edict != null && p.Edict.UsedThisRecruit && !p.Edict.Repeatable)
            { Toast("Edict already used"); return; }
            if (def.EdictNeedsTarget)
            {
                _edictTargeting = true;
                Toast("Choose a warband target");
                return;
            }
            FireEdict(-1);
        }

        void FireEdict(int targetIndex)
        {
            var p = _loop.Human;
            if (p == null) return;
            _edictTargeting = false;
            Apply(new RecruitAction
            {
                Op = RecruitOp.Edict,
                Seat = p.Seat,
                TargetIndex = targetIndex
            });
            Refresh();
        }

        void Reroll()
        {
            var p = _loop.Human;
            if (p == null) return;
            Apply(new RecruitAction { Op = RecruitOp.Reroll, Seat = p.Seat });
            _sel = SelKind.None;
            Refresh();
        }

        void Hold()
        {
            var p = _loop.Human;
            if (p == null) return;
            Apply(new RecruitAction { Op = RecruitOp.Hold, Seat = p.Seat, Held = !p.Hold });
            Refresh();
        }

        void Upgrade()
        {
            var p = _loop.Human;
            if (p == null) return;
            Apply(new RecruitAction { Op = RecruitOp.Upgrade, Seat = p.Seat });
            Refresh();
        }

        void Ready()
        {
            if (_loop.State.Phase != Phase.Recruit) return;
            var p = _loop.Human;
            if (p != null && p.HasFlag(PlayerFlags.GlimpseOpen) && p.GlimpseQueue.Count > 0)
            {
                Toast("Choose a Glimpse first");
                Refresh();
                return;
            }
            LockRecruit();
        }

        void LockRecruit()
        {
            _timerArmed = false;
            _edictTargeting = false;
            if (_helpPanel != null) _helpPanel.SetActive(false);
            _loop.ResolveRecruitAndCombat();
            _showingCombat = true;
            if (_glimpsePanel != null) _glimpsePanel.SetActive(false);
            _playback.Begin(_loop.LastHumanCombat, _loop.HumanSeat, _cat, _loop.State.MatchOver, StandingsLine());
            Refresh();
        }

        void NextAfterCombat()
        {
            _showingCombat = false;
            if (_playback != null && _playback.Root != null)
                _playback.Root.SetActive(false);
            if (!_loop.State.MatchOver)
            {
                _loop.ContinueToNextRecruit();
                _awakenSeen = _loop.State.AwakenEvents;
                ArmTimer(Rules.RecruitSeconds(_loop.State.Round));
            }
            Refresh();
        }

        void ArmTimer(int seconds)
        {
            _timerArmed = true;
            _lowTimeWarned = false;
            _timerEnd = Time.unscaledTime + seconds;
            PaintTimer();
        }

        void PaintTimer()
        {
            if (_timerLabel == null) return;
            if (!_timerArmed)
            {
                _timerLabel.text = "";
                return;
            }
            int left = Mathf.Max(0, Mathf.CeilToInt(_timerEnd - Time.unscaledTime));
            _timerLabel.color = left <= 5 ? HsUi.WickRed : HsUi.Gold;
            string clock = (left / 60) + ":" + (left % 60).ToString("00");
            bool pick = _loop != null && _loop.State.Phase == Phase.CaptainPick;
            _timerLabel.text = pick ? ("PICK  " + clock) : ("COMBAT IN  " + clock);
            if (!pick && !_lowTimeWarned && left <= 5 && left > 0)
            {
                _lowTimeWarned = true;
                Toast("Combat in " + left + "s");
            }
        }

        void PickGlimpse(int offerIndex)
        {
            var p = _loop.Human;
            if (p == null) return;
            Apply(new RecruitAction { Op = RecruitOp.GlimpsePick, Seat = p.Seat, OfferIndex = offerIndex });
            Refresh();
        }

        void OnNetSnapshot(string json)
        {
            if (_loop == null || string.IsNullOrEmpty(json)) return;
            SnapshotApply.Apply(_loop.State, _loop.HumanSeat, _cat, json);
            int t = Protocol.ReadInt(json, "timer");
            if (t > 0)
            {
                _timerArmed = true;
                _timerEnd = Time.unscaledTime + t;
            }
            Refresh();
        }

        SimResult Apply(RecruitAction a)
        {
            if (_net != null && _net.Connected)
            {
                _net.SendAction(a);
                return SimResult.Success();
            }
            SimResult r = _loop.Try(a);
            if (!r.Ok) Toast(r.Code ?? "illegal");
            else if (_loop.State.AwakenEvents > _awakenSeen)
            {
                _awakenSeen = _loop.State.AwakenEvents;
                Toast("AWAKENED");
            }
            return r;
        }

        void Toast(string msg)
        {
            _toast = msg;
            _toastUntil = Time.unscaledTime + 2.2f;
            if (_toastLabel != null) _toastLabel.text = msg;
        }

        void Update()
        {
            if (_toastLabel != null && Time.unscaledTime > _toastUntil)
                _toastLabel.text = "";
            if (_showingCombat && _playback != null)
            {
                _playback.Tick();
                if (_playback.Done && _loop != null && !_loop.State.MatchOver
                    && Time.unscaledTime >= _playback.DoneAt + Rules.CombatAutoContinueSeconds)
                    NextAfterCombat();
            }
            if (!_timerArmed || _loop == null) return;
            PaintTimer();
            if (Time.unscaledTime < _timerEnd) return;
            _timerArmed = false;
            if (_loop.State.Phase == Phase.CaptainPick)
                PickCaptain(0);
            else if (_loop.State.Phase == Phase.Recruit)
            {
                Toast("Time — combat starts");
                LockRecruit();
            }
        }

        string StandingsLine()
        {
            if (_loop == null || !_loop.State.MatchOver) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _loop.State.Seats.Length; i++)
            {
                var s = _loop.State.Seats[i];
                if (i > 0) sb.Append("   ");
                sb.Append('#').Append(s.Place ?? 0).Append(' ').Append(s.DisplayName);
            }
            return sb.ToString();
        }

        void Refresh()
        {
            if (_loop == null) return;
            var p = _loop.Human;
            bool picking = _loop.State.Phase == Phase.CaptainPick;
            _pickPanel.SetActive(picking);
            if (picking && p != null && p.CaptainOffers != null)
            {
                for (int i = 0; i < _offerCards.Count; i++)
                {
                    CaptainDef def = i < p.CaptainOffers.Length ? _cat.GetCaptain(p.CaptainOffers[i]) : null;
                    _offerCards[i].BindCaptain(def, false);
                }
            }

            bool glimpse = p != null && p.HasFlag(PlayerFlags.GlimpseOpen) && p.GlimpseQueue.Count > 0
                           && !_showingCombat && _loop.State.Phase == Phase.Recruit;
            if (_glimpsePanel != null) _glimpsePanel.SetActive(glimpse);
            if (glimpse)
            {
                GlimpseOffer offer = p.GlimpseQueue.Peek();
                for (int i = 0; i < _glimpseCards.Count; i++)
                {
                    UnitDef def = null;
                    if (offer.Choices != null && i < offer.Choices.Length)
                        def = _cat.GetUnit(offer.Choices[i]);
                    _glimpseCards[i].BindPreview(def);
                }
            }

            if (p == null) return;
            CaptainDef cap = _cat.GetCaptain(p.Captain);
            if (_edictBtn != null)
            {
                var capText = _edictBtn.GetComponentInChildren<Text>();
                if (capText != null)
                {
                    if (cap != null && cap.HasEdict)
                        capText.text = _edictTargeting ? "Edict…" : ("Edict  " + cap.EdictCost);
                    else
                        capText.text = "Edict";
                }
            }
            _hud.text = "Round " + _loop.State.Round
                + "   Wick " + p.Wick
                + "   Embers " + p.Embers
                + "   Depth " + p.Depth
                + "   Upgrade " + p.UpgradeCost
                + (p.Hold ? "   HOLD" : "")
                + (_edictTargeting ? "   EDICT TARGET" : "")
                + "   " + (p.Captain.IsEmpty ? "" : NameOfCaptain(p.Captain.Value));

            for (int i = 0; i < _stallCards.Count; i++)
            {
                UnitInstance u = i < p.Stall.Count ? p.Stall[i] : null;
                _stallCards[i].BindUnit(u, _cat, _sel == SelKind.Stall && _selIndex == i);
            }
            for (int i = 0; i < _boardCards.Count; i++)
            {
                UnitInstance u = i < p.Board.Count ? p.Board[i] : null;
                _boardCards[i].BindUnit(u, _cat, _sel == SelKind.Board && _selIndex == i);
            }
            for (int i = 0; i < _handCards.Count; i++)
            {
                UnitInstance u = i < p.Hand.Count ? p.Hand[i] : null;
                _handCards[i].BindUnit(u, _cat, _sel == SelKind.Hand && _selIndex == i);
            }

            PaintInspect(p);

            var lb = new System.Text.StringBuilder();
            Pairing vs = FindPairing(p.Seat);
            if (vs != null)
            {
                int other = vs.SeatA == p.Seat ? vs.SeatB : vs.SeatA;
                PlayerState opp = _loop.State.Seats[other];
                lb.Append("vs  ").Append(opp.DisplayName)
                    .Append("  Wick ").Append(opp.Wick)
                    .Append("  D").Append(opp.Depth);
                string tags = HsUi.ChorusTags(opp, _cat);
                if (tags.Length > 0) lb.AppendLine().Append(tags);
                lb.AppendLine().AppendLine();
            }
            else if (_loop.State.GhostSeat == p.Seat)
                lb.AppendLine("vs  Ash Echo").AppendLine();

            for (int i = 0; i < _loop.State.Seats.Length; i++)
            {
                var s = _loop.State.Seats[i];
                lb.Append(s.Alive ? "● " : "○ ");
                lb.Append(s.DisplayName.PadRight(8));
                lb.Append(" W").Append(s.Wick.ToString().PadLeft(3));
                lb.Append(" D").Append(s.Depth);
                if (s.Place.HasValue) lb.Append(" #").Append(s.Place.Value);
                lb.AppendLine();
            }
            _log.text = lb.ToString();
        }

        void AdvanceHelp()
        {
            _helpStep++;
            if (_helpStep > 3)
            {
                SkipHelp();
                return;
            }
            PaintHelp();
        }

        void SkipHelp()
        {
            PlayerPrefs.SetInt("kindling.help.v1", 1);
            if (_helpPanel != null) _helpPanel.SetActive(false);
        }

        void PaintHelp()
        {
            if (_helpPanel == null || _helpText == null) return;
            if (PlayerPrefs.GetInt("kindling.help.v1", 0) == 1)
            {
                _helpPanel.SetActive(false);
                return;
            }
            _helpPanel.SetActive(true);
            switch (_helpStep)
            {
                case 0:
                    _helpText.text = "1 / 4   Pick a Captain. Timer auto-picks if you wait.";
                    break;
                case 1:
                    _helpText.text = "2 / 4   Drag a stall card into HAND to buy (3 Embers). Purchases never go straight to the board.";
                    break;
                case 2:
                    _helpText.text = "3 / 4   Drag from hand onto WARBAND to play. Spells cast from hand and never sit on the board.";
                    break;
                default:
                    _helpText.text = "4 / 4   Combat starts when the timer hits 0 (15s round 1, 60s from round 5). READY fights early.";
                    break;
            }
        }

        void PaintInspect(PlayerState p)
        {
            if (_infoLabel == null) return;
            UnitInstance u = null;
            if (_sel == SelKind.Stall && _selIndex >= 0 && _selIndex < p.Stall.Count)
                u = p.Stall[_selIndex];
            else if (_sel == SelKind.Board && _selIndex >= 0 && _selIndex < p.Board.Count)
                u = p.Board[_selIndex];
            else if (_sel == SelKind.Hand && _selIndex >= 0 && _selIndex < p.Hand.Count)
                u = p.Hand[_selIndex];
            if (u != null)
            {
                _infoLabel.text = HsUi.Inspect(_cat.GetUnit(u.CatalogId), u);
                return;
            }
            if (_loop.State.Phase == Phase.CaptainPick && p.CaptainOffers != null
                && _sel == SelKind.CaptainOffer && _selIndex >= 0 && _selIndex < p.CaptainOffers.Length)
            {
                CaptainDef cap = _cat.GetCaptain(p.CaptainOffers[_selIndex]);
                if (cap != null)
                {
                    _infoLabel.text = cap.Name + "\nWick " + cap.Wick + "\n"
                        + (cap.HasEdict ? ("Edict " + cap.EdictCost) : "Passive");
                    return;
                }
            }
            _infoLabel.text = "Tap a card for tribe and keywords.\nAuthored text comes later.";
        }

        string NameOfCaptain(string id)
        {
            var d = _cat.GetCaptain(id);
            return d != null ? d.Name : id;
        }

        Pairing FindPairing(int seat)
        {
            if (_loop.State.Pairings == null) return null;
            for (int i = 0; i < _loop.State.Pairings.Length; i++)
            {
                var pr = _loop.State.Pairings[i];
                if (pr.SeatA == seat || pr.SeatB == seat) return pr;
            }
            return null;
        }
    }
}
