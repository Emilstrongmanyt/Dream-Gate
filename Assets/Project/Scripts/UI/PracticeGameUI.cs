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
        private TextMeshProUGUI combatLogText;
        private TextMeshProUGUI playerHeroText;
        private TextMeshProUGUI opponentHeroText;
        private TextMeshProUGUI heroDamageText;
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
        private List<MinionInstance> combatPlayerBoard;
        private List<MinionInstance> combatOpponentBoard;
        private CombatResult activeCombatResult;
        private CardInspectOverlay cardInspectOverlay;
        private Button upgradeButton;
        private Button refreshShopButton;
        private Button endTurnButton;
        private Button menuButton;
        private Button speedButton;
        private Button playAgainButton;
        private readonly StringBuilder logBuilder = new();
        private Action<float> onCombatSpeedChanged;
        private int speedIndex;

        private static readonly float[] SpeedOptions = { 0.5f, 1f, 2f, 3f };
        private const float CardScaleFactor = 1.2f;
        private static readonly Vector2 CardSlotSize = new(132f * CardScaleFactor, 168f * CardScaleFactor);
        private const float ShopSlotSpacing = 148f * CardScaleFactor;
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
        private const int CompactLogMaxLines = 3;
        private const int CompactLogMaxChars = 900;
        private static readonly Color HudPanelColor = new(0.04f, 0.06f, 0.12f, 0.72f);
        private static readonly Color LogPanelColor = new(0.04f, 0.06f, 0.12f, 0.65f);
        private static readonly Color CombatPanelColor = new(0.03f, 0.05f, 0.1f, 0.55f);

        public void Initialize(MatchManager manager, Transform uiRoot)
        {
            matchManager = manager;
            BuildUI(uiRoot);
            matchManager.StateChanged += Refresh;
            matchManager.MessagePosted += AppendLog;
            matchManager.MatchEnded += OnMatchEnded;
            Refresh();
        }

        public void SetCombatSpeedChanged(Action<float> callback)
        {
            onCombatSpeedChanged = callback;
            onCombatSpeedChanged?.Invoke(SpeedOptions[speedIndex]);
        }

        public void BeginCombatPlayback(PlayerState opponent, CombatResult result)
        {
            activeCombatResult = result;
            combatPlayerBoard = CloneBoardMinions(result?.attackerBoardStart);
            combatOpponentBoard = CloneBoardMinions(result?.defenderBoardStart);
            if (combatPlayerBoard.Count == 0)
            {
                combatPlayerBoard = CloneBoardMinions(result?.attackerSnapshot?.board);
            }

            if (combatOpponentBoard.Count == 0)
            {
                combatOpponentBoard = CloneBoardMinions(result?.defenderSnapshot?.board);
            }

            recruitPanel.SetActive(false);
            combatPanel.SetActive(true);
            combatLogText.text = opponent != null
                ? $"COMBAT vs {opponent.heroName}\n"
                : "COMBAT\n";

            var player = result?.attackerSnapshot ?? matchManager.GetHumanPlayer();
            var defender = result?.defenderSnapshot ?? opponent;
            combatPlayerHero?.SetHero(player.heroName, player.heroId, player.heroHealth);
            combatOpponentHero?.SetHero(
                defender?.heroName ?? "Opponent",
                defender?.heroId,
                defender?.heroHealth ?? 0);
            playerHeroText = combatPlayerHero?.NameText;
            opponentHeroText = combatOpponentHero?.NameText;

            RefreshCombatBoards();
            heroDamageText.gameObject.SetActive(false);
            SetRecruitControls(false);
            SetAllSlotButtonsInteractable(false);
        }

        public void ShowCombatLine(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            combatLogText.text += message + "\n";
            AppendLog(message);
        }

        public IEnumerator PlayCombatEvent(CombatEvent combatEvent, float stepSeconds)
        {
            ShowCombatLine(combatEvent.message);

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
                        combatOpponentHero?.SetHero(opponent.heroName, opponent.heroId, remaining);
                    }

                    break;
                case CombatOutcome.DefenderWins:
                    loserRect = playerHeroRect;
                    if (player != null)
                    {
                        var remaining = Mathf.Max(0, player.heroHealth - damage);
                        combatPlayerHero?.SetHero(player.heroName, player.heroId, remaining);
                    }

                    break;
            }

            if (damage <= 0 || loserRect == null)
            {
                yield break;
            }

            heroDamageText.gameObject.SetActive(true);
            heroDamageText.text = $"-{damage}";
            heroDamageText.rectTransform.position = loserRect.position + new Vector3(0f, 36f, 0f);

            var elapsed = 0f;
            const float duration = 0.45f;
            var startPos = loserRect.anchoredPosition;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var shake = Mathf.Sin(elapsed * 48f) * Mathf.Lerp(14f, 0f, elapsed / duration);
                loserRect.anchoredPosition = startPos + new Vector2(shake, 0f);
                yield return null;
            }

            loserRect.anchoredPosition = startPos;
            ShowCombatLine(
                result.outcome == CombatOutcome.AttackerWins
                    ? $"Hero damage: {opponent?.heroName} takes {damage}!"
                    : $"Hero damage: you take {damage}!");

            yield return new WaitForSeconds(0.55f);
            heroDamageText.gameObject.SetActive(false);
        }

        public void EndCombatPlayback()
        {
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
            CreateBackground(root);
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

            CreateSlotRow(recruitPanel.transform, shopSlots, "Shop", ShopRowCenter, 5, ShopSlotSpacing, CardSlotDisplayMode.Shop, OnShopClicked);
            CreateSlotRow(recruitPanel.transform, boardSlots, "Your Army", RecruitPlayerBoardCenter, 5, BoardSlotSpacing, CardSlotDisplayMode.Board, OnBoardClicked);
            CreateSlotRow(recruitPanel.transform, handSlots, "Hand", HandRowCenter, 8, HandSlotSpacing, CardSlotDisplayMode.Hand, OnHandClicked);

            refreshShopButton = CreateActionButton(recruitPanel.transform, "Refresh (1g)", new Vector2(-420, 150), OnRefreshShopClicked);
            upgradeButton = CreateActionButton(recruitPanel.transform, "Upgrade Tavern (4g)", new Vector2(420, 150), OnUpgradeClicked);
            endTurnButton = CreateActionButton(recruitPanel.transform, "End Turn Early", new Vector2(420, 40), OnEndTurnClicked);
            if (matchManager.Mode == MatchMode.Rated)
            {
                endTurnButton.gameObject.SetActive(false);
            }

            speedButton = CreateActionButton(recruitPanel.transform, "Combat Speed: 1x", new Vector2(420, -70), OnSpeedClicked);
            menuButton = CreateActionButton(recruitPanel.transform, "Back", new Vector2(420, -180), () => SceneNavigator.LoadMainMenu());

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
                new Color(0.55f, 0.35f, 0.2f, 0.55f));
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
                new Color(0.2f, 0.35f, 0.65f, 0.55f));
            playerHeroRect = combatPlayerHero.Root;
            playerHeroText = combatPlayerHero.NameText;

            var combatLogPanel = CreatePanel(combatPanel.transform, "CombatLog", new Color(0.04f, 0.06f, 0.12f, 0.82f));
            var logRect = combatLogPanel.GetComponent<RectTransform>();
            logRect.anchorMin = new Vector2(0.5f, 0f);
            logRect.anchorMax = new Vector2(0.5f, 0f);
            logRect.pivot = new Vector2(0.5f, 0f);
            logRect.anchoredPosition = new Vector2(-220, 24);
            logRect.sizeDelta = new Vector2(520, 150);

            combatLogText = CreateText(combatLogPanel.transform, "CombatLogText", Vector2.zero, 15, TextAlignmentOptions.TopLeft);
            combatLogText.rectTransform.anchorMin = Vector2.zero;
            combatLogText.rectTransform.anchorMax = Vector2.one;
            combatLogText.rectTransform.offsetMin = new Vector2(14, 10);
            combatLogText.rectTransform.offsetMax = new Vector2(-14, -10);

            heroDamageText = CreateText(combatPanel.transform, "HeroDamage", Vector2.zero, 34, TextAlignmentOptions.Center);
            heroDamageText.color = new Color(1f, 0.35f, 0.3f);
            heroDamageText.rectTransform.sizeDelta = new Vector2(160, 60);
            heroDamageText.gameObject.SetActive(false);
        }

        // Hero ovals use UiImageSprites (runtime 1x1 sprite) — no builtin UISprite.psd.
        private static HeroPortraitSlot CreateHeroOvalPortrait(
            Transform parent,
            string name,
            Vector2 position,
            Color placeholderColor)
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
            hpText.fontSize = 15;
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

        private static void RefreshCombatRow(List<CardSlotView> slots, List<MinionInstance> board)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (board == null || i >= board.Count)
                {
                    slots[i].SetEmpty("—", false);
                    continue;
                }

                var minion = board[i];
                var card = CardRegistry.Get(minion.cardId);
                slots[i].SetCombatMinion(card, minion);
            }
        }

        private static List<MinionInstance> CloneBoardMinions(List<MinionInstance> board)
        {
            var clone = new List<MinionInstance>();
            if (board == null)
            {
                return clone;
            }

            foreach (var minion in board)
            {
                clone.Add(minion.Clone());
            }

            return clone;
        }

        private IEnumerator PlayAttackEvent(CombatEvent combatEvent)
        {
            if (combatEvent.attackerBoardIndex < 0 || combatEvent.defenderBoardIndex < 0)
            {
                yield break;
            }

            var striker = GetCombatMinion(combatEvent.isAttackerBoard, combatEvent.attackerBoardIndex);
            var target = GetCombatMinion(!combatEvent.isAttackerBoard, combatEvent.defenderBoardIndex);
            if (striker == null || target == null || striker.isDead || target.isDead)
            {
                yield break;
            }

            target.health -= striker.attack;
            RefreshCombatSlot(
                combatEvent.isAttackerBoard ? playerCombatSlots : opponentCombatSlots,
                combatEvent.isAttackerBoard ? combatPlayerBoard : combatOpponentBoard,
                combatEvent.attackerBoardIndex);
            RefreshCombatSlot(
                combatEvent.isAttackerBoard ? opponentCombatSlots : playerCombatSlots,
                combatEvent.isAttackerBoard ? combatOpponentBoard : combatPlayerBoard,
                combatEvent.defenderBoardIndex);

            var strikerSlot = GetCombatSlot(combatEvent.isAttackerBoard, combatEvent.attackerBoardIndex);
            var targetSlot = GetCombatSlot(!combatEvent.isAttackerBoard, combatEvent.defenderBoardIndex);
            var lungeDirection = combatEvent.isAttackerBoard ? Vector2.up : Vector2.down;

            if (strikerSlot?.CombatMotion != null && targetSlot?.CombatMotion != null)
            {
                var lunge = strikerSlot.CombatMotion.PlayAttackLunge(lungeDirection);
                var hit = targetSlot.CombatMotion.PlayHitShake();
                yield return lunge;
                yield return hit;
            }
            else
            {
                yield return new WaitForSeconds(0.2f);
            }
        }

        private IEnumerator PlayDeathEvent(CombatEvent combatEvent)
        {
            if (combatEvent.boardIndex < 0)
            {
                yield return new WaitForSeconds(0.15f);
                yield break;
            }

            var slots = combatEvent.isAttackerBoard ? playerCombatSlots : opponentCombatSlots;
            var board = combatEvent.isAttackerBoard ? combatPlayerBoard : combatOpponentBoard;
            if (board == null || combatEvent.boardIndex >= board.Count)
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
            if (board == null || boardIndex < 0 || boardIndex >= board.Count)
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

        private static void RefreshCombatSlot(List<CardSlotView> slots, List<MinionInstance> board, int index)
        {
            if (board == null || index < 0 || index >= board.Count || index >= slots.Count)
            {
                return;
            }

            var minion = board[index];
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
            if (board == null || combatEvent.boardIndex >= board.Count)
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

            if (combatEvent.boardIndex >= 0 && combatEvent.boardIndex < board.Count)
            {
                var minion = board[combatEvent.boardIndex];
                minion.cardId = definition.cardId;
                minion.attack = definition.attack;
                minion.health = definition.health;
                minion.maxHealth = definition.health;
                RefreshCombatSlot(slots, board, combatEvent.boardIndex);
                return;
            }

            if (board.Count < slots.Count)
            {
                board.Add(MinionInstance.FromDefinition(definition));
                RefreshCombatBoards();
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

        private void CreateBackground(Transform root)
        {
            var bgGo = new GameObject("BoardBackground", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(root, false);
            bgGo.transform.SetAsFirstSibling();

            var rect = bgGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = bgGo.GetComponent<Image>();
            image.sprite = CardArtLoader.LoadBackground("PracticeGameBackground1");
            image.preserveAspect = false;
            image.raycastTarget = false;
            image.color = Color.white;
        }

        private void CreateHud(Transform root)
        {
            hudPanel = CreatePanel(root, "HudPanel", HudPanelColor);
            var hudRect = hudPanel.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0.5f, 1f);
            hudRect.anchorMax = new Vector2(0.5f, 1f);
            hudRect.pivot = new Vector2(0.5f, 1f);
            hudRect.anchoredPosition = new Vector2(0, -12);
            hudRect.sizeDelta = new Vector2(980, 248);

            hudText = CreateText(hudPanel.transform, "HUD", new Vector2(0, -24), 24, TextAlignmentOptions.Center);
            hudText.rectTransform.sizeDelta = new Vector2(940, 84);

            leaderboardText = CreateText(hudPanel.transform, "Leaderboard", new Vector2(0, -104), 16, TextAlignmentOptions.Center);
            leaderboardText.rectTransform.sizeDelta = new Vector2(940, 38);
            leaderboardText.color = new Color(0.85f, 0.9f, 1f);

            var compactLogPanel = new GameObject("CompactLog", typeof(RectTransform), typeof(Image));
            compactLogPanel.transform.SetParent(hudPanel.transform, false);
            var compactLogRect = compactLogPanel.GetComponent<RectTransform>();
            compactLogRect.anchorMin = new Vector2(0.5f, 0f);
            compactLogRect.anchorMax = new Vector2(0.5f, 0f);
            compactLogRect.pivot = new Vector2(0.5f, 0f);
            compactLogRect.anchoredPosition = new Vector2(0, 10);
            compactLogRect.sizeDelta = new Vector2(920, 72);
            compactLogPanel.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.58f);

            logText = CreateText(compactLogPanel.transform, "Log", Vector2.zero, 13, TextAlignmentOptions.TopLeft);
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
            var modeLabel = matchManager.Mode == MatchMode.Rated ? "RATED" : "PRACTICE";
            hudText.text =
                $"{modeLabel} | Turn {matchManager.Turn} | {player.heroName} | Phase: {matchManager.Phase}\n" +
                $"Gold: {player.gold} | Tavern Tier: {player.tavernTier}/{MatchConfig.MaxTavernTier} | HP: {player.heroHealth}";

            if (matchManager.Mode == MatchMode.Rated && DreamGateServices.IsInitialized && DreamGateServices.IsLoggedIn && DreamGateServices.Profile != null)
            {
                hudText.text += $"\nMMR: {DreamGateServices.Profile.mmr}";
            }

            if (matchManager.Phase == MatchPhase.Recruit)
            {
                hudText.text += $"\nTimer: {Mathf.CeilToInt(matchManager.RecruitTimeRemaining)}s";
            }

            leaderboardText.text = matchManager.GetLeaderboardSummary();
            RefreshRecruitHeroes(player);

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
                    shopSlots[i].SetEmpty("Empty", false);
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
                if (i >= player.board.Count)
                {
                    boardSlots[i].SetEmpty($"Slot {i + 1}", false);
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

        private void RefreshHand(PlayerState player)
        {
            for (var i = 0; i < handSlots.Count; i++)
            {
                if (i >= player.hand.Count)
                {
                    handSlots[i].SetEmpty($"Hand {i + 1}", false);
                    continue;
                }

                var minion = player.hand[i];
                var card = CardRegistry.Get(minion.cardId);
                handSlots[i].SetMinionCard(card, minion, CardSlotDisplayMode.Hand, matchManager.Phase == MatchPhase.Recruit);
            }
        }

        private void OnShopClicked(int index)
        {
            matchManager.TryBuyFromShop(index, out var message);
            AppendLog(message);
        }

        private void OnBoardClicked(int index)
        {
            matchManager.TrySellFromBoard(index, out var message);
            AppendLog(message);
        }

        private void OnHandClicked(int index)
        {
            matchManager.TryPlayFromHand(index, out var message);
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

        private void OnSpeedClicked()
        {
            speedIndex = (speedIndex + 1) % SpeedOptions.Length;
            var speed = SpeedOptions[speedIndex];
            speedButton.GetComponentInChildren<TextMeshProUGUI>().text = $"Combat Speed: {speed}x";
            onCombatSpeedChanged?.Invoke(speed);
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
            bool includeCombatMotion = false)
        {
            slots.Clear();
            var totalWidth = (count - 1) * spacing;
            var startX = center.x - totalWidth * 0.5f;

            CreateSectionLabel(parent, label, new Vector2(-470, center.y + RowLabelOffsetY));

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
            frameImage.color = new Color(0.12f, 0.16f, 0.26f, 0.75f);
            frameImage.raycastTarget = true;

            var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(rootGo.transform, false);
            var artRect = artGo.GetComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0.05f, 0.22f);
            artRect.anchorMax = new Vector2(0.95f, 0.98f);
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
            slotView = new CardSlotView(button, frameImage, artImage, statsText, idleMotion, combatMotion, inspectHandler, rect);
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

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanInspect())
            {
                return;
            }

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
        }

        private void CancelInspect()
        {
            pointerDown = false;
            holdTimer = 0f;
            hoverEligible = false;
            hoverTimer = 0f;
            HideInspect();
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
        private static readonly Vector2 InspectCardSize = new(286, 388);
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
            artRect.anchorMin = new Vector2(0.05f, 0.34f);
            artRect.anchorMax = new Vector2(0.95f, 0.98f);
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
            bodyText.text = payload.body;
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
            var targetScale = show ? 1f : 0.9f;
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
        private readonly TextMeshProUGUI hpText;

        public HeroPortraitSlot(
            RectTransform root,
            Image portraitImage,
            TextMeshProUGUI nameText,
            TextMeshProUGUI hpText)
        {
            Root = root;
            PortraitImage = portraitImage;
            NameText = nameText;
            this.hpText = hpText;
        }

        public void SetShopkeeper()
        {
            SetHero(HeroRegistry.ShopkeeperHeroName, HeroRegistry.ShopkeeperHeroId, -1);
            hpText.text = "Shop";
        }

        public void SetHero(string heroName, string heroId, int heroHealth)
        {
            NameText.text = heroName;
            hpText.text = heroHealth < 0 ? "Shop" : $"{heroHealth} HP";

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
    /// Lifts and enlarges a hand card while hovered/held so text is easier to read.
    /// </summary>
    public class HandCardLift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Vector2 LiftOffset = new(0f, 42f * 1.2f);
        private const float LiftScale = 1.28f;
        private const float HoverDelay = 0.24f;

        private RectTransform rect;
        private Vector2 basePosition;
        private Vector3 baseScale;
        private bool initialized;
        private bool hoverEligible;
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

        private void Update()
        {
            if (!hoverEligible || activeLift == this)
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
            rect.localScale = baseScale * LiftScale;
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
            basePosition = rect.anchoredPosition;
            baseScale = rect.localScale;
            initialized = true;
        }

        private void ResetLift()
        {
            rect.anchoredPosition = basePosition;
            rect.localScale = baseScale;
            if (activeLift == this)
            {
                activeLift = null;
            }
        }
    }
}