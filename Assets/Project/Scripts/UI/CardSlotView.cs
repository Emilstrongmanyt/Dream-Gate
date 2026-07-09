using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.UI
{
    public struct CardInspectPayload
    {
        public bool hasCard;
        public string title;
        public string subtitle;
        public string body;
        public Sprite art;
        public bool isGolden;
        public int attack;
        public int health;
        public bool showStats;

        public static CardInspectPayload Empty => new CardInspectPayload { hasCard = false };
    }

    public enum CardSlotDisplayMode
    {
        Shop,
        Board,
        Hand,
        Combat
    }

    public class CardSlotView
    {
        public Button Button { get; }
        public Image FrameImage { get; }
        public Image ArtImage { get; }
        public TextMeshProUGUI StatsText { get; }
        public TextMeshProUGUI AttackText { get; }
        public TextMeshProUGUI HealthText { get; }
        public CardIdleMotion IdleMotion { get; }
        public CombatMinionMotion CombatMotion { get; }
        public CardInspectHandler InspectHandler { get; }
        public RectTransform RootRect { get; }

        private string lastCardKey = string.Empty;
        private bool hasArt;
        private CardInspectPayload inspectPayload = CardInspectPayload.Empty;

        public CardSlotView(
            Button button,
            Image frameImage,
            Image artImage,
            TextMeshProUGUI statsText,
            TextMeshProUGUI attackText,
            TextMeshProUGUI healthText,
            CardIdleMotion idleMotion,
            CombatMinionMotion combatMotion,
            CardInspectHandler inspectHandler,
            RectTransform rootRect)
        {
            Button = button;
            FrameImage = frameImage;
            ArtImage = artImage;
            StatsText = statsText;
            AttackText = attackText;
            HealthText = healthText;
            IdleMotion = idleMotion;
            CombatMotion = combatMotion;
            InspectHandler = inspectHandler;
            RootRect = rootRect;
        }

        public CardInspectPayload BuildInspectPayload()
        {
            return inspectPayload;
        }

        public void SetEmpty(string label, bool interactable)
        {
            lastCardKey = string.Empty;
            inspectPayload = CardInspectPayload.Empty;
            InspectHandler?.SetInspectable(false);
            StatsText.text = string.Empty;
            StatsText.gameObject.SetActive(false);
            ClearArt();
            SetTransparentFrame();
            HideStatOverlays();
            IdleMotion?.SetActiveMotion(false);
            Button.interactable = interactable;
        }

        public void SetShopCard(MinionCardDefinition card, bool interactable)
        {
            if (card == null)
            {
                SetEmpty(string.Empty, false);
                return;
            }

            var cardKey = $"shop:{card.cardId}";
            ApplyArt(card, cardKey);
            StatsText.gameObject.SetActive(false);
            if (card.cardKind == CardKind.Spell)
            {
                HideStatOverlays();
            }
            else
            {
                SetStatOverlays(card.attack, card.health);
            }
            SetTransparentFrame();
            IdleMotion?.SetActiveMotion(false);
            Button.interactable = interactable;
            inspectPayload = BuildShopInspectPayload(card);
            InspectHandler?.SetInspectable(true);
        }

        public void SetMinionCard(MinionCardDefinition card, MinionInstance minion, CardSlotDisplayMode mode, bool interactable)
        {
            if (minion == null)
            {
                SetEmpty(string.Empty, false);
                return;
            }

            var cardKey = $"{mode}:{minion.instanceId}:{minion.cardId}:{minion.attack}:{minion.health}:{minion.isGolden}";

            ApplyArt(card, cardKey);
            StatsText.gameObject.SetActive(false);
            if (card != null && card.cardKind == CardKind.Spell)
            {
                HideStatOverlays();
            }
            else
            {
                SetStatOverlays(minion.attack, minion.health);
            }

            SetTransparentFrame();

            if (IdleMotion != null)
            {
                if (cardKey != lastCardKey)
                {
                    IdleMotion.Configure(minion.cardId);
                }

                IdleMotion.SetActiveMotion(mode == CardSlotDisplayMode.Board);
            }

            Button.interactable = interactable;
            inspectPayload = BuildMinionInspectPayload(card, minion, mode);
            InspectHandler?.SetInspectable(true);
        }

        public void SetCombatMinion(MinionCardDefinition card, MinionInstance minion)
        {
            if (minion == null || minion.isDead)
            {
                SetEmpty("—", false);
                CombatMotion?.ResetVisual();
                return;
            }

            var cardKey = $"combat:{minion.instanceId}:{minion.cardId}:{minion.attack}:{minion.health}:{minion.isGolden}";

            ApplyArt(card, cardKey);
            StatsText.gameObject.SetActive(false);
            SetStatOverlays(minion.attack, minion.health);
            SetTransparentFrame();
            Button.interactable = false;

            if (IdleMotion != null)
            {
                if (cardKey != lastCardKey)
                {
                    IdleMotion.Configure(minion.cardId);
                }

                IdleMotion.SetActiveMotion(true);
            }

            CombatMotion?.ResetVisual();
            RootRect.localScale = Vector3.one;
            inspectPayload = BuildMinionInspectPayload(card, minion, CardSlotDisplayMode.Board);
            InspectHandler?.SetInspectable(true);
        }

        public void SetDefeated()
        {
            inspectPayload = CardInspectPayload.Empty;
            InspectHandler?.SetInspectable(false);
            IdleMotion?.SetActiveMotion(false);
            SetTransparentFrame();
            HideStatOverlays();
            if (hasArt)
            {
                ArtImage.color = new Color(0.45f, 0.45f, 0.45f, 0.45f);
            }
        }

        private void ApplyArt(MinionCardDefinition card, string cardKey)
        {
            lastCardKey = cardKey;
            var sprite = card?.cardArt;
            if (sprite == null && card != null)
            {
                sprite = CardRegistry.Get(card.cardId)?.cardArt;
            }

            if (sprite != null)
            {
                ArtImage.sprite = sprite;
                ArtImage.color = Color.white;
                ArtImage.enabled = true;
                hasArt = true;
            }
            else
            {
                ClearArt();
            }
        }

        private void ClearArt()
        {
            ArtImage.sprite = null;
            ArtImage.enabled = false;
            hasArt = false;
        }

        private void SetTransparentFrame()
        {
            FrameImage.color = new Color(0f, 0f, 0f, 0f);
        }

        private void SetStatOverlays(int attack, int health)
        {
            AttackText.text = attack.ToString();
            HealthText.text = health.ToString();
            AttackText.gameObject.SetActive(true);
            HealthText.gameObject.SetActive(true);
        }

        private void HideStatOverlays()
        {
            AttackText.text = string.Empty;
            HealthText.text = string.Empty;
            AttackText.gameObject.SetActive(false);
            HealthText.gameObject.SetActive(false);
        }

        private CardInspectPayload BuildShopInspectPayload(MinionCardDefinition card)
        {
            return new CardInspectPayload
            {
                hasCard = true,
                title = card.displayName,
                subtitle = $"Tier {card.tier}  •  3 gold",
                body = string.Empty,
                art = card.cardArt,
                isGolden = false,
                attack = card.attack,
                health = card.health,
                showStats = card.cardKind != CardKind.Spell
            };
        }

        private static CardInspectPayload BuildMinionInspectPayload(
            MinionCardDefinition card,
            MinionInstance minion,
            CardSlotDisplayMode mode)
        {
            var name = card != null ? card.displayName : minion.cardId;
            var prefix = minion.isGolden ? "★ Golden " : string.Empty;
            var action = card != null && card.cardKind == CardKind.Spell
                ? "Tap to cast this spell."
                : mode == CardSlotDisplayMode.Hand
                ? "Tap to play from hand."
                : mode == CardSlotDisplayMode.Board
                    ? "Drag to rearrange. Tap to sell for 1 gold."
                    : string.Empty;

            return new CardInspectPayload
            {
                hasCard = true,
                title = $"{prefix}{name}",
                subtitle = card != null ? $"Tier {card.tier}" : string.Empty,
                body = action,
                art = card?.cardArt,
                isGolden = minion.isGolden,
                attack = minion.attack,
                health = minion.health,
                showStats = card == null || card.cardKind != CardKind.Spell
            };
        }

    }
}