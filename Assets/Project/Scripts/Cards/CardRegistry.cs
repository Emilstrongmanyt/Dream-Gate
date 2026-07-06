using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DreamGate.Battlegrounds.Cards
{
    public static class CardRegistry
    {
        private static readonly Dictionary<string, MinionCardDefinition> CardsById = new();
        private static bool initialized;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            CardsById.Clear();
            RegisterBuiltInCards();

            var database = Resources.Load<CardDatabase>("CardDatabase");
            if (database != null)
            {
                foreach (var card in database.allCards)
                {
                    if (card != null && !string.IsNullOrEmpty(card.cardId))
                    {
                        CardsById[card.cardId] = card;
                    }
                }
            }

            initialized = true;
        }

        public static MinionCardDefinition Get(string cardId)
        {
            Initialize();
            return CardsById.TryGetValue(cardId, out var card) ? card : null;
        }

        public static IReadOnlyList<MinionCardDefinition> GetAllShopCards()
        {
            Initialize();
            return CardsById.Values.Where(c => c.canAppearInShop).ToList();
        }

        public static IReadOnlyList<MinionCardDefinition> GetPoolForTier(int tavernTier)
        {
            Initialize();
            return CardsById.Values
                .Where(c => c.canAppearInShop && c.tier <= tavernTier)
                .ToList();
        }

        public static IReadOnlyList<MinionCardDefinition> GetCardsAtTier(int minionTier)
        {
            Initialize();
            return CardsById.Values
                .Where(c => c.canAppearInShop && c.tier == minionTier)
                .ToList();
        }

        private static void RegisterBuiltInCards()
        {
            Register(CreateCard("blue_snail", "Blue Snail", 1, 1, 2, artFile: "BlueSnailCardT1"));
            Register(CreateCard("stirge", "Stirge", 1, 1, 1, AbilityType.DeathrattleSummon,
                "Final Stand: Summon a Jr. Stirge", summonCardId: "jr_stirge", artFile: "StirgeCardT1"));
            Register(CreateCard("jr_stirge", "Jr. Stirge", 1, 1, 1, isToken: true, canAppearInShop: false, artFile: "JrStirgeCardT1"));
            Register(CreateCard("mushmom", "Mushmom", 1, 1, 3, artFile: "MushmomCardT1"));
            Register(CreateCard("teddy", "Teddy", 1, 2, 1, artFile: "TeddyCardT1"));

            Register(CreateCard("evil_eye", "Evil Eye", 2, 4, 2, artFile: "EvilEyeCardT2"));
            Register(CreateCard("blue_mushroom", "Blue Mushroom", 2, 2, 2, AbilityType.OnDamageSummonCopy,
                "Summons an exact copy when hit.", artFile: "BlueMushroomCardT2"));
            Register(CreateCard("scuba_pepe", "Scuba Pepe", 2, 2, 3, artFile: "ScubaPepeCardT2"));

            Register(CreateCard("stone_golem", "Stone Golem", 3, 3, 3, AbilityType.OnDamageTransform,
                "Becomes Enraged when hit.", summonCardId: "enraged_stone_golem", artFile: "StoneGolemCardT3"));
            Register(CreateCard("enraged_stone_golem", "Enraged Stone Golem", 3, 6, 6,
                canAppearInShop: false, artFile: "EnragedStoneGolemCardT3"));
            Register(CreateCard("iron_hog", "Iron Hog", 3, 2, 7, AbilityType.Taunt, "'Taunt'", artFile: "IronHogCardT3"));
            Register(CreateCard("master_death_teddy", "Master Death Teddy", 3, 3, 3, AbilityType.Cleave,
                "This monster also damages enemies next to its target.", artFile: "MasterDeathTeddyCardT3"));
        }

        private static void Register(MinionCardDefinition card)
        {
            CardsById[card.cardId] = card;
        }

        private static MinionCardDefinition CreateCard(
            string id,
            string displayName,
            int tier,
            int attack,
            int health,
            AbilityType ability = AbilityType.None,
            string abilityText = "",
            string summonCardId = "",
            int abilityValue = 1,
            bool isToken = false,
            bool canAppearInShop = true,
            string artFile = "")
        {
            var card = ScriptableObject.CreateInstance<MinionCardDefinition>();
            card.cardId = id;
            card.displayName = displayName;
            card.tier = tier;
            card.attack = attack;
            card.health = health;
            card.abilityType = ability;
            card.abilityValue = abilityValue;
            card.abilityText = abilityText;
            card.summonCardId = summonCardId;
            card.isToken = isToken;
            card.canAppearInShop = canAppearInShop;
            card.cardArt = LoadArt(tier, artFile);
            card.name = displayName;
            return card;
        }

        private static Sprite LoadArt(int tier, string fileName)
        {
            return CardArtLoader.Load(tier, fileName);
        }
    }
}