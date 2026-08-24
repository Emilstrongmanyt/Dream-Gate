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
        Text _combatLog;
        GameObject _combatPanel;
        GameObject _pickPanel;
        GameObject _recruitRoot;

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
            var canvas = HsUi.Canvas("KindlingCanvas");
            HsUi.Panel(canvas.transform, "felt", Vector2.zero, Vector2.one, HsUi.Felt);

            HsUi.Label(HsUi.Panel(canvas.transform, "title", new Vector2(0.01f, 0.94f), new Vector2(0.40f, 0.995f), Color.clear),
                "t", "KINDLING  ·  The Ember Exchange", 26, TextAnchor.MiddleLeft, HsUi.Gold);

            _hud = HsUi.Label(HsUi.Panel(canvas.transform, "hud", new Vector2(0.40f, 0.94f), new Vector2(0.99f, 0.995f), Color.clear),
                "hud", "", 22, TextAnchor.MiddleRight, HsUi.Cream);

            // Stall
            var stallBar = HsUi.Panel(canvas.transform, "stallBar", new Vector2(0.02f, 0.70f), new Vector2(0.72f, 0.93f), HsUi.Wood);
            HsUi.Label(stallBar, "sl", "STALL", 16, TextAnchor.UpperLeft, HsUi.Gold);
            var stallRow = HsUi.Panel(stallBar, "row", new Vector2(0.01f, 0.02f), new Vector2(0.99f, 0.88f), Color.clear);
            var hlg = stallRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(8, 8, 4, 4);
            for (int i = 0; i < 7; i++)
            {
                int idx = i;
                var cv = CardView.Create(stallRow, new Vector2(130, 180));
                cv.OnClicked = () => Select(SelKind.Stall, idx);
                var le = cv.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 130; le.preferredHeight = 180;
                _stallCards.Add(cv);
            }

            // Board
            var boardBar = HsUi.Panel(canvas.transform, "boardBar", new Vector2(0.02f, 0.38f), new Vector2(0.72f, 0.69f), new Color(0.12f, 0.08f, 0.05f, 1));
            HsUi.Label(boardBar, "bl", "WARBAND", 16, TextAnchor.UpperLeft, HsUi.Gold);
            var boardRow = HsUi.Panel(boardBar, "row", new Vector2(0.01f, 0.02f), new Vector2(0.99f, 0.88f), Color.clear);
            var hlg2 = boardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg2.spacing = 8; hlg2.childAlignment = TextAnchor.MiddleCenter;
            hlg2.childForceExpandWidth = false; hlg2.childForceExpandHeight = true;
            for (int i = 0; i < 7; i++)
            {
                int idx = i;
                var cv = CardView.Create(boardRow, new Vector2(130, 180));
                cv.OnClicked = () => Select(SelKind.Board, idx);
                var le = cv.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 130; le.preferredHeight = 180;
                _boardCards.Add(cv);
            }

            // Hand
            var handBar = HsUi.Panel(canvas.transform, "handBar", new Vector2(0.02f, 0.14f), new Vector2(0.72f, 0.37f), HsUi.Wood);
            HsUi.Label(handBar, "hl", "HAND", 16, TextAnchor.UpperLeft, HsUi.Gold);
            var handRow = HsUi.Panel(handBar, "row", new Vector2(0.01f, 0.02f), new Vector2(0.99f, 0.88f), Color.clear);
            var hlg3 = handRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg3.spacing = 6; hlg3.childAlignment = TextAnchor.MiddleLeft;
            hlg3.childForceExpandWidth = false; hlg3.childForceExpandHeight = true;
            for (int i = 0; i < 10; i++)
            {
                int idx = i;
                var cv = CardView.Create(handRow, new Vector2(110, 150));
                cv.OnClicked = () => Select(SelKind.Hand, idx);
                var le = cv.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 110; le.preferredHeight = 150;
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
            HsUi.MakeButton(act, "buyB", "Buy Board  3", new Vector2(0.01f, 0.15f), new Vector2(0.16f, 0.85f), HsUi.GoldDark, () => Buy(DestLoc.Board));
            HsUi.MakeButton(act, "buyH", "Buy Hand  3", new Vector2(0.17f, 0.15f), new Vector2(0.32f, 0.85f), HsUi.GoldDark, () => Buy(DestLoc.Hand));
            HsUi.MakeButton(act, "sell", "Sell  +1", new Vector2(0.33f, 0.15f), new Vector2(0.44f, 0.85f), HsUi.WickRed, Sell);
            HsUi.MakeButton(act, "play", "Play", new Vector2(0.45f, 0.15f), new Vector2(0.54f, 0.85f), HsUi.ChorusColor(Chorus.Gearwights), Play);
            HsUi.MakeButton(act, "latch", "Latch", new Vector2(0.55f, 0.15f), new Vector2(0.64f, 0.85f), HsUi.ChorusColor(Chorus.Gearwights), Latch);
            HsUi.MakeButton(act, "reroll", "Reroll  1", new Vector2(0.65f, 0.15f), new Vector2(0.76f, 0.85f), HsUi.Ember, Reroll);
            HsUi.MakeButton(act, "hold", "Hold", new Vector2(0.77f, 0.15f), new Vector2(0.86f, 0.85f), HsUi.ChorusColor(Chorus.Ashbound), Hold);
            HsUi.MakeButton(act, "up", "Upgrade", new Vector2(0.87f, 0.15f), new Vector2(0.99f, 0.85f), HsUi.Gold, Upgrade);

            HsUi.MakeButton(canvas.transform, "ready", "READY", new Vector2(0.735f, 0.01f), new Vector2(0.99f, 0.13f), new Color(0.15f, 0.45f, 0.18f), Ready);

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
                var cv = CardView.Create(offerRow, new Vector2(220, 320));
                cv.OnClicked = () => PickCaptain(idx);
                var le = cv.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 220; le.preferredHeight = 320;
                _offerCards.Add(cv);
            }
            HsUi.Label(_pickPanel.transform, "hint", "Empty seats are filled with bots. Art is placeholder — import later.", 18, TextAnchor.LowerCenter, HsUi.Cream)
                .GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.22f);

            // Combat overlay
            _combatPanel = HsUi.Panel(canvas.transform, "combat", Vector2.zero, Vector2.one, new Color(0.04f, 0.02f, 0.02f, 0.94f)).gameObject;
            HsUi.Label(_combatPanel.transform, "ct", "ASH RING", 36, TextAnchor.UpperCenter, HsUi.Ember)
                .GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.88f);
            _combatLog = HsUi.Label(_combatPanel.transform, "clog", "", 20, TextAnchor.UpperLeft, HsUi.Cream);
            var clrt = _combatLog.GetComponent<RectTransform>();
            clrt.anchorMin = new Vector2(0.08f, 0.16f);
            clrt.anchorMax = new Vector2(0.92f, 0.86f);
            _combatLog.horizontalOverflow = HorizontalWrapMode.Wrap;
            HsUi.MakeButton(_combatPanel.transform, "next", "CONTINUE", new Vector2(0.35f, 0.03f), new Vector2(0.65f, 0.12f), HsUi.GoldDark, NextAfterCombat);
            _combatPanel.SetActive(false);
        }

        void Select(SelKind kind, int index)
        {
            if (_loop.State.Phase != Phase.Recruit) return;
            if (_sel == SelKind.Board && kind == SelKind.Board && _selIndex != index)
            {
                TryReorder(_selIndex, index);
                _sel = SelKind.None;
                Refresh();
                return;
            }
            if (_sel == SelKind.Hand && kind == SelKind.Board)
            {
                _selIndex = index;
                PlayTo(_selIndex);
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
                Refresh();
            }
        }

        void Buy(DestLoc dest)
        {
            var p = _loop.Human;
            if (p == null || _sel != SelKind.Stall) { Toast("Select a stall minion"); return; }
            Apply(new RecruitAction
            {
                Op = RecruitOp.Buy,
                Seat = p.Seat,
                StallIndex = _selIndex,
                Dest = dest,
                DestIndex = dest == DestLoc.Board ? p.Board.Count : p.Hand.Count
            });
            _sel = SelKind.None;
            Refresh();
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

        void Play()
        {
            var p = _loop.Human;
            if (p == null || _sel != SelKind.Hand) { Toast("Select a hand card"); return; }
            PlayTo(p.Board.Count);
        }

        void PlayTo(int dest)
        {
            var p = _loop.Human;
            if (p == null || _sel != SelKind.Hand) return;
            Apply(new RecruitAction { Op = RecruitOp.Play, Seat = p.Seat, HandIndex = _selIndex, DestIndex = dest, Dest = DestLoc.Board });
            _sel = SelKind.None;
            Refresh();
        }

        void Latch()
        {
            var p = _loop.Human;
            if (p == null) return;
            if (_sel == SelKind.Hand)
                Apply(new RecruitAction { Op = RecruitOp.Latch, Seat = p.Seat, From = DestLoc.Hand, FromIndex = _selIndex, HostIndex = 0 });
            else if (_sel == SelKind.Board)
            {
                int host = _selIndex == 0 && p.Board.Count > 1 ? 1 : 0;
                Apply(new RecruitAction { Op = RecruitOp.Latch, Seat = p.Seat, From = DestLoc.Board, FromIndex = _selIndex, HostIndex = host });
            }
            else { Toast("Select a Latch piece, then Latch onto host 0"); return; }
            _sel = SelKind.None;
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
                Apply(new RecruitAction { Op = RecruitOp.GlimpsePick, Seat = p.Seat, OfferIndex = 0 });
            }
            _loop.ResolveRecruitAndCombat();
            _showingCombat = true;
            _combatPanel.SetActive(true);
            FillCombatLog();
            Refresh();
        }

        void NextAfterCombat()
        {
            _showingCombat = false;
            _combatPanel.SetActive(false);
            if (!_loop.State.MatchOver)
                _loop.ContinueToNextRecruit();
            Refresh();
        }

        SimResult Apply(RecruitAction a)
        {
            SimResult r = _loop.Try(a);
            if (!r.Ok) Toast(r.Code ?? "illegal");
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
        }

        void FillCombatLog()
        {
            var sb = new System.Text.StringBuilder();
            var p = _loop.Human;
            CombatResult cr = _loop.LastHumanCombat;
            if (p != null)
            {
                sb.Append("You  Wick ").Append(p.Wick).Append("  Depth ").Append(p.Depth);
                if (p.Place.HasValue) sb.Append("  Place ").Append(p.Place.Value);
                sb.AppendLine();
            }
            if (cr == null)
            {
                sb.AppendLine("No combat this round (bye / ghost only).");
            }
            else
            {
                if (cr.Draw) sb.AppendLine("Draw — 0 Ring Damage");
                else sb.AppendLine((cr.WinnerSeat == _loop.HumanSeat ? "Victory" : "Defeat") + "  Ring " + cr.Damage);
                int shown = 0;
                for (int i = 0; i < cr.Events.Count && shown < 24; i++)
                {
                    CombatEvent e = cr.Events[i];
                    if (e.Op == CombatOp.AuraRefresh) continue;
                    sb.Append(e.Op);
                    if (!string.IsNullOrEmpty(e.CatalogId)) sb.Append(' ').Append(e.CatalogId);
                    if (e.Amount != 0) sb.Append("  ").Append(e.Amount);
                    sb.AppendLine();
                    shown++;
                }
            }
            if (_loop.State.MatchOver)
            {
                sb.AppendLine().AppendLine("MATCH OVER");
                for (int i = 0; i < _loop.State.Seats.Length; i++)
                {
                    var s = _loop.State.Seats[i];
                    sb.Append('#').Append(s.Place ?? 0).Append("  ").Append(s.DisplayName)
                        .Append("  Wick ").Append(s.Wick).AppendLine();
                }
            }
            _combatLog.text = sb.ToString();
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

            if (p == null) return;
            _hud.text = "Round " + _loop.State.Round
                + "   Wick " + p.Wick
                + "   Embers " + p.Embers
                + "   Depth " + p.Depth
                + "   Upgrade " + p.UpgradeCost
                + (p.Hold ? "   HOLD" : "")
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

            var lb = new System.Text.StringBuilder();
            Pairing vs = FindPairing(p.Seat);
            if (vs != null)
            {
                int other = vs.SeatA == p.Seat ? vs.SeatB : vs.SeatA;
                lb.Append("vs  ").Append(_loop.State.Seats[other].DisplayName)
                    .Append("  Wick ").Append(_loop.State.Seats[other].Wick)
                    .Append("  D").Append(_loop.State.Seats[other].Depth).AppendLine().AppendLine();
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
