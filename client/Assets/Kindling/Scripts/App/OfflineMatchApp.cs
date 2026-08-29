using System;
using System.Collections;
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
        RectTransform _offerRow;
        Text _hud;
        Text _log;
        Text _toastLabel;
        CombatPlayback _playback;
        GameObject _pickPanel;
        Text _infoLabel;
        GameObject _recruitRoot;
        Canvas _canvas;
        RectTransform _safe;
        DropZone _handZone;
        DropZone _boardZone;
        DropZone _stallZone;
        CardDrag _activeDrag;
        Transform _dragOriginParent;
        int _dragOriginSibling;
        Text _timerLabel;
        Image _wickFill;
        Image _emberFill;
        Text _capRailName;
        Button _edictBtn;
        GameObject _glimpsePanel;
        readonly List<CardView> _glimpseCards = new List<CardView>();
        float _timerEnd;
        bool _timerArmed;
        bool _edictTargeting;
        int _awakenSeen;
        bool _lowTimeWarned;
        int _netCombatSeq;
        int _netRound;
        bool _netReadySent;
        CombatResult _netCombat;
        GameObject _helpPanel;
        Text _helpText;
        int _helpStep;
        NetMatchClient _net;
        GameObject _menuRoot;
        GameObject _authPanel;
        GameObject _hubPanel;
        GameObject _settingsPanel;
        InputField _nameInput;
        InputField _passInput;
        InputField _hostInput;
        Text _hubWelcome;
        Text _menuStatus;
        string _authToken;
        string _displayName;
        string _accountId;
        int _mmr;
        bool _inMatch;
        GameObject _crownPanel;
        Text _crownText;
        Text _historyLabel;
        string _frameId = "gold";
        string _matchMode = "practice";

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
            BuildUi();
            BuildMenu();
            if (_pickPanel != null) _pickPanel.SetActive(false);
            if (_glimpsePanel != null) _glimpsePanel.SetActive(false);
            if (_helpPanel != null) _helpPanel.SetActive(false);
            if (_playback != null && _playback.Root != null) _playback.Root.SetActive(false);
            RestoreSession();
            ShowMenu();
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
            HsUi.Panel(_canvas.transform, "felt", Vector2.zero, Vector2.one, HsUi.Felt);
            _safe = HsUi.SafeRoot(_canvas.transform);
            var canvas = _safe;

            HsUi.Band(canvas, "t", "KINDLING", 22, TextAnchor.MiddleLeft, HsUi.Gold,
                new Vector2(0.01f, 0.94f), new Vector2(0.20f, 0.995f));
            _hud = HsUi.Band(canvas, "hud", "", 20, TextAnchor.MiddleCenter, HsUi.Cream,
                new Vector2(0.21f, 0.94f), new Vector2(0.84f, 0.995f));
            HsUi.MakeButton(canvas, "leave", "MENU", new Vector2(0.86f, 0.94f), new Vector2(0.99f, 0.995f), HsUi.WickRed, LeaveMatch);

            var stallBar = HsUi.Panel(canvas, "stallBar", new Vector2(0.01f, 0.70f), new Vector2(0.73f, 0.93f), HsUi.Wood);
            HsUi.Band(stallBar, "sl", "STALL", 14, TextAnchor.MiddleLeft, HsUi.Gold,
                new Vector2(0.02f, 0.86f), new Vector2(0.40f, 0.98f));
            _stallZone = stallBar.gameObject.AddComponent<DropZone>();
            _stallZone.Init(CardZone.Stall, stallBar.GetComponent<Image>());
            var stallRow = HsUi.Panel(stallBar, "row", new Vector2(0.01f, 0.02f), new Vector2(0.99f, 0.84f), Color.clear);
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
            HsUi.Band(boardBar, "bl", "WARBAND", 14, TextAnchor.MiddleLeft, HsUi.Gold,
                new Vector2(0.02f, 0.86f), new Vector2(0.50f, 0.98f));
            _boardZone = boardBar.gameObject.AddComponent<DropZone>();
            _boardZone.Init(CardZone.Board, boardBar.GetComponent<Image>());
            var boardRow = HsUi.Panel(boardBar, "row", new Vector2(0.01f, 0.02f), new Vector2(0.99f, 0.84f), Color.clear);
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
            HsUi.Band(handBar, "hl", "HAND", 14, TextAnchor.MiddleLeft, HsUi.Gold,
                new Vector2(0.02f, 0.86f), new Vector2(0.40f, 0.98f));
            _handZone = handBar.gameObject.AddComponent<DropZone>();
            _handZone.Init(CardZone.Hand, handBar.GetComponent<Image>());
            var handRow = HsUi.Panel(handBar, "row", new Vector2(0.01f, 0.02f), new Vector2(0.99f, 0.84f), Color.clear);
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

            var capRail = HsUi.Panel(canvas, "capRail", new Vector2(0.735f, 0.62f), new Vector2(0.99f, 0.93f), HsUi.Wood);
            var portrait = HsUi.Panel(capRail, "port", new Vector2(0.22f, 0.46f), new Vector2(0.78f, 0.92f), HsUi.Felt);
            StoneTheme.Skin(portrait.GetComponent<Image>(), "ProfileFrame_137_Bg", false);
            _capRailName = HsUi.Band(capRail, "cn", "", 14, TextAnchor.MiddleCenter, HsUi.Cream,
                new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.45f));
            _wickFill = MakeFillBar(capRail, "wick", new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.28f), "Slider_Basic01_Fill_Orange");
            _emberFill = MakeFillBar(capRail, "ember", new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.14f), "Slider_Basic01_Fill_White");

            var lb = HsUi.Panel(canvas, "lb", new Vector2(0.735f, 0.38f), new Vector2(0.99f, 0.61f), HsUi.Wood);
            HsUi.Band(lb, "lbt", "TABLE", 14, TextAnchor.MiddleCenter, HsUi.Gold,
                new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.98f));
            _log = HsUi.Band(lb, "log", "", 13, TextAnchor.UpperLeft, HsUi.Cream,
                new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.86f));
            _log.resizeTextForBestFit = false;
            lb.gameObject.AddComponent<RectMask2D>();

            var act = HsUi.Panel(canvas.transform, "act", new Vector2(0.02f, 0.01f), new Vector2(0.72f, 0.15f), HsUi.Wood);
            _edictBtn = HsUi.MakeButton(act, "edict", "Edict", new Vector2(0.02f, 0.12f), new Vector2(0.22f, 0.88f), HsUi.ChorusColor(Chorus.Spirit), Edict);
            HsUi.MakeButton(act, "reroll", "Reroll  1", new Vector2(0.24f, 0.12f), new Vector2(0.44f, 0.88f), HsUi.Ember, Reroll);
            HsUi.MakeButton(act, "hold", "Hold", new Vector2(0.46f, 0.12f), new Vector2(0.66f, 0.88f), HsUi.ChorusColor(Chorus.Humanoid), Hold);
            HsUi.MakeButton(act, "up", "Upgrade", new Vector2(0.68f, 0.12f), new Vector2(0.98f, 0.88f), HsUi.Gold, Upgrade);

            HsUi.MakeButton(canvas.transform, "ready", "READY", new Vector2(0.735f, 0.01f), new Vector2(0.99f, 0.13f), new Color(0.15f, 0.45f, 0.18f), Ready);
            _timerLabel = HsUi.Band(HsUi.Panel(canvas, "timer", new Vector2(0.735f, 0.14f), new Vector2(0.99f, 0.20f), Color.clear),
                "timer", "COMBAT IN 0:15", 20, TextAnchor.MiddleCenter, HsUi.Gold, Vector2.zero, Vector2.one);
            var infoPanel = HsUi.Panel(canvas, "info", new Vector2(0.735f, 0.21f), new Vector2(0.99f, 0.37f), HsUi.Wood);
            _infoLabel = HsUi.Band(infoPanel, "info", "Tap a Kindled.", 14, TextAnchor.UpperLeft, HsUi.Cream,
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
            infoPanel.gameObject.AddComponent<RectMask2D>();

            var toastRt = HsUi.Panel(canvas.transform, "toast", new Vector2(0.25f, 0.48f), new Vector2(0.75f, 0.56f), new Color(0, 0, 0, 0.0f));
            toastRt.GetComponent<Image>().raycastTarget = false;
            _toastLabel = HsUi.Label(toastRt, "toast", "", 24, TextAnchor.MiddleCenter, HsUi.Selected);
            BannerFx.Build(canvas);

            _recruitRoot = canvas.gameObject;

            // Captain pick overlay — 4 unique-claim offers every match
            _pickPanel = HsUi.Panel(canvas, "pick", Vector2.zero, Vector2.one, new Color(0.05f, 0.03f, 0.02f, 0.92f)).gameObject;
            HsUi.Band(_pickPanel.transform, "pt", "Choose your Captain", 34, TextAnchor.MiddleCenter, HsUi.Gold,
                new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.98f));
            HsUi.Band(_pickPanel.transform, "ps", "Passive is always on. Edict costs Embers once per recruit.", 16, TextAnchor.MiddleCenter, HsUi.Cream,
                new Vector2(0.08f, 0.80f), new Vector2(0.92f, 0.88f));
            _offerRow = HsUi.Panel(_pickPanel.transform, "offers", new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.78f), Color.clear);
            var grid = _offerRow.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.cellSize = new Vector2(180, 250);
            grid.spacing = new Vector2(10, 10);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.padding = new RectOffset(8, 8, 4, 4);
            EnsureOfferCards(3);
            HsUi.Band(_pickPanel.transform, "hint", "Four offers. No two Captains in the lobby can match.", 16, TextAnchor.MiddleCenter, HsUi.Cream,
                new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.12f));

            _playback = new CombatPlayback();
            _playback.Build(canvas.transform, NextAfterCombat);

            _glimpsePanel = HsUi.Panel(canvas, "glimpse", Vector2.zero, Vector2.one, new Color(0.06f, 0.03f, 0.08f, 0.93f)).gameObject;
            HsUi.Band(_glimpsePanel.transform, "gt", "GLIMPSE  ·  choose one", 30, TextAnchor.MiddleCenter, HsUi.Gold,
                new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.97f));
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
            HsUi.Band(_glimpsePanel.transform, "gh", "Timer does not pause.", 16, TextAnchor.MiddleCenter, HsUi.Cream,
                new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.18f));
            _glimpsePanel.SetActive(false);

            _helpPanel = HsUi.Panel(canvas, "help", new Vector2(0.18f, 0.38f), new Vector2(0.82f, 0.64f), new Color(0.08f, 0.05f, 0.02f, 0.94f)).gameObject;
            _helpText = HsUi.Band(_helpPanel.transform, "ht", "", 18, TextAnchor.UpperLeft, HsUi.Cream,
                new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.94f));
            HsUi.MakeButton(_helpPanel.transform, "next", "GOT IT", new Vector2(0.55f, 0.06f), new Vector2(0.96f, 0.26f), HsUi.GoldDark, AdvanceHelp);
            HsUi.MakeButton(_helpPanel.transform, "skip", "SKIP", new Vector2(0.04f, 0.06f), new Vector2(0.48f, 0.26f), HsUi.WickRed, SkipHelp);
            _helpPanel.SetActive(false);

            _crownPanel = HsUi.Panel(canvas, "crown", Vector2.zero, Vector2.one, new Color(0.05f, 0.03f, 0.02f, 0.96f)).gameObject;
            HsUi.Band(_crownPanel.transform, "ct", "THE CROWN", 36, TextAnchor.MiddleCenter, HsUi.Gold,
                new Vector2(0.10f, 0.84f), new Vector2(0.90f, 0.96f));
            _crownText = HsUi.Band(_crownPanel.transform, "cs", "", 20, TextAnchor.UpperCenter, HsUi.Cream,
                new Vector2(0.12f, 0.22f), new Vector2(0.88f, 0.80f));
            HsUi.MakeButton(_crownPanel.transform, "ok", "BACK TO MENU", new Vector2(0.35f, 0.06f), new Vector2(0.65f, 0.16f), HsUi.GoldDark, DismissCrown);
            _crownPanel.SetActive(false);
        }

        void EnsureOfferCards(int n)
        {
            if (_offerRow == null) return;
            if (n < 1) n = 1;
            while (_offerCards.Count < n)
            {
                int idx = _offerCards.Count;
                var cv = CardView.Create(_offerRow, new Vector2(180, 250), CardZone.Offer, idx);
                cv.OnClicked = () => PickCaptain(cv.Drag != null ? cv.Drag.Index : idx);
                _offerCards.Add(cv);
            }
            for (int i = 0; i < _offerCards.Count; i++)
            {
                bool on = i < n;
                _offerCards[i].gameObject.SetActive(on);
                if (on && _offerCards[i].Drag != null)
                    _offerCards[i].Drag.Index = i;
            }
        }

        void LayoutPickGrid(int n)
        {
            if (_offerRow == null) return;
            var grid = _offerRow.GetComponent<GridLayoutGroup>();
            if (grid == null) return;
            if (n <= 3)
            {
                grid.constraintCount = n < 1 ? 1 : n;
                grid.cellSize = new Vector2(240, 340);
                grid.spacing = new Vector2(18, 12);
            }
            else if (n <= 8)
            {
                grid.constraintCount = 4;
                grid.cellSize = new Vector2(200, 280);
                grid.spacing = new Vector2(12, 10);
            }
            else
            {
                grid.constraintCount = 4;
                grid.cellSize = new Vector2(170, 240);
                grid.spacing = new Vector2(8, 8);
            }
        }

        void BuildMenu()
        {
            Transform root = _safe != null ? _safe : _canvas.transform;
            _menuRoot = HsUi.Panel(root, "menu", Vector2.zero, Vector2.one, new Color(0.08f, 0.04f, 0.02f, 0.98f)).gameObject;
            HsUi.Band(_menuRoot.transform, "mt", "KINDLING", 48, TextAnchor.MiddleCenter, HsUi.Gold,
                new Vector2(0.18f, 0.86f), new Vector2(0.82f, 0.97f));
            HsUi.Band(_menuRoot.transform, "ms", "The Ember Exchange", 22, TextAnchor.MiddleCenter, HsUi.Cream,
                new Vector2(0.18f, 0.79f), new Vector2(0.82f, 0.86f));
            HsUi.Band(_menuRoot.transform, "msub", "Sign in with a username, then Practice or Queue.", 16, TextAnchor.MiddleCenter, HsUi.Gold,
                new Vector2(0.18f, 0.73f), new Vector2(0.82f, 0.79f));

            var card = HsUi.Panel(_menuRoot.transform, "card", new Vector2(0.33f, 0.12f), new Vector2(0.77f, 0.71f), HsUi.Wood);

            _authPanel = HsUi.Panel(card, "auth", Vector2.zero, Vector2.one, Color.clear).gameObject;
            _nameInput = HsUi.MakeInput(_authPanel.transform, "name", new Vector2(0.10f, 0.70f), new Vector2(0.90f, 0.86f), "Username", false, 16);
            _passInput = HsUi.MakeInput(_authPanel.transform, "pass", new Vector2(0.10f, 0.50f), new Vector2(0.90f, 0.66f), "Password", true, 64);
            HsUi.MakeButton(_authPanel.transform, "reg", "REGISTER", new Vector2(0.10f, 0.28f), new Vector2(0.48f, 0.44f), HsUi.GoldDark, () => StartCoroutine(DoRegister()));
            HsUi.MakeButton(_authPanel.transform, "login", "LOG IN", new Vector2(0.52f, 0.28f), new Vector2(0.90f, 0.44f), new Color(0.15f, 0.45f, 0.18f), () => StartCoroutine(DoLogin()));
            HsUi.Band(_authPanel.transform, "ah", "Username 3–16 letters  ·  password 6+", 14, TextAnchor.MiddleCenter, HsUi.Cream,
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.22f));

            _hubPanel = HsUi.Panel(card, "hub", Vector2.zero, Vector2.one, Color.clear).gameObject;
            _hubWelcome = HsUi.Band(_hubPanel.transform, "hw", "Welcome", 22, TextAnchor.MiddleCenter, HsUi.Gold,
                new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.96f));
            HsUi.MakeButton(_hubPanel.transform, "prac", "PRACTICE  ·  vs bots", new Vector2(0.10f, 0.54f), new Vector2(0.90f, 0.72f), new Color(0.15f, 0.45f, 0.18f), StartPractice);
            HsUi.MakeButton(_hubPanel.transform, "queue", "QUEUE  ·  Casual", new Vector2(0.10f, 0.34f), new Vector2(0.90f, 0.52f), HsUi.GoldDark, () => StartCoroutine(StartQueue()));
            HsUi.MakeButton(_hubPanel.transform, "set", "SETTINGS", new Vector2(0.10f, 0.14f), new Vector2(0.48f, 0.30f), HsUi.GoldDark, ToggleSettings);
            HsUi.MakeButton(_hubPanel.transform, "out", "LOG OUT", new Vector2(0.52f, 0.14f), new Vector2(0.90f, 0.30f), HsUi.WickRed, Logout);

            _settingsPanel = HsUi.Panel(_menuRoot.transform, "settings", new Vector2(0.30f, 0.14f), new Vector2(0.70f, 0.78f), HsUi.Wood).gameObject;
            HsUi.Band(_settingsPanel.transform, "stt", "SETTINGS", 26, TextAnchor.MiddleCenter, HsUi.Gold,
                new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.97f));
            _hostInput = HsUi.MakeInput(_settingsPanel.transform, "host", new Vector2(0.10f, 0.68f), new Vector2(0.90f, 0.82f), "Match host (optional)", false, 128);
            HsUi.MakeButton(_settingsPanel.transform, "frame", "CARD FRAME", new Vector2(0.10f, 0.48f), new Vector2(0.90f, 0.62f), HsUi.GoldDark, CycleFrame);
            HsUi.MakeButton(_settingsPanel.transform, "a11y", "PATTERNS", new Vector2(0.10f, 0.32f), new Vector2(0.90f, 0.46f), HsUi.ChorusColor(Chorus.Spirit), TogglePatterns);
            HsUi.MakeButton(_settingsPanel.transform, "close", "CLOSE", new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.26f), HsUi.GoldDark, ToggleSettings);
            _settingsPanel.SetActive(false);

            var hist = HsUi.Panel(_menuRoot.transform, "history", new Vector2(0.02f, 0.12f), new Vector2(0.30f, 0.71f), HsUi.Wood);
            hist.gameObject.AddComponent<RectMask2D>();
            HsUi.Band(hist, "ht", "RECENT MATCHES", 15, TextAnchor.MiddleCenter, HsUi.Gold,
                new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.97f));
            _historyLabel = HsUi.Band(hist, "hb", "", 14, TextAnchor.UpperLeft, HsUi.Cream,
                new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.84f));
            _historyLabel.resizeTextForBestFit = false;
            _historyLabel.verticalOverflow = VerticalWrapMode.Truncate;

            _menuStatus = HsUi.Band(_menuRoot.transform, "st", "", 16, TextAnchor.MiddleCenter, HsUi.Selected,
                new Vector2(0.10f, 0.02f), new Vector2(0.90f, 0.10f));
            _hubPanel.SetActive(false);
        }

        void ShowMenu()
        {
            _inMatch = false;
            _timerArmed = false;
            if (_menuRoot != null)
            {
                _menuRoot.SetActive(true);
                _menuRoot.transform.SetAsLastSibling();
            }
            bool logged = !string.IsNullOrEmpty(_authToken) && !string.IsNullOrEmpty(_displayName);
            if (_authPanel != null) _authPanel.SetActive(!logged);
            if (_hubPanel != null) _hubPanel.SetActive(logged);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            PaintHub();
        }

        void PaintHub()
        {
            if (_hubWelcome != null)
            {
                _hubWelcome.text = string.IsNullOrEmpty(_displayName)
                    ? "Welcome"
                    : ("Welcome, " + _displayName + "\nWick rating  " + (_mmr > 0 ? _mmr : 1500));
            }
            if (_hostInput != null && string.IsNullOrEmpty(_hostInput.text))
                _hostInput.text = NetMatchClient.ResolveHost() ?? "";
            if (_historyLabel != null) _historyLabel.text = LocalHistoryText();
            ApplyFramePrefs();
        }

        void ToggleSettings()
        {
            if (_settingsPanel == null) return;
            bool on = !_settingsPanel.activeSelf;
            _settingsPanel.SetActive(on);
            if (on)
            {
                _settingsPanel.transform.SetAsLastSibling();
                PaintHub();
            }
        }

        void CycleFrame()
        {
            _frameId = Cosmetics.NextFrame(_frameId);
            PlayerPrefs.SetString("kindling.frame", _frameId);
            PlayerPrefs.Save();
            ApplyFramePrefs();
            MenuMsg("Card frame  " + _frameId);
        }

        void TogglePatterns()
        {
            bool on = PlayerPrefs.GetInt("kindling.a11y.patterns", 0) != 1;
            PlayerPrefs.SetInt("kindling.a11y.patterns", on ? 1 : 0);
            PlayerPrefs.Save();
            HsUi.ForcePatterns = on;
            MenuMsg(on ? "Chorus patterns on" : "Chorus patterns default");
        }

        void ApplyFramePrefs()
        {
            if (string.IsNullOrEmpty(_frameId))
                _frameId = PlayerPrefs.GetString("kindling.frame", "gold");
            HsUi.FrameColor = HsUi.CosmeticFrame(_frameId);
            HsUi.ForcePatterns = PlayerPrefs.GetInt("kindling.a11y.patterns", 0) == 1;
        }

        static string LocalHistoryText()
        {
            string raw = PlayerPrefs.GetString("kindling.history.v1", "");
            if (string.IsNullOrEmpty(raw)) return "Play a match to fill this list.";
            var items = Protocol.ExtractObjects("{\"h\":" + (raw.StartsWith("[") ? raw : ("[" + raw + "]")) + "}", "h");
            if (items.Count == 0) return "Play a match to fill this list.";
            var sb = new System.Text.StringBuilder();
            int n = items.Count < 6 ? items.Count : 6;
            for (int i = 0; i < n; i++)
            {
                int place = Protocol.ReadInt(items[i], "place");
                string mode = Protocol.ReadString(items[i], "mode");
                string cap = Protocol.ReadString(items[i], "captain");
                sb.Append('#').Append(place > 0 ? place.ToString() : "-");
                sb.Append("  ").Append(string.IsNullOrEmpty(mode) ? "match" : mode);
                if (!string.IsNullOrEmpty(cap))
                    sb.Append("  ").Append(cap.Length > 14 ? cap.Substring(0, 14) : cap);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        void RecordHistory()
        {
            if (_loop == null || _loop.Human == null) return;
            int place = _loop.Human.Place ?? 0;
            string cap = _loop.Human.Captain.IsEmpty ? "" : NameOfCaptain(_loop.Human.Captain.Value);
            string item = "{\"place\":" + place + ",\"mode\":\"" + JsonEsc(_matchMode)
                + "\",\"captain\":\"" + JsonEsc(cap) + "\"}";
            string raw = PlayerPrefs.GetString("kindling.history.v1", "[]");
            if (raw.Length < 2 || raw[0] != '[') raw = "[]";
            string next = "[" + item + (raw == "[]" ? "" : "," + raw.Substring(1, raw.Length - 2)) + "]";
            if (next.Length > 2000) next = next.Substring(0, 2000);
            PlayerPrefs.SetString("kindling.history.v1", next);
            if (place > 0) _mmr = Mathf.Max(100, _mmr + (place == 1 ? 25 : (place <= 4 ? 8 : -12)));
            PlayerPrefs.SetInt("kindling.auth.mmr", _mmr);
            PlayerPrefs.Save();
        }

        void ShowCrown()
        {
            RecordHistory();
            if (_crownPanel == null)
            {
                ReturnToMenu();
                return;
            }
            _crownPanel.SetActive(true);
            _crownPanel.transform.SetAsLastSibling();
            if (_crownText != null)
                _crownText.text = StandingsLine().Replace("   ", "\n");
        }

        void DismissCrown()
        {
            if (_crownPanel != null) _crownPanel.SetActive(false);
            ReturnToMenu();
        }

        void MenuMsg(string msg)
        {
            if (_menuStatus != null) _menuStatus.text = msg ?? "";
            Toast(msg);
        }

        void RestoreSession()
        {
            _authToken = PlayerPrefs.GetString("kindling.auth.token", "");
            _displayName = PlayerPrefs.GetString("kindling.auth.name", "");
            _accountId = PlayerPrefs.GetString("kindling.auth.id", "");
            _mmr = PlayerPrefs.GetInt("kindling.auth.mmr", 1500);
            _frameId = PlayerPrefs.GetString("kindling.frame", "gold");
            ApplyFramePrefs();
            if (string.IsNullOrEmpty(_authToken) || string.IsNullOrEmpty(_displayName))
            {
                _authToken = "";
                _displayName = "";
            }
        }

        void SaveSession()
        {
            PlayerPrefs.SetString("kindling.auth.token", _authToken ?? "");
            PlayerPrefs.SetString("kindling.auth.name", _displayName ?? "");
            PlayerPrefs.SetString("kindling.auth.id", _accountId ?? "");
            PlayerPrefs.SetInt("kindling.auth.mmr", _mmr);
            PlayerPrefs.Save();
        }

        void ApplyAccountJson(string token, string account)
        {
            _authToken = token ?? "";
            _displayName = Protocol.ReadString(account, "displayName");
            _accountId = Protocol.ReadString(account, "id");
            _mmr = Protocol.ReadInt(account, "mmr");
            if (_mmr < 1) _mmr = 1500;
            SaveSession();
            ShowMenu();
        }

        IEnumerator DoRegister()
        {
            yield return SubmitAuth("/v1/auth/register", true);
        }

        IEnumerator DoLogin()
        {
            yield return SubmitAuth("/v1/auth/login", false);
        }

        IEnumerator SubmitAuth(string path, bool register)
        {
            string name = _nameInput != null ? _nameInput.text : "";
            string pass = _passInput != null ? _passInput.text : "";
            string bad = AccountAuth.ValidateName(name);
            if (bad != null) { MenuMsg(AuthHint(bad)); yield break; }
            bad = AccountAuth.ValidatePassword(pass);
            if (bad != null) { MenuMsg(AuthHint(bad)); yield break; }

            string host = CurrentHost();
            if (string.IsNullOrEmpty(host))
            {
                if (TryLocalAuth(name, pass, register, out string err))
                    MenuMsg(register ? "Registered — local practice" : "Logged in");
                else
                    MenuMsg(AuthHint(err));
                yield break;
            }

            EnsureNet(host);
            string body = "{\"displayName\":\"" + JsonEsc(name.Trim())
                + "\",\"password\":\"" + JsonEsc(pass)
                + "\",\"deviceId\":\"" + JsonEsc(SystemInfo.deviceUniqueIdentifier) + "\"}";
            MenuMsg(register ? "Registering…" : "Logging in…");
            int code = 0;
            string resp = "";
            yield return _net.PostJson(path, body, (c, t) => { code = c; resp = t; });
            if (code == 200)
            {
                ApplyAccountJson(Protocol.ReadString(resp, "token"), Protocol.ExtractObject(resp, "account"));
                MenuMsg("Welcome, " + _displayName);
                yield break;
            }
            string errCode = Protocol.ReadString(resp, "error");
            if (string.IsNullOrEmpty(errCode)) errCode = _net.LastHttpError;
            MenuMsg(AuthHint(errCode));
        }

        bool TryLocalAuth(string name, string pass, bool register, out string err)
        {
            err = null;
            string login = AccountAuth.NormalizeLogin(name);
            string key = "kindling.local." + login;
            const string pepper = "kindling-local-pepper";
            if (register)
            {
                if (PlayerPrefs.HasKey(key)) { err = "NAME_TAKEN"; return false; }
                string id = DeviceAuth.NewAccountId();
                string salt = AccountAuth.NewSalt();
                string hash = AccountAuth.HashPassword(pass, pepper, salt);
                string json = AccountAuth.CreateAccount(id, name.Trim(), login, salt, hash, "");
                PlayerPrefs.SetString(key, json);
                ApplyAccountJson("local." + id, json);
                return true;
            }
            if (!PlayerPrefs.HasKey(key)) { err = "BAD_LOGIN"; return false; }
            string stored = PlayerPrefs.GetString(key, "{}");
            if (!AccountAuth.VerifyPassword(pass, pepper, Protocol.ReadString(stored, "passSalt"), Protocol.ReadString(stored, "passHash")))
            {
                err = "BAD_LOGIN";
                return false;
            }
            ApplyAccountJson("local." + Protocol.ReadString(stored, "id"), stored);
            return true;
        }

        static string AuthHint(string code)
        {
            switch (code)
            {
                case "NAME_SHORT": return "Name must be at least 3 characters";
                case "NAME_LONG": return "Name must be 16 characters or fewer";
                case "NAME_CHARS": return "Use letters, numbers, spaces, - or _";
                case "NAME_TAKEN": return "That name is taken";
                case "PASS_SHORT": return "Password must be at least 6 characters";
                case "PASS_LONG": return "Password is too long";
                case "BAD_LOGIN": return "Name or password is wrong";
                case "unauthorized": return "Log in first";
                default:
                    return string.IsNullOrEmpty(code) ? "Could not reach the match host" : code;
            }
        }

        string CurrentHost()
        {
            if (_hostInput != null && !string.IsNullOrEmpty(_hostInput.text))
                return _hostInput.text.Trim().TrimEnd('/');
            return NetMatchClient.ResolveHost();
        }

        void EnsureNet(string host)
        {
            if (_net == null) _net = gameObject.AddComponent<NetMatchClient>();
            _net.Host = host;
            _net.AuthToken = _authToken;
            _net.OnSnapshot = OnNetSnapshot;
            _net.OnError = code => Toast(code ?? "error");
        }

        void StartPractice()
        {
            if (string.IsNullOrEmpty(_authToken)) { MenuMsg("Log in first"); return; }
            if (_cat == null) return;
            _net?.Disconnect();
            _loop = MatchLoop.Create(_cat, Guid.NewGuid(), (uint)Environment.TickCount, humanSeats: 1);
            if (_loop.Human != null) _loop.Human.DisplayName = _displayName;
            _loop.TutorialWickFloor = PlayerPrefs.GetInt("kindling.help.v1", 0) != 1;
            _matchMode = "practice";
            EnterMatch();
            ArmTimer(Rules.CaptainPickSeconds);
            if (PlayerPrefs.GetInt("kindling.help.v1", 0) != 1)
            {
                _helpStep = 0;
                PaintHelp();
            }
            Refresh();
            Toast("Practice  ·  1v7 bots");
        }

        IEnumerator StartQueue()
        {
            if (string.IsNullOrEmpty(_authToken)) { MenuMsg("Log in first"); yield break; }
            if (_authToken.StartsWith("local.", StringComparison.Ordinal))
            {
                MenuMsg("Local accounts play Practice. Set a match host to queue.");
                yield break;
            }
            string host = CurrentHost();
            if (string.IsNullOrEmpty(host))
            {
                MenuMsg("Set a match host to queue Casual");
                yield break;
            }
            PlayerPrefs.SetString("kindling.host", host);
            PlayerPrefs.Save();
            EnsureNet(host);
            _net.AuthToken = _authToken;
            MenuMsg("Queuing…");
            yield return _net.QueueMatch();
            if (!_net.Connected)
            {
                MenuMsg(string.IsNullOrEmpty(_net.LastHttpError) ? "Queue failed" : AuthHint(_net.LastHttpError));
                yield break;
            }
            _loop = MatchLoop.Create(_cat, Guid.NewGuid(), (uint)Environment.TickCount, humanSeats: 1);
            if (_loop.Human != null) _loop.Human.DisplayName = _displayName;
            _matchMode = "casual";
            EnterMatch();
            Refresh();
            Toast("Casual  ·  queued");
        }

        void EnterMatch()
        {
            _inMatch = true;
            _showingCombat = false;
            _netCombatSeq = 0;
            _netReadySent = false;
            if (_menuRoot != null) _menuRoot.SetActive(false);
            BannerFx.Show("ActionText_Start", 1.1f);
        }

        void LeaveMatch()
        {
            if (!_inMatch) return;
            if (NetLive && _net != null)
                _net.SendRaw("{\"op\":\"Abandon\"}");
            ReturnToMenu();
        }

        void ReturnToMenu()
        {
            _timerArmed = false;
            _showingCombat = false;
            _edictTargeting = false;
            if (_playback != null && _playback.Root != null)
                _playback.Root.SetActive(false);
            if (_glimpsePanel != null) _glimpsePanel.SetActive(false);
            if (_pickPanel != null) _pickPanel.SetActive(false);
            if (_helpPanel != null) _helpPanel.SetActive(false);
            if (_crownPanel != null) _crownPanel.SetActive(false);
            _net?.Disconnect();
            _loop = null;
            ShowMenu();
            MenuMsg("The Ember Exchange");
        }

        void Logout()
        {
            _authToken = "";
            _displayName = "";
            _accountId = "";
            _mmr = 1500;
            SaveSession();
            if (_nameInput != null) _nameInput.text = "";
            if (_passInput != null) _passInput.text = "";
            ShowMenu();
            MenuMsg("Logged out");
        }

        static string JsonEsc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        void Select(SelKind kind, int index)
        {
            if (_loop == null || _loop.State.Phase != Phase.Recruit) return;
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

        bool NetLive => _net != null && _net.Connected;

        void PickCaptain(int offerIndex)
        {
            var p = _loop.Human;
            if (p == null) return;
            var r = Apply(new RecruitAction { Op = RecruitOp.CaptainPick, Seat = p.Seat, OfferIndex = offerIndex });
            if (!r.Ok)
            {
                Toast(r.Code == "CAPTAIN_TAKEN" ? "That Captain is taken" : (r.Code ?? "Cannot pick"));
                Refresh();
                return;
            }
            if (!NetLive)
            {
                _loop.StartFromCaptainPick();
                _pickPanel.SetActive(false);
                _awakenSeen = _loop.State.AwakenEvents;
                ArmTimer(Rules.RecruitSeconds(_loop.State.Round));
            }
            Refresh();
        }

        static Image MakeFillBar(Transform parent, string name, Vector2 min, Vector2 max, string fillSprite)
        {
            var bg = HsUi.Panel(parent, name, min, max, HsUi.Wood);
            StoneTheme.Skin(bg.GetComponent<Image>(), "Slider_Basic01_Bg");
            var fillRt = HsUi.Panel(bg, "fill", new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.78f), HsUi.Ember);
            var img = fillRt.GetComponent<Image>();
            if (!StoneTheme.Skin(img, fillSprite, false))
                img.color = HsUi.Ember;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            img.fillAmount = 1f;
            img.raycastTarget = false;
            return img;
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
            if (_stallZone != null) _stallZone.SetHot(z == _stallZone);
        }

        void OnCardDragEnded(CardDrag drag, PointerEventData ev)
        {
            DropZone z = CardDrag.HitZone(ev);
            if (_handZone != null) _handZone.SetHot(false);
            if (_boardZone != null) _boardZone.SetHot(false);
            if (_stallZone != null) _stallZone.SetHot(false);

            bool handled = false;
            if (z != null && _loop != null && _loop.State.Phase == Phase.Recruit)
            {
                if (drag.Zone == CardZone.Stall && (z.Zone == CardZone.Hand || z.Zone == CardZone.Board))
                {
                    int dest = z.Zone == CardZone.Hand
                        ? InsertIndex(_handCards, Occupied(_handCards), ev)
                        : (_loop.Human != null ? _loop.Human.Hand.Count : 0);
                    BuyStallToHand(drag.Index, dest);
                    handled = true;
                }
                else if ((drag.Zone == CardZone.Board || drag.Zone == CardZone.Hand) && z.Zone == CardZone.Stall)
                {
                    SellFrom(drag.Zone == CardZone.Board ? DestLoc.Board : DestLoc.Hand, drag.Index);
                    handled = true;
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
            if (p == null || _sel != SelKind.Stall) { Toast("Drag a stall card onto your hand or warband"); return; }
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
            if (_sel == SelKind.Board)
                SellFrom(DestLoc.Board, _selIndex);
            else if (_sel == SelKind.Hand)
                SellFrom(DestLoc.Hand, _selIndex);
            else { Toast("Drop a board or hand card onto the stall to sell"); return; }
            Refresh();
        }

        void SellFrom(DestLoc loc, int index)
        {
            var p = _loop.Human;
            if (p == null) return;
            Apply(new RecruitAction { Op = RecruitOp.Sell, Seat = p.Seat, Loc = loc, Index = index });
            _sel = SelKind.None;
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
            if (_showingCombat) return;
            if (_loop.State.Phase != Phase.Recruit) return;
            var p = _loop.Human;
            if (p != null && p.HasFlag(PlayerFlags.GlimpseOpen) && p.GlimpseQueue.Count > 0)
            {
                Toast("Choose a Glimpse first");
                Refresh();
                return;
            }
            if (NetLive)
            {
                if (_netReadySent) return;
                _netReadySent = true;
                Apply(new RecruitAction { Op = RecruitOp.Ready, Seat = p.Seat });
                Toast("Ready");
                BannerFx.Show("ActionText_Ready", 0.9f);
                return;
            }
            BannerFx.Show("ActionText_Ready", 0.8f);
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
            if (_loop != null && _loop.State.MatchOver)
            {
                ShowCrown();
                return;
            }
            if (!NetLive && _loop != null && !_loop.State.MatchOver)
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
            if (Protocol.ReadString(json, "op") == "Error")
            {
                Toast(Protocol.ReadString(json, "code"));
                return;
            }
            SnapshotApply.Apply(_loop.State, _loop.HumanSeat, _cat, json);
            int t = Protocol.ReadInt(json, "timer");
            if (t > 0)
            {
                _timerArmed = true;
                _timerEnd = Time.unscaledTime + t;
            }
            int round = _loop.State.Round;
            if (round != _netRound)
            {
                _netRound = round;
                _netReadySent = false;
            }
            int combatSeq = Protocol.ReadInt(json, "combatSeq");
            string combat = Protocol.ExtractObject(json, "combat");
            if (combatSeq > _netCombatSeq && !string.IsNullOrEmpty(combat) && combat != "null")
            {
                _netCombatSeq = combatSeq;
                _netCombat = CombatSnapshot.Read(combat);
                _showingCombat = true;
                if (_glimpsePanel != null) _glimpsePanel.SetActive(false);
                _playback.Begin(_netCombat, _loop.HumanSeat, _cat, _loop.State.MatchOver, StandingsLine());
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
            BannerFx.Tick();
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
            if (NetLive) return;
            if (Time.unscaledTime < _timerEnd) return;
            _timerArmed = false;
            if (_loop.State.Phase == Phase.CaptainPick)
            {
                if (_loop.Human != null)
                    _loop.PickFirstFreeCaptain(_loop.Human.Seat);
                _loop.StartFromCaptainPick();
                if (_pickPanel != null) _pickPanel.SetActive(false);
                _awakenSeen = _loop.State.AwakenEvents;
                ArmTimer(Rules.RecruitSeconds(_loop.State.Round));
                Refresh();
            }
            else if (_loop.State.Phase == Phase.Recruit)
            {
                Toast("Time — combat starts");
                LockRecruit();
            }
        }

        void OnApplicationPause(bool pause)
        {
            if (!pause && _inMatch && _net != null)
                StartCoroutine(_net.Reconnect());
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus && _inMatch && _net != null && !_net.Connected)
                StartCoroutine(_net.Reconnect());
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
            if (!_inMatch || _loop == null) return;
            var p = _loop.Human;
            bool picking = _loop.State.Phase == Phase.CaptainPick;
            _pickPanel.SetActive(picking);
            if (picking)
            {
                _pickPanel.transform.SetAsLastSibling();
                if (p != null && p.CaptainOffers != null)
                {
                    EnsureOfferCards(p.CaptainOffers.Length);
                    LayoutPickGrid(p.CaptainOffers.Length);
                    for (int i = 0; i < _offerCards.Count; i++)
                    {
                        CaptainDef def = i < p.CaptainOffers.Length ? _cat.GetCaptain(p.CaptainOffers[i]) : null;
                        bool taken = def != null && MatchLoop.CaptainTaken(_loop.State, def.Id, p.Seat);
                        _offerCards[i].BindCaptain(def, false, taken);
                    }
                }
            }

            bool glimpse = p != null && p.HasFlag(PlayerFlags.GlimpseOpen) && p.GlimpseQueue.Count > 0
                           && !_showingCombat && _loop.State.Phase == Phase.Recruit;
            if (_glimpsePanel != null) _glimpsePanel.SetActive(glimpse);
            if (glimpse)
            {
                _glimpsePanel.transform.SetAsLastSibling();
                if (_helpPanel != null) _helpPanel.SetActive(false);
                GlimpseOffer offer = p.GlimpseQueue.Peek();
                for (int i = 0; i < _glimpseCards.Count; i++)
                {
                    UnitDef def = null;
                    if (offer.Choices != null && i < offer.Choices.Length)
                        def = _cat.GetUnit(offer.Choices[i]);
                    _glimpseCards[i].BindPreview(def);
                }
            }
            else if (_helpPanel != null && PlayerPrefs.GetInt("kindling.help.v1", 0) != 1
                     && !_showingCombat && !picking)
            {
                _helpPanel.SetActive(true);
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
            string user = string.IsNullOrEmpty(p.DisplayName) ? _displayName : p.DisplayName;
            string capName = p.Captain.IsEmpty ? "" : NameOfCaptain(p.Captain.Value);
            _hud.text = "R" + _loop.State.Round
                + "   Wick " + p.Wick
                + "   Embers " + p.Embers
                + "   D" + p.Depth
                + (p.Hold ? "   HOLD" : "")
                + (_edictTargeting ? "   EDICT" : "");
            if (_wickFill != null)
                _wickFill.fillAmount = Mathf.Clamp01(p.Wick / 30f);
            if (_emberFill != null)
                _emberFill.fillAmount = Mathf.Clamp01(p.Embers / 20f);
            if (_capRailName != null)
            {
                string who = string.IsNullOrEmpty(user) ? "You" : user;
                _capRailName.text = string.IsNullOrEmpty(capName) ? who : (capName + "\n" + who);
            }
            if (_log != null && _log.transform.parent != null)
            {
                var title = _log.transform.parent.Find("lbt");
                var titleText = title != null ? title.GetComponent<Text>() : null;
                if (titleText != null) titleText.text = "TABLE";
            }

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
                string nm = s.DisplayName ?? ("Seat" + i);
                if (nm.Length > 8) nm = nm.Substring(0, 8);
                lb.Append(s.Alive ? "● " : "○ ");
                lb.Append(nm);
                lb.Append("  W").Append(s.Wick);
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
                    _helpText.text = "1 / 4   Pick one of four Captains. Username is yours; the Captain is this match's power. Taken names cannot be shared.";
                    break;
                case 1:
                    _helpText.text = "2 / 4   Drag a stall card onto HAND or WARBAND to buy (3 Embers). Buys always land in your hand. Drag a warband card onto the stall to sell.";
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
                        + Kindling.Sim.Captains.CaptainPower.Line(cap);
                    return;
                }
            }
            if (!p.Captain.IsEmpty)
            {
                CaptainDef mine = _cat.GetCaptain(p.Captain);
                if (mine != null)
                {
                    _infoLabel.text = mine.Name + "\nWick " + mine.Wick + "\n"
                        + Kindling.Sim.Captains.CaptainPower.Line(mine);
                    return;
                }
            }
            _infoLabel.text = "Tap a Kindled. Drag stall to hand to buy. Drag hand to warband to play.";
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
