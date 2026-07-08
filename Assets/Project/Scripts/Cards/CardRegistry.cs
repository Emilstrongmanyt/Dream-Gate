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
            // Tier 1 — budget ~3-5 stats; tokens and deathrattles sit slightly below curve.
            Register(CreateCard("snail", "Snail", 1, 1, 2, tripleRewardCardId: "mano", artFile: "SnailCard"));
            Register(CreateCard("blue_snail", "Blue Snail", 1, 1, 3, tripleRewardCardId: "mano", artFile: "BlueSnailCard"));
            Register(CreateCard("red_snail", "Red Snail", 1, 2, 2, tripleRewardCardId: "mano", artFile: "RedSnailCard"));
            Register(CreateCard("shroom", "Shroom", 1, 1, 2, artFile: "ShroomCard"));
            Register(CreateCard("green_mushroom", "Green Mushroom", 1, 2, 2, artFile: "GreenMushroomCard"));
            Register(CreateCard("mano", "Mano", 1, 8, 8, AbilityType.DeathrattleSummon,
                "Deathrattle: Summon a Red Snail.", summonCardId: "red_snail", canAppearInShop: false,
                artFile: "ManoCard"));
            Register(CreateSpell("onyx_apple_spell", "Onyx Apple", 3, SpellEffect.OnyxApple,
                "Double a friendly minion's Attack.", "OnyxAppleCard"));
            Register(CreateSpell("divine_shield_spell", "Divine Shield", 3, SpellEffect.DivineShield,
                "Give a friendly minion Divine Shield.", "DivineShieldCard"));
            Register(CreateSpell("rage_spell", "Rage", 1, SpellEffect.Rage,
                "Give all friendly minions +1 Attack.", "RageSpellCard"));
            Register(CreateSpell("doom_spell", "Doom", 1, SpellEffect.Doom,
                "Next combat, a random enemy minion becomes a Snail.", "DoomSpellCard"));
            Register(CreateCard("octopus", "Octopus", 1, 1, 4, AbilityType.Taunt, "Taunt.", artFile: "OctopusCard"));
            Register(CreateCard("stirge", "Stirge", 1, 1, 2, AbilityType.DeathrattleSummon,
                "Deathrattle: Summon a Jr. Stirge.", summonCardId: "jr_stirge", artFile: "StirgeCard"));
            Register(CreateCard("jr_stirge", "Jr. Stirge", 1, 1, 1, isToken: true, canAppearInShop: false,
                artFile: "JrStirgeCard"));

            // Tier 2 — budget ~5-7 stats; taunt trades attack for health.
            Register(CreateCard("scuba_pepe", "Scuba Pepe", 2, 2, 4, artFile: "ScubaPepeCard"));
            Register(CreateCard("horny_mushroom", "Horny Mushroom", 2, 1, 5, AbilityType.BattlecryDamageHero,
                "Deal 1 damage to your hero when played.", artFile: "HornyMushroomCard"));
            Register(CreateCard("axe_stump", "Axe Stump", 2, 3, 2, cardTribe: "stump", artFile: "AxeStumpCard"));
            Register(CreateCard("dark_stump", "Dark Stump", 2, 2, 4, AbilityType.BattlecryBuffOtherTribeHealth,
                "Battlecry: Give your other Stumps +2 HP.", cardTribe: "stump", abilityValue: 2,
                artFile: "DarkStumpCard"));
            Register(CreateCard("jr_wraith", "Jr. Wraith", 2, 3, 2, AbilityType.OnDamageDodge,
                "20% Chance to avoid damage when hit.", cardTribe: "wraith", abilityValue: 20,
                artFile: "Jr.WraithCard"));
            Register(CreateCard("brown_teddy", "Brown Teddy", 2, 2, 3, AbilityType.OnDamageSummonCopyChance,
                "20% Chance to summon a copy when hit.", abilityValue: 20, artFile: "BrownTeddyCard"));
            Register(CreateCard("baby_balrog", "Baby Balrog", 2, 3, 3, tripleRewardCardId: "baby_balrog_gang",
                artFile: "BabyBalrogCard"));
            Register(CreateCard("baby_balrog_gang", "Baby Balrog Gang", 2, 4, 5, canAppearInShop: false,
                artFile: "BabyBalrogGangCard"));

            // Tier 3 — budget ~7-10 stats; premium bodies pay for keywords.
            Register(CreateCard("stone_golem", "Stone Golem", 3, 2, 6, AbilityType.Taunt, "Taunt.",
                artFile: "StoneGolemCard"));
            Register(CreateCard("enraged_stone_golem", "Enraged Stone Golem", 3, 5, 5,
                canAppearInShop: false, artFile: "EnragedStoneGolemCard"));
            Register(CreateCard("king_slime", "King Slime", 3, 3, 5, AbilityType.DeathrattleBuffAllAttack,
                "Deathrattle: Give your minions +3 ATT for this combat.", abilityValue: 3,
                artFile: "KingSlimeCard"));
            Register(CreateCard("mushmom", "Mushmom", 3, 4, 5, AbilityType.DeathrattleSummon,
                "Deathrattle: Summon 2 Shrooms.", summonCardId: "shroom", abilityValue: 2,
                artFile: "MushmomCard"));
            Register(CreateCard("wraith", "Wraith", 3, 3, 3, AbilityType.BattlecryBuffTribeAttack,
                "Battlecry: Give your Wraith and Jr. Wraith +2 ATT this game.", cardTribe: "wraith",
                abilityValue: 2, artFile: "WraithCard"));
            Register(CreateCard("ghost_stumpy", "Ghost Stumpy", 3, 3, 2, AbilityType.BattlecryBuffTribe,
                "Battlecry: Your Stumps have +1 ATT, +1 HP this game.", cardTribe: "stump", abilityValue: 1,
                artFile: "GhostStumpyCard"));
            Register(CreateCard("stump_spirit", "Stump Spirit", 3, 2, 2, isToken: true, canAppearInShop: false,
                artFile: "AxeStumpCard"));
            Register(CreateCard("master_soul_teddy", "Master Soul Teddy", 3, 4, 4, AbilityType.Cleave,
                "Also damages minions adjacent to the target.", artFile: "MasterSoulTeddyCard"));

            // Tier 4 — capstone; mega-windfury trades burst for survivability under recoil.
            Register(CreateCard("jr_balrog", "Jr. Balrog", 4, 5, 12, AbilityType.MegaWindfury, "Mega-Windfury.",
                artFile: "Jr.BalrogCard"));
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
            string tripleRewardCardId = "",
            int abilityValue = 1,
            bool isToken = false,
            bool canAppearInShop = true,
            string artFile = "",
            string cardTribe = "")
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
            card.cardTribe = cardTribe;
            card.summonCardId = summonCardId;
            card.tripleRewardCardId = tripleRewardCardId;
            card.isToken = isToken;
            card.canAppearInShop = canAppearInShop;
            card.cardArt = LoadArt(tier, artFile);
            card.name = displayName;
            return card;
        }

        private static MinionCardDefinition CreateSpell(
            string id,
            string displayName,
            int tier,
            SpellEffect effect,
            string abilityText,
            string artFile)
        {
            var card = CreateCard(id, displayName, tier, 0, 0, abilityText: abilityText, artFile: artFile);
            card.cardKind = CardKind.Spell;
            card.spellEffect = effect;
            return card;
        }

        private static Sprite LoadArt(int tier, string fileName)
        {
            return CardArtLoader.Load(tier, fileName);
        }
    }
}