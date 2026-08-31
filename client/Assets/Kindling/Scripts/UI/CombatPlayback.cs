using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Kindling.Sim;
using Kindling.Sim.Catalog;
using Kindling.Sim.Model;

namespace Kindling.Client
{
    public sealed class CombatPlayback
    {
        public GameObject Root;
        public bool Done { get; private set; }
        public float DoneAt { get; private set; }

        readonly List<CardView> _youCards = new List<CardView>();
        readonly List<CardView> _oppCards = new List<CardView>();
        readonly List<UnitInstance> _youBoard = new List<UnitInstance>();
        readonly List<UnitInstance> _oppBoard = new List<UnitInstance>();
        Text _youHdr;
        Text _oppHdr;
        Text _ticker;
        Text _result;
        Text _speedCap;
        Catalog _cat;
        CombatResult _cr;
        int _youSeat;
        int _index;
        float _nextAt;
        float _speed = 2f;
        float _capAt;
        ulong _pulseA;
        ulong _pulseB;
        System.Action _onDismiss;
        bool _matchOver;
        string _standings;

        public void Build(Transform canvas, System.Action onDismiss)
        {
            _onDismiss = onDismiss;
            Root = HsUi.Panel(canvas, "combat", Vector2.zero, Vector2.one, new Color(0.04f, 0.02f, 0.02f, 0.96f)).gameObject;
            HsUi.Band(Root.transform, "ct", "ASH RING", 28, TextAnchor.MiddleCenter, HsUi.Ember,
                new Vector2(0.10f, 0.92f), new Vector2(0.90f, 0.99f));

            _oppHdr = HsUi.Label(HsUi.Panel(Root.transform, "oh", new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.92f), Color.clear),
                "oh", "Opponent", 20, TextAnchor.MiddleLeft, HsUi.Cream);
            var oppRow = HsUi.Panel(Root.transform, "oppRow", new Vector2(0.04f, 0.62f), new Vector2(0.96f, 0.86f), new Color(0.10f, 0.05f, 0.05f, 1));
            FillRow(oppRow, _oppCards, CardZone.Board);

            _ticker = HsUi.Label(HsUi.Panel(Root.transform, "tick", new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.61f), Color.clear),
                "tick", "", 22, TextAnchor.MiddleCenter, HsUi.Selected);
            _result = HsUi.Label(HsUi.Panel(Root.transform, "res", new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.48f), Color.clear),
                "res", "", 26, TextAnchor.MiddleCenter, HsUi.Gold);

            var youRow = HsUi.Panel(Root.transform, "youRow", new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.40f), new Color(0.08f, 0.10f, 0.05f, 1));
            FillRow(youRow, _youCards, CardZone.Board);
            _youHdr = HsUi.Label(HsUi.Panel(Root.transform, "yh", new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.16f), Color.clear),
                "yh", "You", 20, TextAnchor.MiddleLeft, HsUi.Cream);

            HsUi.MakeButton(Root.transform, "skip", "SKIP", new Vector2(0.08f, 0.02f), new Vector2(0.28f, 0.11f), HsUi.WickRed, Skip);
            var speed = HsUi.MakeButton(Root.transform, "spd", "1×", new Vector2(0.40f, 0.02f), new Vector2(0.60f, 0.11f), HsUi.GoldDark, ToggleSpeed);
            _speedCap = speed.GetComponentInChildren<Text>();
            HsUi.MakeButton(Root.transform, "next", "CONTINUE", new Vector2(0.72f, 0.02f), new Vector2(0.92f, 0.11f), new Color(0.15f, 0.45f, 0.18f), Continue);
            Root.SetActive(false);
        }

        static void FillRow(RectTransform row, List<CardView> into, CardZone zone)
        {
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.padding = new RectOffset(8, 8, 6, 6);
            into.Clear();
            for (int i = 0; i < 7; i++)
            {
                var cv = CardView.Create(row, new Vector2(150, 200), zone, i);
                if (cv.Drag != null) cv.Drag.enabled = false;
                into.Add(cv);
            }
        }

        public void Begin(CombatResult cr, int youSeat, Catalog cat, bool matchOver, string standings)
        {
            _cat = cat;
            _cr = cr;
            _youSeat = youSeat;
            _matchOver = matchOver;
            _standings = standings ?? "";
            _index = 0;
            _speed = 2f;
            if (_speedCap != null) _speedCap.text = "2×";
            _pulseA = 0;
            _pulseB = 0;
            Done = false;
            DoneAt = 0;
            _youBoard.Clear();
            _oppBoard.Clear();
            Root.SetActive(true);
            _capAt = Time.unscaledTime + Rules.CombatPlaybackCapSeconds;

            if (cr == null)
            {
                _oppHdr.text = "No pairing this round";
                _youHdr.text = "You";
                _ticker.text = "No Ash Ring fight (bye).";
                _result.text = "";
                Paint();
                MarkDone();
                return;
            }

            bool youAreA = cr.SeatA == youSeat;
            string youName = youAreA ? cr.NameA : cr.NameB;
            string oppName = youAreA ? cr.NameB : cr.NameA;
            int youW = youAreA ? cr.WickA : cr.WickB;
            int oppW = youAreA ? cr.WickB : cr.WickA;
            int youD = youAreA ? cr.DepthA : cr.DepthB;
            int oppD = youAreA ? cr.DepthB : cr.DepthA;
            if (string.IsNullOrEmpty(oppName)) oppName = cr.SeatB < 0 || cr.SeatA < 0 ? "Ash Echo" : "Opponent";
            _youHdr.text = (string.IsNullOrEmpty(youName) ? "You" : youName) + "   Wick " + youW + "   D" + youD;
            _oppHdr.text = oppName + "   Wick " + (oppW > 0 ? oppW.ToString() : "—") + "   D" + oppD;

            List<CombatPiece> youSnap = youAreA ? cr.BoardA : cr.BoardB;
            List<CombatPiece> oppSnap = youAreA ? cr.BoardB : cr.BoardA;
            CopySnap(youSnap, _youBoard);
            CopySnap(oppSnap, _oppBoard);
            _ticker.text = "Kindle…";
            _result.text = "";
            Paint();
            _nextAt = Time.unscaledTime + 0.35f;
        }

        static void CopySnap(List<CombatPiece> src, List<UnitInstance> dst)
        {
            dst.Clear();
            if (src == null) return;
            for (int i = 0; i < src.Count; i++)
                dst.Add(src[i].ToUnit());
        }

        public void Tick()
        {
            if (!Root.activeSelf || Done) return;
            if (!Done && Time.unscaledTime >= _capAt)
            {
                Skip();
                return;
            }
            if (_cr == null) return;
            if (Time.unscaledTime < _nextAt) return;
            Advance();
        }

        public void Skip()
        {
            if (Done) return;
            if (_cr == null)
            {
                MarkDone();
                return;
            }
            while (!Done)
                StepOnce(silent: true);
            _pulseA = 0;
            _pulseB = 0;
            ShowOutcome();
            Paint();
        }

        void MarkDone()
        {
            Done = true;
            DoneAt = Time.unscaledTime;
        }

        void Continue()
        {
            if (!Done) Skip();
            else _onDismiss?.Invoke();
        }

        void ToggleSpeed()
        {
            if (_speed < 1.5f) _speed = 2f;
            else if (_speed < 3f) _speed = 4f;
            else _speed = 1f;
            if (_speedCap != null) _speedCap.text = _speed <= 1.1f ? "1×" : (_speed < 3f ? "2×" : "4×");
        }

        void Advance()
        {
            if (_cr.Events == null || _index >= _cr.Events.Count)
            {
                ShowOutcome();
                Paint();
                return;
            }
            CombatEvent e = _cr.Events[_index++];
            if (e.Op == CombatOp.AuraRefresh || e.Op == CombatOp.GlimpseEmpty || e.Op == CombatOp.HandFull)
            {
                _nextAt = Time.unscaledTime;
                return;
            }
            StepEvent(e);
            Paint();
            float d = Delay(e.Op) / _speed;
            _nextAt = Time.unscaledTime + d;
            if (_index >= _cr.Events.Count)
                ShowOutcome();
        }

        void StepOnce(bool silent)
        {
            if (_cr.Events == null || _index >= _cr.Events.Count)
            {
                MarkDone();
                return;
            }
            CombatEvent e = _cr.Events[_index++];
            if (e.Op == CombatOp.AuraRefresh) return;
            StepEvent(e);
            if (_index >= _cr.Events.Count) MarkDone();
        }

        void StepEvent(CombatEvent e)
        {
            _pulseA = e.SrcInstance;
            _pulseB = e.DstInstance;
            switch (e.Op)
            {
                case CombatOp.Attack:
                    _ticker.text = NameOf(e.CatalogId) + " strikes";
                    Touch(e.SrcInstance, u => { if (e.Atk > 0) u.Atk = e.Atk; });
                    Burst("slash", e.SrcInstance);
                    break;
                case CombatOp.Damage:
                    _ticker.text = NameOf(e.CatalogId) + "  −" + e.Amount;
                    Touch(e.DstInstance, u => u.Hp = e.HpAfter);
                    Burst("hit", e.DstInstance);
                    break;
                case CombatOp.Venom:
                    _ticker.text = "Venom";
                    Touch(e.DstInstance, u => u.Hp = 0);
                    Burst("venom", e.DstInstance);
                    break;
                case CombatOp.AegisBreak:
                    _ticker.text = "Aegis breaks";
                    Touch(e.DstInstance, u => u.RemoveKeyword(Keyword.Aegis));
                    Burst("spark", e.DstInstance);
                    break;
                case CombatOp.Death:
                    _ticker.text = NameOf(e.CatalogId) + " falls";
                    Burst("poof", e.SrcInstance);
                    Remove(e.SrcInstance);
                    break;
                case CombatOp.Summon:
                    _ticker.text = NameOf(e.CatalogId) + " arrives";
                    Insert(e.SrcSeat, e.SrcSlot, PieceFrom(e));
                    Burst("smoke", e.SrcInstance);
                    break;
                case CombatOp.Afterglow:
                    _ticker.text = NameOf(e.CatalogId) + " Afterglow";
                    Insert(e.SrcSeat, e.SrcSlot, PieceFrom(e));
                    Burst("fire", e.SrcInstance);
                    break;
                case CombatOp.Echo:
                    _ticker.text = NameOf(e.CatalogId) + " Echo";
                    break;
                case CombatOp.Kindle:
                case CombatOp.KindleStart:
                    _ticker.text = e.Op == CombatOp.KindleStart ? "Kindle" : (NameOf(e.CatalogId) + " Kindles");
                    Burst("fire", e.SrcInstance);
                    break;
                case CombatOp.CombatEnd:
                    ShowOutcome();
                    break;
                case CombatOp.BoardFull:
                    _ticker.text = "Board full";
                    break;
                default:
                    if (!string.IsNullOrEmpty(e.CatalogId))
                        _ticker.text = e.Op + "  " + NameOf(e.CatalogId);
                    else
                        _ticker.text = e.Op.ToString();
                    break;
            }
        }

        UnitInstance PieceFrom(CombatEvent e)
        {
            int hp = e.HpAfter > 0 ? e.HpAfter : 1;
            int atk = e.Atk > 0 ? e.Atk : 1;
            return new UnitInstance
            {
                InstanceId = e.SrcInstance,
                CatalogId = new UnitId(e.CatalogId),
                Atk = atk,
                Hp = hp,
                MaxHp = hp,
                CombatSeat = e.SrcSeat,
                AttackCharges = 1
            };
        }

        void Insert(int seat, int slot, UnitInstance u)
        {
            List<UnitInstance> board = BoardOf(seat);
            if (slot < 0 || slot > board.Count) slot = board.Count;
            board.Insert(slot, u);
            while (board.Count > 7)
                board.RemoveAt(board.Count - 1);
        }

        void Remove(ulong id)
        {
            if (Pull(_youBoard, id) || Pull(_oppBoard, id)) return;
        }

        static bool Pull(List<UnitInstance> board, ulong id)
        {
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].InstanceId == id)
                {
                    board.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        void Touch(ulong id, System.Action<UnitInstance> fn)
        {
            UnitInstance u = Find(id);
            if (u != null) fn(u);
        }

        UnitInstance Find(ulong id)
        {
            if (id == 0) return null;
            for (int i = 0; i < _youBoard.Count; i++)
                if (_youBoard[i].InstanceId == id) return _youBoard[i];
            for (int i = 0; i < _oppBoard.Count; i++)
                if (_oppBoard[i].InstanceId == id) return _oppBoard[i];
            return null;
        }

        List<UnitInstance> BoardOf(int seat)
        {
            if (seat == _youSeat) return _youBoard;
            return _oppBoard;
        }

        string NameOf(string id)
        {
            if (string.IsNullOrEmpty(id) || _cat == null) return id ?? "";
            UnitDef d = _cat.GetUnit(id);
            return d != null ? d.Name : id;
        }

        void ShowOutcome()
        {
            if (_cr == null)
            {
                _result.text = "";
                return;
            }
            string line;
            if (_cr.Draw) line = "Draw  ·  0 Ring";
            else if (_cr.WinnerSeat == _youSeat) line = "Victory  ·  Ring " + _cr.Damage;
            else line = "Defeat  ·  Ring " + _cr.Damage;
            if (_matchOver) line += "\nMATCH OVER";
            _result.text = line;
            if (_cr.Draw) BannerFx.Show("ActionText_Nice", 1.2f);
            else if (_cr.WinnerSeat == _youSeat) BannerFx.Show(_matchOver ? "ActionText_Victory" : "ActionText_Win", 1.4f);
            else BannerFx.Show(_matchOver ? "ActionText_Defeat" : "ActionText_Lose", 1.4f);
            if (_matchOver && !string.IsNullOrEmpty(_standings))
                _ticker.text = _standings;
            else
                _ticker.text = line;
            MarkDone();
        }

        static float Delay(CombatOp op)
        {
            switch (op)
            {
                case CombatOp.Attack: return 0.55f;
                case CombatOp.Death:
                case CombatOp.Summon:
                case CombatOp.Afterglow: return 0.42f;
                case CombatOp.CombatEnd: return 0.70f;
                case CombatOp.KindleStart: return 0.35f;
                default: return 0.28f;
            }
        }

        void Burst(string key, ulong instanceId)
        {
            var rt = RectOf(instanceId);
            if (rt != null) VfxPlayer.Play(key, rt);
        }

        RectTransform RectOf(ulong instanceId)
        {
            if (instanceId == 0) return null;
            for (int i = 0; i < _youCards.Count; i++)
            {
                if (_youCards[i] != null && _youCards[i].Unit != null && _youCards[i].Unit.InstanceId == instanceId)
                    return _youCards[i].GetComponent<RectTransform>();
            }
            for (int i = 0; i < _oppCards.Count; i++)
            {
                if (_oppCards[i] != null && _oppCards[i].Unit != null && _oppCards[i].Unit.InstanceId == instanceId)
                    return _oppCards[i].GetComponent<RectTransform>();
            }
            return null;
        }

        void Paint()
        {
            PaintRow(_youCards, _youBoard);
            PaintRow(_oppCards, _oppBoard);
        }

        void PaintRow(List<CardView> cards, List<UnitInstance> board)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                UnitInstance u = i < board.Count ? board[i] : null;
                bool pulse = u != null && (u.InstanceId == _pulseA || u.InstanceId == _pulseB);
                cards[i].BindUnit(u, _cat, pulse);
                if (cards[i].Drag != null) cards[i].Drag.enabled = false;
            }
        }
    }
}
