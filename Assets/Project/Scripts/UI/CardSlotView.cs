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
            CardIdleMotion idleMotion,
            CombatMinionMotion combatMotion,
            CardInspectHandler inspectHandler,
            RectTransform rootRect)
        {
            Button = button;
            FrameImage = frameImage;
            ArtImage = artImage;
            StatsText = statsText;
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
            StatsText.text = label;
            ClearArt();
            SetGoldenFrame(false);
            FrameImage.color = new Color(0.12f, 0.16f, 0.26f, 0.75f);
            IdleMotion?.SetActiveMotion(false);
            Button.interactable = interactable;
        }

        public void SetShopCard(MinionCardDefinition card, bool interactable)
        {
            if (card == null)
            {
                SetEmpty("Empty", false);
                return;
            }

            var cardKey = $"shop:{card.cardId}";
            ApplyArt(card, cardKey);
            StatsText.text = $"T{card.tier} {card.displayName}\n{card.attack}/{card.health}\n3g";
            SetGoldenFrame(false);
            IdleMotion?.SetActiveMotion(false);
            Button.interactable = interactable;
            inspectPayload = BuildShopInspectPayload(card);
            InspectHandler?.SetInspectable(true);
        }

        public void SetMinionCard(MinionCardDefinition card, MinionInstance minion, CardSlotDisplayMode mode, bool interactable)
        {
            if (minion == null)
            {
                SetEmpty(mode == CardSlotDisplayMode.Hand ? "Hand" : "Slot", false);
                return;
            }

            var cardKey = $"{mode}:{minion.instanceId}:{minion.cardId}:{minion.attack}:{minion.health}:{minion.isGolden}";
            var name = card != null ? card.displayName : minion.cardId;
            var prefix = minion.isGolden ? "★ " : string.Empty;
            var action = mode == CardSlotDisplayMode.Board ? "Sell 1g" : "Play";

            ApplyArt(card, cardKey);
            StatsText.text = $"{prefix}{name}\n{minion.attack}/{minion.health}\n{action}";
            SetGoldenFrame(minion.isGolden);

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
            var name = card != null ? card.displayName : minion.cardId;
            var prefix = minion.isGolden ? "★ " : string.Empty;

            ApplyArt(card, cardKey);
            StatsText.text = $"{prefix}{name}\n{minion.attack}/{minion.health}";
            SetGoldenFrame(minion.isGolden);
            FrameImage.color = hasArt
                ? new Color(0.08f, 0.1f, 0.16f, 0.82f)
                : new Color(0.12f, 0.16f, 0.26f, 0.9f);
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
            FrameImage.color = new Color(0.08f, 0.08f, 0.1f, 0.35f);
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

        private void SetGoldenFrame(bool isGolden)
        {
            if (isGolden)
            {
                FrameImage.color = new Color(0.45f, 0.35f, 0.08f, 0.85f);
                return;
            }

            FrameImage.color = hasArt
                ? new Color(0.08f, 0.1f, 0.16f, 0.55f)
                : new Color(0.12f, 0.16f, 0.26f, 0.75f);
        }

        private CardInspectPayload BuildShopInspectPayload(MinionCardDefinition card)
        {
            return new CardInspectPayload
            {
                hasCard = true,
                title = card.displayName,
                subtitle = $"Tier {card.tier}  •  {card.attack}/{card.health}  •  3 gold",
                body = BuildAbilityBody(card),
                art = card.cardArt,
                isGolden = false
            };
        }

        private static CardInspectPayload BuildMinionInspectPayload(
            MinionCardDefinition card,
            MinionInstance minion,
            CardSlotDisplayMode mode)
        {
            var name = card != null ? card.displayName : minion.cardId;
            var prefix = minion.isGolden ? "★ Golden " : string.Empty;
            var action = mode == CardSlotDisplayMode.Hand
                ? "Tap to play from hand."
                : mode == CardSlotDisplayMode.Board
                    ? "Drag to rearrange. Tap to sell for 1 gold."
                    : string.Empty;

            var body = BuildAbilityBody(card);
            if (!string.IsNullOrEmpty(action))
            {
                body = string.IsNullOrEmpty(body) ? action : $"{body}\n\n{action}";
            }

            return new CardInspectPayload
            {
                hasCard = true,
                title = $"{prefix}{name}",
                subtitle = $"{minion.attack}/{minion.health}" + (card != null ? $"  •  Tier {card.tier}" : string.Empty),
                body = body,
                art = card?.cardArt,
                isGolden = minion.isGolden
            };
        }

        private static string BuildAbilityBody(MinionCardDefinition card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(card.abilityText))
            {
                return card.abilityText.Trim();
            }

            return card.abilityType switch
            {
                AbilityType.Taunt => "Taunt",
                AbilityType.Cleave => "Cleave",
                AbilityType.DeathrattleSummon => "Deathrattle: Summon",
                AbilityType.StartOfCombatBuffSelf => "Start of Combat: Buff self",
                AbilityType.OnDamageSummonCopy => "After damage: Summon a copy",
                AbilityType.OnDamageTransform => "After damage: Transform",
                _ => string.Empty
            };
        }
    }
}