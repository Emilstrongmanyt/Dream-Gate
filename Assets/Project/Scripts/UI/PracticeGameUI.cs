using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Combat;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Heroes;
using DreamGate.Battlegrounds.Players;
using DreamGate.Battlegrounds.Services;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.UI
{
    public class PracticeGameUI : MonoBehaviour
    {
        private MatchManager matchManager;
        private readonly List<CardSlotView> shopSlots = new();
        private readonly List<CardSlotView> boardSlots = new();
        private readonly List<CardSlotView> handSlots = new();
        private readonly List<CardSlotView> playerCombatSlots = new();
        private readonly List<CardSlotView> opponentCombatSlots = new();

        private TextMeshProUGUI hudText;
        private TextMeshProUGUI leaderboardText;
        private TextMeshProUGUI logText;
        private GameObject compactLogPanel;
        private TextMeshProUGUI playerHeroText;
        private TextMeshProUGUI opponentHeroText;
        private CombatDamageFloater combatDamageFloater;
        private TextMeshProUGUI resultsText;
        private GameObject recruitPanel;
        private GameObject combatPanel;
        private GameObject resultsPanel;
        private GameObject hudPanel;
        private HeroPortraitSlot recruitShopkeeperHero;
        private HeroPortraitSlot recruitPlayerHero;
        private HeroPortraitSlot combatPlayerHero;
        private HeroPortraitSlot combatOpponentHero;
        private RectTransform playerHeroRect;
        private RectTransform opponentHeroRect;
        private MinionInstance[] combatPlayerBoard;
        private MinionInstance[] combatOpponentBoard;
        private CombatResult activeCombatResult;
        private CardInspectOverlay cardInspectOverlay;
        private Button upgradeButton;
        private Button refreshShopButton;
        private Button endTurnButton;
        private Button menuButton;
        private Button playAgainButton;
        private TextMeshProUGUI recruitCountdownText;
        private Image playerGoldCoinImage;
        private TextMeshProUGUI playerGoldText;
        private int pendingSpellHandIndex = -1;
        private bool humanCombatPlaybackActive;
        private readonly StringBuilder logBuilder = new();
        private const int RecruitCountdownStartSeconds = 20;
        private static readonly Vector2 RecruitCompassCountdownCenter = new(0f, 0f);
        private const float CardScaleFactor = 1.2f;
        private static readonly Vector2 CardSlotSize = new(132f * CardScaleFactor, 168f * CardScaleFactor);
        private const float ShopSlotSpacing = 128f * CardScaleFactor;
        private const float BoardSlotSpacing = 128f * CardScaleFactor;
        private const float HandSlotSpacing = 138f * CardScaleFactor;
        private const float RowLabelOffsetY = 95f * CardScaleFactor;
        private static readonly Vector2 RecruitShopkeeperHeroCenter = new(0, 580);
        private static readonly Vector2 ShopRowCenter = new(0, 150);
        private static readonly Vector2 RecruitPlayerBoardCenter = new(0, -320);
        private static readonly Vector2 RecruitPlayerHeroCenter = new(0, -740);
        private static readonly Vector2 HandRowCenter = new(0, -930);
        private static readonly Vector2 OpponentHeroCenter = new(0, 580);
        private static readonly Vector2 OpponentBoardCenter = new(0, 150);
        private static readonly Vector2 PlayerBoardCenter = new(0, -320);
        private static readonly Vector2 PlayerHeroCenter = new(0, -740);
        private static readonly Vector2 HeroOvalSize = new(156, 172);
        private const int CompactLogMaxLines = 2;
        private const int CompactLogMaxChars = 420;
        private static readonly Color HudPanelColor = new(0.04f, 0.06f, 0.12f, 0.58f);
        private static readonly Color CombatPanelColor = new(0f, 0f, 0f, 0f);

        public void Initialize(MatchManager manager, Transform uiRoot)
        {
            matchManager = manager;
            BuildUI(uiRoot);
            matchManager.StateChanged += Refresh;
            matchManager.MessagePosted += AppendLog;
            matchManager.MatchEnded += OnMatchEnded;
            Refresh();
        }

        public void BeginCombatPlayback(PlayerState opponent, CombatResult result)
        {
            humanCombatPlaybackActive = true;
            activeCombatResult = result;
            combatPlayerBoard = CloneBoardMinions(result?.attackerBoardStart);
            combatOpponentBoard = CloneBoardMinions(result?.defenderBoardStart);
            if (CountLivingCombatMinions(combatPlayerBoard) == 0)
            {
                combatPlayerBoard = CloneBoardMinions(result?.attackerSnapshot?.board);
            }

            if (CountLivingCombatMinions(combatOpponentBoard) == 0)
            {
                combatOpponentBoard = CloneBoardMinions(result?.defenderSnapshot?.board);
            }

            recruitPanel.SetActive(false);
            combatPanel.SetActive(true);
            if (recruitCountdownText != null)
            {
                recruitCountdownText.gameObject.SetActive(false);
            }

            SetCombatHudMode(true, opponent);

            var player = result?.attackerSnapshot ?? matchManager.GetHumanPlayer();
            var defender = result?.defenderSnapshot ?? opponent;
            combatPlayerHero?.SetHero(player.heroName, player.heroId, player.heroHealth, combatDisplay: true);
            combatOpponentHero?.SetHero(
                defender?.heroName ?? "Opponent",
                defender?.heroId,
                defender?.heroHealth ?? 0,
                combatDisplay: true);
            playerHeroText = combatPlayerHero?.NameText;
            opponentHeroText = combatOpponentHero?.NameText;

            RefreshCombatBoards();
            SetRecruitControls(false);
            SetAllSlotButtonsInteractable(false);
        }

        /// <summary>
        /// Combat SFX only during the human player's own combat playback (both boards).
        /// Background bot-vs-bot fights never enter this path.
        /// </summary>
        private void PlayOurCombatSfx(Action playClip)
        {
            if (!humanCombatPlaybackActive)
            {
                return;
            }

            playClip();
        }

        public IEnumerator PlayCombatEvent(CombatEvent combatEvent, float stepSeconds)
        {
            switch (combatEvent.type)
            {
                case CombatEventType.Attack:
                    yield return PlayAttackEvent(combatEvent);
                    break;
                case CombatEventType.Death:
                    yield return PlayDeathEvent(combatEvent);
                    break;
                case CombatEventType.Start:
                    yield return PlayBoardChangeEvent(combatEvent, stepSeconds);
                    break;
                case CombatEventType.Deathrattle:
                    yield return PlayBoardChangeEvent(combatEvent, stepSeconds);
                    break;
                default:
                    yield return new WaitForSeconds(stepSeconds);
                    break;
            }
        }

        public IEnumerator PlayHeroDamage(CombatResult result)
        {
            if (result == null)
            {
                yield break;
            }

            var player = result.attackerSnapshot;
            var opponent = result.defenderSnapshot;
            RectTransform loserRect = null;
            var damage = GetHeroDamageAmount(result);

            switch (result.outcome)
            {
                case CombatOutcome.AttackerWins:
                    loserRect = opponentHeroRect;
                    if (opponent != null)
                    {
                        var remaining = Mathf.Max(0, opponent.heroHealth - damage);
                        combatOpponentHero?.SetHero(opponent.heroName, opponent.heroId, remaining, combatDisplay: true);
                    }

                    break;
                case CombatOutcome.DefenderWins:
                    loserRect = playerHeroRect;
                    if (player != null)
                    {
                        var remaining = Mathf.Max(0, player.heroHealth - damage);
                        combatPlayerHero?.SetHero(player.heroName, player.heroId, remaining, combatDisplay: true);
                    }

                    break;
            }

            if (damage <= 0 || loserRect == null)
            {
                yield break;
            }

            PlayOurCombatSfx(GameSfxPlayer.PlayHit);

            var elapsed = 0f;
            const float duration = 0.45f;
            var startPos = loserRect.anchoredPosition;
            var floatRoutine = combatDamageFloater != null
                ? StartCoroutine(combatDamageFloater.Show(loserRect, damage, CombatDamageStyle.Hero))
                : null;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var shake = Mathf.Sin(elapsed * 48f) * Mathf.Lerp(14f, 0f, elapsed / duration);
                loserRect.anchoredPosition = startPos + new Vector2(shake, 0f);
                yield return null;
            }

            loserRect.anchoredPosition = startPos;
            if (floatRoutine != null)
            {
                yield return floatRoutine;
            }

            yield return new WaitForSeconds(0.2f);
        }

        public void EndCombatPlayback()
        {
            humanCombatPlaybackActive = false;
            SetCombatHudMode(false, null);
            combatPanel.SetActive(false);
            recruitPanel.SetActive(true);
            activeCombatResult = null;
            combatPlayerBoard = null;
            combatOpponentBoard = null;
            Refresh();
        }

        private void OnDestroy()
        {
            if (matchManager == null)
            {
                return;
            }

            matchManager.StateChanged -= Refresh;
            matchManager.MessagePosted -= AppendLog;
            matchManager.MatchEnded -= OnMatchEnded;
        }

        private void BuildUI(Transform root)
        {
            CreateHud(root);
            cardInspectOverlay = gameObject.AddComponent<CardInspectOverlay>();
            cardInspectOverlay.Initialize(root);

            recruitPanel = new GameObject("RecruitPanel", typeof(RectTransform));
            recruitPanel.transform.SetParent(root, false);
            var recruitRect = recruitPanel.GetComponent<RectTransform>();
            recruitRect.anchorMin = Vector2.zero;
            recruitRect.anchorMax = Vector2.one;
            recruitRect.offsetMin = Vector2.zero;
            recruitRect.offsetMax = Vector2.zero;

            recruitShopkeeperHero = CreateHeroOvalPortrait(
                recruitPanel.transform,
                "RecruitShopkeeperHero",
                RecruitShopkeeperHeroCenter,
                new Color(0.55f, 0.35f, 0.2f, 0.55f));
            recruitPlayerHero = CreateHeroOvalPortrait(
                recruitPanel.transform,
                "RecruitPlayerHero",
                RecruitPlayerHeroCenter,
                new Color(0.2f, 0.35f, 0.65f, 0.55f));
            CreatePlayerGoldDisplay(recruitPanel.transform);

            recruitCountdownText = CreateRecruitCountdown(recruitPanel.transform);

            CreateSlotRow(recruitPanel.transform, shopSlots, "Shop", ShopRowCenter, 5, ShopSlotSpacing, CardSlotDisplayMode.Shop, OnShopClicked);
            CreateSlotRow(
                recruitPanel.transform,
                boardSlots,
                "Your Army",
                RecruitPlayerBoardCenter,
                5,
                BoardSlotSpacing,
                CardSlotDisplayMode.Board,
                OnBoardClicked,
                showSectionLabel: false);
            CreateSlotRow(
                recruitPanel.transform,
                handSlots,
                "Hand",
                HandRowCenter,
                6,
                HandSlotSpacing,
                CardSlotDisplayMode.Hand,
                OnHandClicked,
                showSectionLabel: false);

            refreshShopButton = CreateSpriteButton(recruitPanel.transform, "RefreshButton", new Vector2(250, 610), new Vector2(180, 72), OnRefreshShopClicked);
            upgradeButton = CreateSpriteButton(recruitPanel.transform, "TierUpgradeButton", new Vector2(250, 510), new Vector2(180, 72), OnUpgradeClicked);
            endTurnButton = CreateSpriteButton(recruitPanel.transform, "StartCombatButton", new Vector2(420, -620), new Vector2(180, 72), OnEndTurnClicked);
            if (matchManager.Mode == MatchMode.Rated)
            {
                endTurnButton.gameObject.SetActive(false);
            }

            menuButton = CreateSpriteButton(recruitPanel.transform, "BackButton", new Vector2(420, -760), new Vector2(180, 72), () => SceneNavigator.LoadMainMenu());

            BuildCombatPanel(root);

            resultsPanel = CreatePanel(root, "ResultsPanel", new Color(0.08f, 0.1f, 0.18f, 0.96f));
            resultsText = CreateText(resultsPanel.transform, "ResultsText", new Vector2(0, 80), 28, TextAlignmentOptions.Center);
            resultsText.rectTransform.sizeDelta = new Vector2(800, 500);
            playAgainButton = CreateActionButton(resultsPanel.transform, "Play Again", new Vector2(0, -180), OnPlayAgainClicked);
            resultsPanel.SetActive(false);
        }

        private void BuildCombatPanel(Transform root)
        {
            combatPanel = CreatePanel(root, "CombatPanel", CombatPanelColor);
            combatPanel.SetActive(false);

            combatOpponentHero = CreateHeroOvalPortrait(
                combatPanel.transform,
                "OpponentHero",
                OpponentHeroCenter,
                new Color(0.55f, 0.35f, 0.2f, 0.55f),
                combatHero: true);
            opponentHeroRect = combatOpponentHero.Root;
            opponentHeroText = combatOpponentHero.NameText;

            CreateSlotRow(
                combatPanel.transform,
                opponentCombatSlots,
                "Rival Army",
                OpponentBoardCenter,
                5,
                BoardSlotSpacing,
                CardSlotDisplayMode.Combat,
                _ => { },
                includeCombatMotion: true);

            CreateSlotRow(
                combatPanel.transform,
                playerCombatSlots,
                "Your Army",
                PlayerBoardCenter,
                5,
                BoardSlotSpacing,
                CardSlotDisplayMode.Combat,
                _ => { },
                includeCombatMotion: true);

            combatPlayerHero = CreateHeroOvalPortrait(
                combatPanel.transform,
                "PlayerHero",
                PlayerHeroCenter,
                new Color(0.2f, 0.35f, 0.65f, 0.55f),
                combatHero: true);
            playerHeroRect = combatPlayerHero.Root;
            playerHeroText = combatPlayerHero.NameText;

            var floaterGo = new GameObject("CombatDamageFloater", typeof(RectTransform));
            floaterGo.transform.SetParent(combatPanel.transform, false);
            var floaterRect = floaterGo.GetComponent<RectTransform>();
            floaterRect.anchorMin = Vector2.zero;
            floaterRect.anchorMax = Vector2.one;
            floaterRect.offsetMin = Vector2.zero;
            floaterRect.offsetMax = Vector2.zero;
            combatDamageFloater = floaterGo.AddComponent<CombatDamageFloater>();
            combatDamageFloater.Initialize(floaterGo.transform);
        }

        private void SetCombatHudMode(bool inCombat, PlayerState opponent)
        {
            if (hudPanel != null)
            {
                hudPanel.SetActive(true);
            }

            if (leaderboardText != null)
            {
                leaderboardText.gameObject.SetActive(!inCombat);
            }

            if (compactLogPanel != null)
            {
                compactLogPanel.SetActive(!inCombat);
            }

            if (!inCombat || hudText == null)
            {
                return;
            }

            var player = matchManager?.GetHumanPlayer();
            var opponentName = opponent?.heroName ?? "Opponent";
            hudText.text = $"COMBAT  •  {player?.heroName ?? "You"} vs {opponentName}";
            hudText.fontSize = 26;
        }

        // Hero ovals use UiImageSprites (runtime 1x1 sprite) — no builtin UISprite.psd.
        private static HeroPortraitSlot CreateHeroOvalPortrait(
            Transform parent,
            string name,
            Vector2 position,
            Color placeholderColor,
            bool combatHero = false)
        {
            var solidSprite = UiImageSprites.GetSolid();
            var rootGo = new GameObject(name, typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);
            var root = rootGo.GetComponent<RectTransform>();
            root.anchoredPosition = position;
            root.sizeDelta = HeroOvalSize;

            var frameGo = new GameObject("OvalFrame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(root, false);
            var frameRect = frameGo.GetComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            var frameImage = frameGo.GetComponent<Image>();
            frameImage.sprite = solidSprite;
            frameImage.color = new Color(0f, 0f, 0f, 0f);
            frameImage.raycastTarget = false;

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(root, false);
            var portraitRect = portraitGo.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.06f, 0.08f);
            portraitRect.anchorMax = new Vector2(0.94f, 0.92f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;
            var portraitImage = portraitGo.GetComponent<Image>();
            portraitImage.sprite = solidSprite;
            portraitImage.color = placeholderColor;
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
            portraitImage.enabled = true;

            var nameGo = new GameObject("HeroName", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(root, false);
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, -0.18f);
            nameRect.anchorMax = new Vector2(1f, 0.02f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            var nameText = nameGo.GetComponent<TextMeshProUGUI>();
            nameText.fontSize = 17;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;
            nameText.text = name;

            var hpGo = new GameObject("HeroHp", typeof(RectTransform), typeof(TextMeshProUGUI));
            hpGo.transform.SetParent(root, false);
            var hpRect = hpGo.GetComponent<RectTransform>();
            hpRect.anchorMin = new Vector2(0f, -0.34f);
            hpRect.anchorMax = new Vector2(1f, -0.16f);
            hpRect.offsetMin = Vector2.zero;
            hpRect.offsetMax = Vector2.zero;
            var hpText = hpGo.GetComponent<TextMeshProUGUI>();
            hpText.fontSize = combatHero ? 22 : 15;
            hpText.fontStyle = combatHero ? FontStyles.Bold : FontStyles.Normal;
            hpText.alignment = TextAlignmentOptions.Center;
            hpText.color = new Color(0.9f, 0.95f, 1f);
            hpText.text = "— HP";

            return new HeroPortraitSlot(root, portraitImage, nameText, hpText);
        }

        private void RefreshCombatBoards()
        {
            RefreshCombatRow(playerCombatSlots, combatPlayerBoard);
            RefreshCombatRow(opponentCombatSlots, combatOpponentBoard);
        }

        private static void RefreshCombatRow(List<CardSlotView> slots, MinionInstance[] board)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (board == null || i >= board.Length || board[i] == null)
                {
                    slots[i].SetEmpty(string.Empty, false);
                    continue;
                }

                var minion = board[i];
                var card = CardRegistry.Get(minion.cardId);
                slots[i].SetCombatMinion(card, minion);
            }
        }

        private static MinionInstance[] CloneBoardMinions(MinionInstance[] board)
        {
            var clone = new MinionInstance[MatchConfig.BoardSize];
            if (board == null)
            {
                return clone;
            }

            for (var i = 0; i < board.Length && i < clone.Length; i++)
            {
                clone[i] = board[i] != null ? board[i].Clone() : null;
            }

            return clone;
        }

        private static int CountLivingCombatMinions(MinionInstance[] board)
        {
            if (board == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var minion in board)
            {
                if (minion != null && !minion.isDead)
                {
                    count++;
                }
            }

            return count;
        }

        private IEnumerator PlayAttackEvent(CombatEvent combatEvent)
        {
            if (combatEvent.attackerBoardIndex < 0)
            {
                yield break;
            }

            if (combatEvent.damageAmount > 0 && !combatEvent.isRecoil)
            {
                PlayOurCombatSfx(GameSfxPlayer.PlayHit);
            }

            var striker = GetCombatMinion(combatEvent.isAttackerBoard, combatEvent.attackerBoardIndex);
            if (striker == null || striker.isDead)
            {
                yield break;
            }

            var damage = combatEvent.damageAmount;
            if (combatEvent.isRecoil)
            {
                if (damage <= 0)
                {
                    yield break;
                }

                striker.health -= damage;
                RefreshCombatSlot(
                    combatEvent.isAttackerBoard ? playerCombatSlots : opponentCombatSlots,
                    combatEvent.isAttackerBoard ? combatPlayerBoard : combatOpponentBoard,
                    combatEvent.attackerBoardIndex);

                var strikerSlot = GetCombatSlot(combatEvent.isAttackerBoard, combatEvent.attackerBoardIndex);
                Coroutine floatRoutine = null;
                if (strikerSlot?.RootRect != null && combatDamageFloater != null)
                {
                    floatRoutine = StartCoroutine(
                        combatDamageFloater.Show(strikerSlot.RootRect, damage, CombatDamageStyle.Recoil));
                }

                if (strikerSlot?.CombatMotion != null)
                {
                    yield return strikerSlot.CombatMotion.PlayHitShake();
                }
                else
                {
                    yield return new WaitForSeconds(0.2f);
                }

                if (floatRoutine != null)
                {
                    yield return floatRoutine;
                }

                yield break;
            }

            if (combatEvent.defenderBoardIndex < 0)
            {
                yield break;
            }

            var target = GetCombatMinion(!combatEvent.isAttackerBoard, combatEvent.defenderBoardIndex);
            if (target == null || target.isDead)
            {
                yield break;
            }

            if (damage <= 0)
            {
                damage = striker.attack;
            }

            target.health -= damage;

            RefreshCombatSlot(
                combatEvent.isAttackerBoard ? playerCombatSlots : opponentCombatSlots,
                combatEvent.isAttackerBoard ? combatPlayerBoard : combatOpponentBoard,
                combatEvent.attackerBoardIndex);
            RefreshCombatSlot(
                combatEvent.isAttackerBoard ? opponentCombatSlots : playerCombatSlots,
                combatEvent.isAttackerBoard ? combatOpponentBoard : combatPlayerBoard,
                combatEvent.defenderBoardIndex);

            var attackerSlot = GetCombatSlot(combatEvent.isAttackerBoard, combatEvent.attackerBoardIndex);
            var defenderSlot = GetCombatSlot(!combatEvent.isAttackerBoard, combatEvent.defenderBoardIndex);
            var lungeDirection = combatEvent.isAttackerBoard ? Vector2.up : Vector2.down;
            var damageStyle = combatEvent.isCleave ? CombatDamageStyle.Cleave : CombatDamageStyle.Incoming;
            Coroutine damageFloatRoutine = null;
            if (defenderSlot?.RootRect != null && combatDamageFloater != null)
            {
                damageFloatRoutine = StartCoroutine(
                    combatDamageFloater.Show(defenderSlot.RootRect, damage, damageStyle));
            }

            if (!combatEvent.isCleave && attackerSlot?.CombatMotion != null && defenderSlot?.CombatMotion != null)
            {
                var lunge = attackerSlot.CombatMotion.PlayAttackLunge(lungeDirection);
                var hit = defenderSlot.CombatMotion.PlayHitShake();
                yield return lunge;
                yield return hit;
            }
            else if (combatEvent.isCleave && defenderSlot?.CombatMotion != null)
            {
                yield return defenderSlot.CombatMotion.PlayHitShake();
            }
            else
            {
                yield return new WaitForSeconds(0.2f);
            }

            if (damageFloatRoutine != null)
            {
                yield return damageFloatRoutine;
            }
        }

        private IEnumerator PlayDeathEvent(CombatEvent combatEvent)
        {
            if (combatEvent.boardIndex < 0)
            {
                yield return new WaitForSeconds(0.15f);
                yield break;
            }

            PlayOurCombatSfx(GameSfxPlayer.PlaySellCard);

            var slots = combatEvent.isAttackerBoard ? playerCombatSlots : opponentCombatSlots;
            var board = combatEvent.isAttackerBoard ? combatPlayerBoard : combatOpponentBoard;
            if (board == null || combatEvent.boardIndex < 0 || combatEvent.boardIndex >= board.Length ||
                board[combatEvent.boardIndex] == null)
            {
                yield break;
            }

            var minion = board[combatEvent.boardIndex];
            minion.isDead = true;
            minion.health = Mathf.Max(0, minion.health);
            RefreshCombatSlot(slots, board, combatEvent.boardIndex);
            var slot = slots[combatEvent.boardIndex];
            if (slot?.CombatMotion != null)
            {
                slot.IdleMotion?.SetActiveMotion(false);
                yield return slot.CombatMotion.PlayDeath();
            }

            slot?.SetDefeated();
            yield return new WaitForSeconds(0.08f);
        }

        private MinionInstance GetCombatMinion(bool isAttackerBoard, int boardIndex)
        {
            var board = isAttackerBoard ? combatPlayerBoard : combatOpponentBoard;
            if (board == null || boardIndex < 0 || boardIndex >= board.Length)
            {
                return null;
            }

            return board[boardIndex];
        }

        private CardSlotView GetCombatSlot(bool isAttackerBoard, int boardIndex)
        {
            var slots = isAttackerBoard ? playerCombatSlots : opponentCombatSlots;
            if (boardIndex < 0 || boardIndex >= slots.Count)
            {
                return null;
            }

            return slots[boardIndex];
        }

        private static void RefreshCombatSlot(List<CardSlotView> slots, MinionInstance[] board, int index)
        {
            if (board == null || index < 0 || index >= board.Length || index >= slots.Count)
            {
                return;
            }

            var minion = board[index];
            if (minion == null)
            {
                slots[index].SetEmpty("—", false);
                return;
            }

            var card = CardRegistry.Get(minion.cardId);
            slots[index].SetCombatMinion(card, minion);
        }

        private IEnumerator PlayBoardChangeEvent(CombatEvent combatEvent, float stepSeconds)
        {
            if (combatEvent.attackDelta != 0 || combatEvent.healthDelta != 0)
            {
                ApplyCombatStatDelta(combatEvent);
            }
            else if (!string.IsNullOrEmpty(combatEvent.abilityCardId))
            {
                ApplyCombatAbilityChange(combatEvent);
            }
            else
            {
                RefreshCombatBoards();
            }

            if (combatEvent.type == CombatEventType.Deathrattle && combatEvent.boardIndex >= 0)
            {
                PlayOurCombatSfx(GameSfxPlayer.PlayDropCard);
            }
            else if (combatEvent.attackDelta != 0 || combatEvent.healthDelta != 0)
            {
                PlayOurCombatSfx(GameSfxPlayer.PlayHit);
            }

            yield return new WaitForSeconds(stepSeconds * 0.6f);
        }

        private void ApplyCombatStatDelta(CombatEvent combatEvent)
        {
            if (combatEvent.boardIndex < 0)
            {
                RefreshCombatBoards();
                return;
            }

            var board = combatEvent.isAttackerBoard ? combatPlayerBoard : combatOpponentBoard;
            var slots = combatEvent.isAttackerBoard ? playerCombatSlots : opponentCombatSlots;
            if (board == null || combatEvent.boardIndex < 0 || combatEvent.boardIndex >= board.Length ||
                board[combatEvent.boardIndex] == null)
            {
                return;
            }

            var minion = board[combatEvent.boardIndex];
            if (combatEvent.attackDelta != 0)
            {
                minion.attack += combatEvent.attackDelta;
            }

            if (combatEvent.healthDelta != 0)
            {
                minion.health += combatEvent.healthDelta;
                minion.maxHealth += combatEvent.healthDelta;
            }

            RefreshCombatSlot(slots, board, combatEvent.boardIndex);
        }

        private void ApplyCombatAbilityChange(CombatEvent combatEvent)
        {
            var board = combatEvent.isAttackerBoard ? combatPlayerBoard : combatOpponentBoard;
            var slots = combatEvent.isAttackerBoard ? playerCombatSlots : opponentCombatSlots;
            if (board == null)
            {
                return;
            }

            var definition = CardRegistry.Get(combatEvent.abilityCardId);
            if (definition == null)
            {
                RefreshCombatBoards();
                return;
            }

            if (combatEvent.boardIndex >= 0 && combatEvent.boardIndex < board.Length)
            {
                if (board[combatEvent.boardIndex] != null)
                {
                    var minion = board[combatEvent.boardIndex];
                    minion.cardId = definition.cardId;
                    minion.attack = definition.attack;
                    minion.health = definition.health;
                    minion.maxHealth = definition.health;
                    RefreshCombatSlot(slots, board, combatEvent.boardIndex);
                    return;
                }

                board[combatEvent.boardIndex] = MinionInstance.FromDefinition(definition);
                RefreshCombatSlot(slots, board, combatEvent.boardIndex);
                return;
            }

            for (var i = 0; i < board.Length; i++)
            {
                if (board[i] == null)
                {
                    board[i] = MinionInstance.FromDefinition(definition);
                    RefreshCombatBoards();
                    return;
                }
            }
        }

        private static int GetHeroDamageAmount(CombatResult result)
        {
            if (result.outcome == CombatOutcome.AttackerWins)
            {
                return result.damageToDefender > 0
                    ? result.damageToDefender
                    : DamageCalculator.CalculateSurvivorTierSum(result.attackerSnapshot.GetLivingBoard());
            }

            if (result.outcome == CombatOutcome.DefenderWins)
            {
                return result.damageToAttacker > 0
                    ? result.damageToAttacker
                    : DamageCalculator.CalculateSurvivorTierSum(result.defenderSnapshot.GetLivingBoard());
            }

            return 0;
        }

        private void CreateHud(Transform root)
        {
            hudPanel = CreatePanel(root, "HudPanel", HudPanelColor);
            var hudRect = hudPanel.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0.5f, 1f);
            hudRect.anchorMax = new Vector2(0.5f, 1f);
            hudRect.pivot = new Vector2(0.5f, 1f);
            hudRect.anchoredPosition = new Vector2(0, -8);
            hudRect.sizeDelta = new Vector2(980, 188);

            hudText = CreateText(hudPanel.transform, "HUD", new Vector2(0, -20), 22, TextAlignmentOptions.Center);
            hudText.rectTransform.sizeDelta = new Vector2(940, 72);

            leaderboardText = CreateText(hudPanel.transform, "Leaderboard", new Vector2(0, -88), 15, TextAlignmentOptions.Center);
            leaderboardText.rectTransform.sizeDelta = new Vector2(940, 32);
            leaderboardText.color = new Color(0.85f, 0.9f, 1f);

            compactLogPanel = new GameObject("CompactLog", typeof(RectTransform), typeof(Image));
            compactLogPanel.transform.SetParent(hudPanel.transform, false);
            var compactLogRect = compactLogPanel.GetComponent<RectTransform>();
            compactLogRect.anchorMin = new Vector2(0.5f, 0f);
            compactLogRect.anchorMax = new Vector2(0.5f, 0f);
            compactLogRect.pivot = new Vector2(0.5f, 0f);
            compactLogRect.anchoredPosition = new Vector2(0, 8);
            compactLogRect.sizeDelta = new Vector2(920, 52);
            compactLogPanel.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.42f);

            logText = CreateText(compactLogPanel.transform, "Log", Vector2.zero, 12, TextAlignmentOptions.TopLeft);
            logText.rectTransform.anchorMin = Vector2.zero;
            logText.rectTransform.anchorMax = Vector2.one;
            logText.rectTransform.offsetMin = new Vector2(12, 6);
            logText.rectTransform.offsetMax = new Vector2(-12, -6);
            logText.color = new Color(0.82f, 0.88f, 0.96f);
            logText.overflowMode = TextOverflowModes.Ellipsis;
            logText.maxVisibleLines = CompactLogMaxLines;
        }

        private void Refresh()
        {
            var player = matchManager.GetHumanPlayer();
            if (combatPanel != null && combatPanel.activeSelf)
            {
                RefreshRecruitHeroes(player);
                return;
            }

            hudText.fontSize = 22;
            var modeLabel = matchManager.Mode == MatchMode.Rated ? "RATED" : "PRACTICE";
            var timerSuffix = matchManager.Phase == MatchPhase.Recruit
                ? $"  •  {Mathf.CeilToInt(matchManager.RecruitTimeRemaining)}s"
                : string.Empty;
            var mmrSuffix = matchManager.Mode == MatchMode.Rated &&
                            DreamGateServices.IsInitialized &&
                            DreamGateServices.IsLoggedIn &&
                            DreamGateServices.Profile != null
                ? $"  •  MMR {DreamGateServices.Profile.mmr}"
                : string.Empty;

            hudText.text =
                $"{modeLabel}  •  Turn {matchManager.Turn}{timerSuffix}\n" +
                $"Tier {player.tavernTier}/{MatchConfig.MaxTavernTier}  •  HP {player.heroHealth}{mmrSuffix}";

            leaderboardText.text = matchManager.GetLeaderboardSummary();
            RefreshRecruitHeroes(player);
            RefreshPlayerGold(player);
            RefreshRecruitCountdown();

            var inCombatPlayback = combatPanel != null && combatPanel.activeSelf;
            if (!inCombatPlayback)
            {
                RefreshShop(player);
                RefreshBoard(player);
                RefreshHand(player);
            }

            var canAct = matchManager.Phase == MatchPhase.Recruit && !inCombatPlayback;
            SetRecruitControls(canAct);
        }

        private void SetRecruitControls(bool enabled)
        {
            var player = matchManager.GetHumanPlayer();
            refreshShopButton.interactable = enabled && player.gold >= MatchConfig.ShopRefreshCost;
            upgradeButton.interactable = enabled;
            if (endTurnButton != null && matchManager.Mode != MatchMode.Rated)
            {
                endTurnButton.interactable = enabled;
            }
        }

        private void SetAllSlotButtonsInteractable(bool enabled)
        {
            foreach (var slot in shopSlots)
            {
                slot.Button.interactable = enabled;
            }

            foreach (var slot in boardSlots)
            {
                slot.Button.interactable = enabled;
            }

            foreach (var slot in handSlots)
            {
                slot.Button.interactable = enabled;
            }
        }

        private void RefreshShop(PlayerState player)
        {
            for (var i = 0; i < shopSlots.Count; i++)
            {
                if (i >= player.shopCardIds.Count || string.IsNullOrEmpty(player.shopCardIds[i]))
                {
                    shopSlots[i].SetEmpty(string.Empty, false);
                    continue;
                }

                var card = CardRegistry.Get(player.shopCardIds[i]);
                shopSlots[i].SetShopCard(card, matchManager.Phase == MatchPhase.Recruit);
            }
        }

        private void RefreshBoard(PlayerState player)
        {
            for (var i = 0; i < boardSlots.Count; i++)
            {
                if (player.board[i] == null)
                {
                    boardSlots[i].SetEmpty(string.Empty, false);
                    continue;
                }

                var minion = player.board[i];
                var card = CardRegistry.Get(minion.cardId);
                boardSlots[i].SetMinionCard(card, minion, CardSlotDisplayMode.Board, matchManager.Phase == MatchPhase.Recruit);
            }
        }

        private void RefreshRecruitHeroes(PlayerState player)
        {
            recruitShopkeeperHero?.SetShopkeeper();
            recruitPlayerHero?.SetHero(player.heroName, player.heroId, player.heroHealth);
        }

        private void RefreshPlayerGold(PlayerState player)
        {
            if (playerGoldText == null)
            {
                return;
            }

            playerGoldText.text = player.gold.ToString();
        }

        private void RefreshRecruitCountdown()
        {
            if (recruitCountdownText == null)
            {
                return;
            }

            var showCountdown = matchManager.Phase == MatchPhase.Recruit &&
                                (combatPanel == null || !combatPanel.activeSelf) &&
                                matchManager.RecruitTimeRemaining <= RecruitCountdownStartSeconds;
            recruitCountdownText.gameObject.SetActive(showCountdown);
            if (!showCountdown)
            {
                return;
            }

            recruitCountdownText.text = Mathf.CeilToInt(matchManager.RecruitTimeRemaining).ToString();
        }

        private void CreatePlayerGoldDisplay(Transform parent)
        {
            var coinSprite = Resources.Load<Sprite>("Coin");
            var go = new GameObject("PlayerGoldDisplay", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = RecruitPlayerHeroCenter + new Vector2(118f, 8f);
            rect.sizeDelta = new Vector2(88f, 88f);

            var coinGo = new GameObject("Coin", typeof(RectTransform), typeof(Image));
            coinGo.transform.SetParent(go.transform, false);
            var coinRect = coinGo.GetComponent<RectTransform>();
            coinRect.anchorMin = Vector2.zero;
            coinRect.anchorMax = Vector2.one;
            coinRect.offsetMin = Vector2.zero;
            coinRect.offsetMax = Vector2.zero;
            playerGoldCoinImage = coinGo.GetComponent<Image>();
            playerGoldCoinImage.sprite = coinSprite;
            playerGoldCoinImage.preserveAspect = true;
            playerGoldCoinImage.raycastTarget = false;
            playerGoldCoinImage.color = coinSprite != null ? Color.white : new Color(0.85f, 0.7f, 0.15f, 0.95f);

            var textGo = new GameObject("GoldAmount", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            playerGoldText = textGo.GetComponent<TextMeshProUGUI>();
            playerGoldText.fontSize = 30;
            playerGoldText.fontStyle = FontStyles.Bold;
            playerGoldText.alignment = TextAlignmentOptions.Center;
            playerGoldText.color = Color.white;
            playerGoldText.outlineWidth = 0.35f;
            playerGoldText.outlineColor = Color.black;
            playerGoldText.text = "0";
        }

        private static TextMeshProUGUI CreateRecruitCountdown(Transform parent)
        {
            var go = new GameObject("RecruitCompassCountdown", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = RecruitCompassCountdownCenter;
            rect.sizeDelta = new Vector2(180f, 180f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = 96;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.95f, 0.72f, 1f);
            text.outlineWidth = 0.4f;
            text.outlineColor = new Color(0.12f, 0.08f, 0.02f, 1f);
            text.text = string.Empty;
            go.SetActive(false);
            return text;
        }

        private void RefreshHand(PlayerState player)
        {
            for (var visualSlot = 0; visualSlot < handSlots.Count; visualSlot++)
            {
                var handIndex = HandLayout.GetHandIndexForVisualSlot(visualSlot, player.hand.Count);
                if (handIndex < 0)
                {
                    handSlots[visualSlot].SetEmpty(string.Empty, false);
                    continue;
                }

                var minion = player.hand[handIndex];
                var card = CardRegistry.Get(minion.cardId);
                handSlots[visualSlot].SetMinionCard(card, minion, CardSlotDisplayMode.Hand, matchManager.Phase == MatchPhase.Recruit);

                var dragHandler = handSlots[visualSlot].RootRect.GetComponent<HandCardDragHandler>();
                if (dragHandler != null)
                {
                    dragHandler.Configure(handIndex, visualSlot, this);
                }

            }
        }

        private void OnShopClicked(int index)
        {
            matchManager.TryBuyFromShop(index, out var message);
            AppendLog(message);
        }

        private void OnBoardClicked(int index)
        {
            if (pendingSpellHandIndex >= 0)
            {
                matchManager.TryCastSpellFromHand(pendingSpellHandIndex, index, out var castMessage);
                pendingSpellHandIndex = -1;
                AppendLog(castMessage);
                return;
            }

            matchManager.TrySellFromBoard(index, out var message);
            AppendLog(message);
        }

        internal bool CanDragBoardSlot(int index)
        {
            if (matchManager == null || matchManager.Phase != MatchPhase.Recruit)
            {
                return false;
            }

            var player = matchManager.GetHumanPlayer();
            return player != null && index >= 0 && index < player.board.Length && player.board[index] != null;
        }

        internal int GetBoardDropSlotIndex(Vector2 screenPosition, Camera eventCamera, bool requireEmpty = false)
        {
            var player = matchManager?.GetHumanPlayer();
            var bestIndex = -1;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < boardSlots.Count; i++)
            {
                if (requireEmpty && player != null && player.board[i] != null)
                {
                    continue;
                }

                var slotRect = boardSlots[i].RootRect;
                if (RectTransformUtility.RectangleContainsScreenPoint(slotRect, screenPosition, eventCamera))
                {
                    return i;
                }

                var screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, slotRect.position);
                var distance = Vector2.Distance(screenPosition, screenPoint);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            const float maxDropDistance = 220f;
            return bestDistance <= maxDropDistance ? bestIndex : -1;
        }

        internal bool CanDragHandSlot(int handIndex)
        {
            if (matchManager == null || matchManager.Phase != MatchPhase.Recruit)
            {
                return false;
            }

            var player = matchManager.GetHumanPlayer();
            if (player == null || handIndex < 0 || handIndex >= player.hand.Count || player.BoardFull)
            {
                return false;
            }

            var definition = CardRegistry.Get(player.hand[handIndex].cardId);
            return definition == null || definition.cardKind != CardKind.Spell;
        }

        internal void TryPlayHandToBoard(int handIndex, int boardIndex)
        {
            matchManager.TryPlayFromHandToSlot(handIndex, boardIndex, out var message);
            if (!string.IsNullOrEmpty(message))
            {
                AppendLog(message);
            }
        }

        internal void ForceRefreshHand()
        {
            var player = matchManager?.GetHumanPlayer();
            if (player != null)
            {
                RefreshHand(player);
            }
        }

        internal void TryBoardReorder(int fromIndex, int toIndex)
        {
            matchManager.TryReorderBoard(fromIndex, toIndex, out var message);
            if (!string.IsNullOrEmpty(message))
            {
                AppendLog(message);
            }
        }

        internal Transform GetBoardDragLayer()
        {
            return recruitPanel != null ? recruitPanel.transform : transform;
        }

        private void OnHandClicked(int visualSlotIndex)
        {
            var player = matchManager.GetHumanPlayer();
            var handIndex = HandLayout.GetHandIndexForVisualSlot(visualSlotIndex, player.hand.Count);
            if (handIndex < 0)
            {
                return;
            }

            var definition = CardRegistry.Get(player.hand[handIndex].cardId);
            if (definition != null && definition.cardKind == CardKind.Spell)
            {
                if (!SpellSystem.RequiresBoardTarget(definition.spellEffect))
                {
                    pendingSpellHandIndex = -1;
                    matchManager.TryCastSpellFromHand(handIndex, -1, out var castMessage);
                    AppendLog(castMessage);
                }
                else
                {
                    pendingSpellHandIndex = handIndex;
                    AppendLog($"Select a friendly minion for {definition.displayName}.");
                }

                return;
            }

            pendingSpellHandIndex = -1;
            matchManager.TryPlayFromHand(handIndex, out var message);
            AppendLog(message);
        }

        private void OnUpgradeClicked()
        {
            matchManager.TryUpgradeTavern(out var message);
            AppendLog(message);
        }

        private void OnRefreshShopClicked()
        {
            matchManager.TryRefreshShop(out var message);
            AppendLog(message);
        }

        private void OnEndTurnClicked()
        {
            matchManager.EndRecruitEarly();
        }

        private void OnPlayAgainClicked()
        {
            if (matchManager.Mode == MatchMode.Rated)
            {
                SceneNavigator.LoadRatedLobby();
                return;
            }

            MatchSessionContext.BeginPractice();
            SceneNavigator.LoadPracticeGame();
        }

        private void OnMatchEnded()
        {
            SetRecruitControls(false);
            ShowResults(matchManager.FinalResult);
        }

        private void ShowResults(MatchResult result)
        {
            if (result == null)
            {
                return;
            }

            resultsPanel.SetActive(true);
            var outcome = result.playerWon ? "VICTORY" : "DEFEAT";
            var eliminations = result.eliminationOrder.Count > 0
                ? string.Join("\n", result.eliminationOrder)
                : "No eliminations recorded.";

            var mmrSection = result.matchMode == MatchMode.Rated
                ? $"\nMMR: {result.mmrBefore} → {result.mmrAfter} ({FormatMmrDelta(result.mmrDelta)})\n"
                : string.Empty;

            resultsText.text =
                $"{outcome}\n\n" +
                $"Hero: {result.heroName}\n" +
                $"Placement: #{result.placement}\n" +
                $"Turns Played: {result.turnsPlayed}\n" +
                $"Final Health: {result.finalHeroHealth}\n" +
                $"Damage Dealt: {result.damageDealt}\n" +
                $"Damage Taken: {result.damageTaken}" +
                mmrSection +
                $"\nEliminations:\n{eliminations}";

            playAgainButton.GetComponentInChildren<TextMeshProUGUI>().text =
                result.matchMode == MatchMode.Rated ? "Queue Again" : "Play Again";
        }

        private static string FormatMmrDelta(int delta)
        {
            return delta >= 0 ? $"+{delta}" : delta.ToString();
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            logBuilder.Insert(0, message + "\n");
            if (logBuilder.Length > CompactLogMaxChars)
            {
                logBuilder.Length = CompactLogMaxChars;
            }

            logText.text = TrimLogForDisplay(logBuilder.ToString(), CompactLogMaxLines);
        }

        private static string TrimLogForDisplay(string raw, int maxLines)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var lines = raw.Split('\n');
            var kept = new List<string>(maxLines);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                kept.Add(line.Trim());
                if (kept.Count >= maxLines)
                {
                    break;
                }
            }

            return string.Join("\n", kept);
        }

        private void CreateSlotRow(
            Transform parent,
            List<CardSlotView> slots,
            string label,
            Vector2 center,
            int count,
            float spacing,
            CardSlotDisplayMode mode,
            Action<int> onClick,
            bool includeCombatMotion = false,
            bool showSectionLabel = true)
        {
            slots.Clear();
            var totalWidth = (count - 1) * spacing;
            var startX = center.x - totalWidth * 0.5f;

            if (showSectionLabel)
            {
                CreateSectionLabel(parent, label, new Vector2(-470, center.y + RowLabelOffsetY));
            }

            for (var i = 0; i < count; i++)
            {
                var index = i;
                var position = new Vector2(startX + i * spacing, center.y);
                var slot = CreateCardSlot(
                    parent,
                    $"{label}{i + 1}",
                    position,
                    mode,
                    () => onClick(index),
                    cardInspectOverlay,
                    includeCombatMotion);
                if (mode == CardSlotDisplayMode.Board)
                {
                    var dragHandler = slot.RootRect.GetComponent<BoardCardDragHandler>();
                    dragHandler.Configure(index, this);
                }
                else if (mode == CardSlotDisplayMode.Hand)
                {
                    var handDragHandler = slot.RootRect.GetComponent<HandCardDragHandler>();
                    handDragHandler.Configure(-1, index, this);
                }

                slots.Add(slot);
            }
        }

        private static CardSlotView CreateCardSlot(
            Transform parent,
            string name,
            Vector2 position,
            CardSlotDisplayMode mode,
            UnityEngine.Events.UnityAction onClick,
            CardInspectOverlay inspectOverlay,
            bool includeCombatMotion = false)
        {
            var rootGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            rootGo.transform.SetParent(parent, false);

            var rect = rootGo.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = CardSlotSize;

            var frameImage = rootGo.GetComponent<Image>();
            frameImage.color = new Color(0f, 0f, 0f, 0f);
            frameImage.raycastTarget = true;

            var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(rootGo.transform, false);
            var artRect = artGo.GetComponent<RectTransform>();
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;
            var artImage = artGo.GetComponent<Image>();
            artImage.preserveAspect = true;
            artImage.raycastTarget = false;
            artImage.enabled = false;

            CardIdleMotion idleMotion = null;
            if (mode == CardSlotDisplayMode.Board || mode == CardSlotDisplayMode.Combat)
            {
                idleMotion = artGo.AddComponent<CardIdleMotion>();
                idleMotion.SetActiveMotion(false);
            }

            if (mode == CardSlotDisplayMode.Hand)
            {
                rootGo.AddComponent<HandCardLift>();
                rootGo.AddComponent<HandCardDragHandler>();
            }

            if (mode == CardSlotDisplayMode.Board)
            {
                rootGo.AddComponent<BoardCardDragHandler>();
            }

            CombatMinionMotion combatMotion = null;
            if (includeCombatMotion)
            {
                combatMotion = rootGo.AddComponent<CombatMinionMotion>();
                combatMotion.CacheBase();
            }

            var statsGo = new GameObject("Stats", typeof(RectTransform), typeof(Image));
            statsGo.transform.SetParent(rootGo.transform, false);
            var statsRect = statsGo.GetComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0f, 0f);
            statsRect.anchorMax = new Vector2(1f, 0.22f);
            statsRect.offsetMin = Vector2.zero;
            statsRect.offsetMax = Vector2.zero;
            statsGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            statsGo.SetActive(false);

            var attackText = CreateCardStatOverlay(artGo.transform, "AttackText", new Vector2(0.12f, 0.11f));
            var healthText = CreateCardStatOverlay(artGo.transform, "HealthText", new Vector2(0.88f, 0.11f));

            var statsTextGo = new GameObject("StatsText", typeof(RectTransform), typeof(TextMeshProUGUI));
            statsTextGo.transform.SetParent(statsGo.transform, false);
            var statsTextRect = statsTextGo.GetComponent<RectTransform>();
            statsTextRect.anchorMin = Vector2.zero;
            statsTextRect.anchorMax = Vector2.one;
            statsTextRect.offsetMin = Vector2.zero;
            statsTextRect.offsetMax = Vector2.zero;

            var statsText = statsTextGo.GetComponent<TextMeshProUGUI>();
            statsText.fontSize = 13;
            statsText.alignment = TextAlignmentOptions.Center;
            statsText.color = Color.white;
            statsText.text = name;

            var inspectHandler = rootGo.AddComponent<CardInspectHandler>();
            var button = rootGo.GetComponent<Button>();
            CardSlotView slotView = null;
            slotView = new CardSlotView(
                button,
                frameImage,
                artImage,
                statsText,
                attackText,
                healthText,
                idleMotion,
                combatMotion,
                inspectHandler,
                rect);
            inspectHandler.Configure(inspectOverlay, () => slotView.BuildInspectPayload());
            button.onClick.AddListener(() =>
            {
                if (inspectHandler.ConsumeSuppressClick())
                {
                    return;
                }

                onClick();
            });

            return slotView;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 pos, int fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(900, 120);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.text = name;
            return text;
        }

        private static void CreateSectionLabel(Transform parent, string label, Vector2 pos)
        {
            var text = CreateText(parent, label + "Label", pos, 22, TextAlignmentOptions.Left);
            text.text = label;
            text.rectTransform.sizeDelta = new Vector2(200, 36);
        }

        private static TextMeshProUGUI CreateCardStatOverlay(Transform parent, string name, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(48f, 48f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = 24;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineWidth = 0.35f;
            text.outlineColor = Color.black;
            text.gameObject.SetActive(false);
            return text;
        }

        private static Button CreateSpriteButton(
            Transform parent,
            string resourceName,
            Vector2 pos,
            Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            var sprite = Resources.Load<Sprite>(resourceName);
            var go = new GameObject(resourceName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
            }
            else
            {
                image.color = new Color(0.15f, 0.2f, 0.35f, 0.92f);
            }

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            return button;
        }

        private static TextMeshProUGUI CreateButtonOverlayLabel(Transform parent, string label)
        {
            var textGo = new GameObject("OverlayLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(parent, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineWidth = 0.2f;
            text.outlineColor = Color.black;
            return text;
        }

        private static Button CreateActionButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(240, 58);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.15f, 0.2f, 0.35f, 0.92f);

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.fontSize = 15;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.text = label;
            return button;
        }
    }

    /// <summary>
    /// Hold (mobile) or hover (desktop) to magnify a card without triggering a quick tap/click.
    /// </summary>
    public class CardInspectHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
    {
        [SerializeField] private float holdDuration = 0.32f;
        [SerializeField] private float hoverDelay = 0.24f;

        private CardInspectOverlay overlay;
        private Func<CardInspectPayload> getPayload;
        private bool inspectable = true;
        private bool pointerDown;
        private bool inspectVisible;
        private bool suppressClick;
        private bool hoverEligible;
        private float holdTimer;
        private float hoverTimer;

        public void Configure(CardInspectOverlay inspectOverlay, Func<CardInspectPayload> payloadProvider)
        {
            overlay = inspectOverlay;
            getPayload = payloadProvider;
        }

        public void SetInspectable(bool enabled)
        {
            inspectable = enabled;
            if (!inspectable)
            {
                CancelInspect();
            }
        }

        public bool ConsumeSuppressClick()
        {
            if (!suppressClick)
            {
                return false;
            }

            suppressClick = false;
            return true;
        }

        public void SuppressNextClick()
        {
            suppressClick = true;
        }

        public void CancelInspect()
        {
            pointerDown = false;
            holdTimer = 0f;
            hoverEligible = false;
            hoverTimer = 0f;
            HideInspect();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanInspect())
            {
                return;
            }

            PauseHandLift();
            pointerDown = true;
            holdTimer = 0f;
            hoverEligible = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (inspectVisible)
            {
                suppressClick = true;
                HideInspect();
            }

            pointerDown = false;
            holdTimer = 0f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverEligible = false;
            hoverTimer = 0f;

            if (!pointerDown)
            {
                HideInspect();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanInspect() || !UseHoverInspect())
            {
                return;
            }

            hoverEligible = true;
            hoverTimer = 0f;
        }

        private void Update()
        {
            if (!CanInspect())
            {
                return;
            }

            if (pointerDown && !inspectVisible)
            {
                holdTimer += Time.unscaledDeltaTime;
                if (holdTimer >= holdDuration)
                {
                    ShowInspect();
                }
            }

            if (hoverEligible && !pointerDown && !inspectVisible && UseHoverInspect())
            {
                hoverTimer += Time.unscaledDeltaTime;
                if (hoverTimer >= hoverDelay)
                {
                    ShowInspect();
                }
            }
        }

        private void OnDisable()
        {
            CancelInspect();
        }

        private bool CanInspect()
        {
            return inspectable && overlay != null && getPayload != null;
        }

        private void ShowInspect()
        {
            var payload = getPayload();
            if (!payload.hasCard)
            {
                return;
            }

            inspectVisible = true;
            overlay.Show(payload);
        }

        private void HideInspect()
        {
            if (!inspectVisible)
            {
                return;
            }

            inspectVisible = false;
            overlay.Hide();
            ResumeHandLift();
        }

        private void PauseHandLift()
        {
            var lift = GetComponent<HandCardLift>();
            if (lift == null)
            {
                return;
            }

            lift.PauseForInspect();
        }

        private void ResumeHandLift()
        {
            var lift = GetComponent<HandCardLift>();
            if (lift == null)
            {
                return;
            }

            lift.ResumeAfterInspect();
        }

        private static bool UseHoverInspect()
        {
            return !Application.isMobilePlatform;
        }
    }

    /// <summary>
    /// Full-screen magnified card preview for hold/hover inspect.
    /// </summary>
    public class CardInspectOverlay : MonoBehaviour
    {
        private static readonly Vector2 InspectCardSize = new(429, 582);
        private const float InspectShowScale = 1.5f;
        private const float InspectHideScale = 1.35f;
        private static readonly Color DefaultFrameColor = new(0.12f, 0.16f, 0.26f, 0.95f);
        private static readonly Color GoldenFrameColor = new(0.45f, 0.35f, 0.08f, 0.95f);

        private GameObject root;
        private RectTransform cardRect;
        private Image frameImage;
        private Image artImage;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI subtitleText;
        private TextMeshProUGUI bodyText;
        private CanvasGroup canvasGroup;
        private Coroutine animationRoutine;

        public void Initialize(Transform uiRoot)
        {
            root = new GameObject("CardInspectOverlay", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(uiRoot, false);

            var overlayRect = root.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            root.SetActive(false);

            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(root.transform, false);
            var backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            backdrop.GetComponent<Image>().raycastTarget = false;

            var cardGo = new GameObject("InspectCard", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(root.transform, false);
            cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = InspectCardSize;
            frameImage = cardGo.GetComponent<Image>();
            frameImage.color = DefaultFrameColor;

            var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(cardGo.transform, false);
            var artRect = artGo.GetComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0.02f, 0.02f);
            artRect.anchorMax = new Vector2(0.98f, 0.98f);
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;
            artImage = artGo.GetComponent<Image>();
            artImage.preserveAspect = true;
            artImage.raycastTarget = false;

            titleText = CreateInspectText(cardGo.transform, "Title", new Vector2(0, -18), 30, 0.9f, 0.98f, 34);
            subtitleText = CreateInspectText(cardGo.transform, "Subtitle", new Vector2(0, -58), 22, 0.82f, 0.9f, 30);
            bodyText = CreateInspectText(cardGo.transform, "Body", new Vector2(0, 0), 18, 0.04f, 0.3f, 120);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.margin = new Vector4(14f, 10f, 14f, 10f);
        }

        public void Show(CardInspectPayload payload)
        {
            if (root == null)
            {
                return;
            }

            titleText.text = payload.title;
            subtitleText.text = payload.subtitle;
            var hasBody = !string.IsNullOrWhiteSpace(payload.body);
            bodyText.text = hasBody ? payload.body : string.Empty;
            bodyText.gameObject.SetActive(hasBody);
            frameImage.color = payload.isGolden ? GoldenFrameColor : DefaultFrameColor;

            if (payload.art != null)
            {
                artImage.sprite = payload.art;
                artImage.color = Color.white;
                artImage.enabled = true;
            }
            else
            {
                artImage.sprite = null;
                artImage.enabled = false;
            }

            root.transform.SetAsLastSibling();
            root.SetActive(true);
            cardRect.localScale = Vector3.one * InspectHideScale;
            RestartAnimation(true);
        }

        public void Hide()
        {
            if (root == null || !root.activeSelf)
            {
                return;
            }

            RestartAnimation(false);
        }

        private void RestartAnimation(bool show)
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            animationRoutine = StartCoroutine(AnimateVisibility(show));
        }

        private IEnumerator AnimateVisibility(bool show)
        {
            const float duration = 0.12f;
            var startAlpha = canvasGroup.alpha;
            var targetAlpha = show ? 1f : 0f;
            var startScale = cardRect.localScale.x;
            var targetScale = show ? InspectShowScale : InspectHideScale;
            var elapsed = 0f;

            canvasGroup.blocksRaycasts = show;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = show ? Mathf.SmoothStep(0f, 1f, t) : 1f - Mathf.SmoothStep(0f, 1f, t);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
                var scale = Mathf.Lerp(startScale, targetScale, eased);
                cardRect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            cardRect.localScale = Vector3.one * targetScale;

            if (!show)
            {
                root.SetActive(false);
            }

            animationRoutine = null;
        }

        private static TextMeshProUGUI CreateInspectText(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            int fontSize,
            float anchorMinY,
            float anchorMaxY,
            float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, anchorMinY);
            rect.anchorMax = new Vector2(0.95f, anchorMaxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(0f, height);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.text = name;
            return text;
        }
    }

    internal static class UiImageSprites
    {
        private static Sprite solidUiSprite;

        public static Sprite GetSolid()
        {
            if (solidUiSprite != null)
            {
                return solidUiSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            solidUiSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return solidUiSprite;
        }
    }

    internal sealed class HeroPortraitSlot
    {
        public RectTransform Root { get; }
        public Image PortraitImage { get; }
        public TextMeshProUGUI NameText { get; }
        public TextMeshProUGUI HpText { get; }

        public HeroPortraitSlot(
            RectTransform root,
            Image portraitImage,
            TextMeshProUGUI nameText,
            TextMeshProUGUI hpText)
        {
            Root = root;
            PortraitImage = portraitImage;
            NameText = nameText;
            HpText = hpText;
        }

        public void SetShopkeeper()
        {
            NameText.text = HeroRegistry.ShopkeeperHeroName;
            HpText.text = "Shop";

            var shopKeeperSprite = Resources.Load<Sprite>("ShopKeeper");
            if (shopKeeperSprite != null)
            {
                PortraitImage.sprite = shopKeeperSprite;
                PortraitImage.color = Color.white;
                PortraitImage.enabled = true;
                return;
            }

            SetHero(HeroRegistry.ShopkeeperHeroName, HeroRegistry.ShopkeeperHeroId, -1);
            HpText.text = "Shop";
        }

        public void SetHero(string heroName, string heroId, int heroHealth, bool combatDisplay = false)
        {
            NameText.text = heroName;
            HpText.text = heroHealth < 0 ? "Shop" : $"{heroHealth} HP";
            HpText.fontSize = combatDisplay ? 22 : 15;
            HpText.fontStyle = combatDisplay ? FontStyles.Bold : FontStyles.Normal;
            HpText.gameObject.SetActive(heroHealth >= 0 || combatDisplay);

            var portrait = HeroRegistry.LoadPortrait(heroId);
            if (portrait != null)
            {
                PortraitImage.sprite = portrait;
                PortraitImage.color = Color.white;
                PortraitImage.enabled = true;
            }
            else
            {
                PortraitImage.sprite = UiImageSprites.GetSolid();
                PortraitImage.color = new Color(0.2f, 0.35f, 0.65f, 0.55f);
                PortraitImage.enabled = true;
            }
        }
    }

    /// <summary>
    /// Drag board minions between slots during the recruit phase.
    /// </summary>
    public class BoardCardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float DragScale = 1.08f;
        private const float DragAlpha = 0.72f;
        private const float DragSuppressDistance = 12f;

        private PracticeGameUI ui;
        private CardInspectHandler inspectHandler;
        private CardIdleMotion idleMotion;
        private CanvasGroup canvasGroup;
        private RectTransform rect;
        private Transform dragLayer;
        private Transform originalParent;
        private Vector2 basePosition;
        private Vector2 dragOffset;
        private Vector3 baseScale;
        private int originalSiblingIndex;
        private int slotIndex;
        private bool dragging;

        public void Configure(int index, PracticeGameUI owner)
        {
            slotIndex = index;
            ui = owner;
            inspectHandler = GetComponent<CardInspectHandler>();
            idleMotion = GetComponentInChildren<CardIdleMotion>();
            rect = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            dragLayer = owner.GetBoardDragLayer();
            CacheBase();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (ui == null || !ui.CanDragBoardSlot(slotIndex))
            {
                return;
            }

            dragging = true;
            inspectHandler?.CancelInspect();
            idleMotion?.SetActiveMotion(false);

            originalParent = rect.parent;
            originalSiblingIndex = rect.GetSiblingIndex();
            basePosition = rect.anchoredPosition;
            baseScale = rect.localScale;

            rect.SetParent(dragLayer, true);
            rect.SetAsLastSibling();
            rect.localScale = baseScale * DragScale;
            canvasGroup.alpha = DragAlpha;
            canvasGroup.blocksRaycasts = false;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
            {
                dragOffset = rect.anchoredPosition - localPoint;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
            {
                rect.anchoredPosition = localPoint + dragOffset;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            var dragged = Vector2.Distance(eventData.pressPosition, eventData.position) >= DragSuppressDistance;
            var dropIndex = ui.GetBoardDropSlotIndex(eventData.position, eventData.pressEventCamera);
            if (dropIndex >= 0 && dropIndex != slotIndex)
            {
                ui.TryBoardReorder(slotIndex, dropIndex);
                inspectHandler?.SuppressNextClick();
            }
            else if (dragged)
            {
                inspectHandler?.SuppressNextClick();
            }

            rect.SetParent(originalParent, false);
            rect.SetSiblingIndex(originalSiblingIndex);
            rect.anchoredPosition = basePosition;
            rect.localScale = baseScale;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            idleMotion?.SetActiveMotion(true);
        }

        private void OnDisable()
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            if (originalParent != null)
            {
                rect.SetParent(originalParent, false);
                rect.SetSiblingIndex(originalSiblingIndex);
            }

            rect.anchoredPosition = basePosition;
            rect.localScale = baseScale;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        private void CacheBase()
        {
            if (rect == null)
            {
                rect = GetComponent<RectTransform>();
            }

            basePosition = rect.anchoredPosition;
            baseScale = rect.localScale;
        }
    }

    /// <summary>
    /// Drag hand cards onto empty board slots during the recruit phase.
    /// </summary>
    public class HandCardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float DragScale = 1.08f;
        private const float DragSuppressDistance = 12f;

        private PracticeGameUI ui;
        private CardInspectHandler inspectHandler;
        private HandCardLift handLift;
        private CanvasGroup canvasGroup;
        private RectTransform rect;
        private Transform dragLayer;
        private Transform originalParent;
        private Vector2 basePosition;
        private Vector2 dragOffset;
        private Vector3 baseScale;
        private int originalSiblingIndex;
        private int handIndex;
        private int visualSlotIndex;
        private bool dragging;

        public void Configure(int listIndex, int visualIndex, PracticeGameUI owner)
        {
            handIndex = listIndex;
            visualSlotIndex = visualIndex;
            ui = owner;
            inspectHandler = GetComponent<CardInspectHandler>();
            handLift = GetComponent<HandCardLift>();
            rect = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            dragLayer = owner.GetBoardDragLayer();
            CacheBase();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (ui == null || !ui.CanDragHandSlot(handIndex))
            {
                return;
            }

            dragging = true;
            inspectHandler?.CancelInspect();
            if (handLift != null)
            {
                handLift.enabled = false;
            }

            originalParent = rect.parent;
            originalSiblingIndex = rect.GetSiblingIndex();
            basePosition = rect.anchoredPosition;
            baseScale = rect.localScale;

            rect.SetParent(dragLayer, false);
            rect.SetAsLastSibling();
            rect.localScale = baseScale * DragScale;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;

            var dragLayerRect = dragLayer as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayerRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
            {
                dragOffset = rect.anchoredPosition - localPoint;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayer as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
            {
                rect.anchoredPosition = localPoint + dragOffset;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            var draggedDistance = Vector2.Distance(eventData.pressPosition, eventData.position);
            if (draggedDistance >= DragSuppressDistance)
            {
                inspectHandler?.SuppressNextClick();
                var dropIndex = ui.GetBoardDropSlotIndex(eventData.position, eventData.pressEventCamera, requireEmpty: true);
                if (dropIndex >= 0)
                {
                    ui.TryPlayHandToBoard(handIndex, dropIndex);
                }
            }

            rect.SetParent(originalParent, false);
            rect.SetSiblingIndex(originalSiblingIndex);
            rect.localScale = baseScale;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            if (handLift != null)
            {
                handLift.enabled = true;
                handLift.ResetLift();
            }

            ui.ForceRefreshHand();
        }

        private void OnDisable()
        {
            if (!dragging || rect == null)
            {
                return;
            }

            dragging = false;
            if (originalParent != null)
            {
                rect.SetParent(originalParent, false);
                rect.SetSiblingIndex(originalSiblingIndex);
                rect.anchoredPosition = basePosition;
                rect.localScale = baseScale;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }

            if (handLift != null)
            {
                handLift.enabled = true;
                handLift.ResetLift();
            }
        }

        private void CacheBase()
        {
            basePosition = rect.anchoredPosition;
            baseScale = rect.localScale;
        }
    }

    /// <summary>
    /// Lifts and enlarges a hand card while hovered/held so text is easier to read.
    /// </summary>
    public class HandCardLift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Vector2 LiftOffset = new(0f, 42f * 1.2f);
        private static readonly Vector3 NominalScale = Vector3.one;
        private const float LiftScale = 1.28f;
        private const float HoverDelay = 0.24f;

        private RectTransform rect;
        private Vector2 basePosition;
        private Vector3 baseScale = NominalScale;
        private bool initialized;
        private bool hoverEligible;
        private bool pausedForInspect;
        private float hoverTimer;
        private static HandCardLift activeLift;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            CacheBase();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (pausedForInspect)
            {
                return;
            }

            if (!initialized)
            {
                CacheBase();
            }

            hoverEligible = true;
            hoverTimer = 0f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverEligible = false;
            hoverTimer = 0f;

            if (activeLift == this)
            {
                ResetLift();
            }
        }

        public void PauseForInspect()
        {
            pausedForInspect = true;
            hoverEligible = false;
            hoverTimer = 0f;
            ResetLift();
            enabled = false;
        }

        public void ResumeAfterInspect()
        {
            pausedForInspect = false;
            enabled = true;
            ResetLift();
        }

        private void Update()
        {
            if (pausedForInspect || !hoverEligible || activeLift == this)
            {
                return;
            }

            hoverTimer += Time.unscaledDeltaTime;
            if (hoverTimer < HoverDelay)
            {
                return;
            }

            if (activeLift != null && activeLift != this)
            {
                activeLift.ResetLift();
            }

            activeLift = this;
            rect.anchoredPosition = basePosition + LiftOffset;
            rect.localScale = NominalScale * LiftScale;
            transform.SetAsLastSibling();
        }

        private void OnDisable()
        {
            if (activeLift == this)
            {
                activeLift = null;
            }
        }

        private void CacheBase()
        {
            if (activeLift != this && rect.localScale.x <= NominalScale.x * 1.01f)
            {
                basePosition = rect.anchoredPosition;
            }

            baseScale = NominalScale;
            initialized = true;
        }

        public void RecacheBase()
        {
            if (activeLift == this || rect.localScale.x > NominalScale.x * 1.01f)
            {
                return;
            }

            basePosition = rect.anchoredPosition;
            baseScale = NominalScale;
            initialized = true;
        }

        public void ResetLift()
        {
            if (!initialized)
            {
                CacheBase();
            }

            rect.anchoredPosition = basePosition;
            rect.localScale = NominalScale;
            baseScale = NominalScale;
            if (activeLift == this)
            {
                activeLift = null;
            }
        }
    }
}