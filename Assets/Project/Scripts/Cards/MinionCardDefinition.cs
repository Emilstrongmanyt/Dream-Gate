using UnityEngine;

namespace DreamGate.Battlegrounds.Cards
{
    [CreateAssetMenu(fileName = "MinionCard", menuName = "Dream Gate/Minion Card")]
    public class MinionCardDefinition : ScriptableObject
    {
        public string cardId;
        public string displayName;
        [Range(1, 4)] public int tier = 1;
        public int attack = 1;
        public int health = 1;
        public bool isToken;
        public bool canAppearInShop = true;
        public CardKind cardKind = CardKind.Minion;
        public SpellEffect spellEffect = SpellEffect.None;
        public AbilityType abilityType = AbilityType.None;
        public int abilityValue = 1;
        public string abilityText;
        [Tooltip("Tribe tag for battlecry buffs (e.g. stump, wraith).")]
        public string cardTribe;
        [Tooltip("Used by DeathrattleSummon abilities.")]
        public string summonCardId;
        [Tooltip("When tripled, becomes this card instead of a golden copy.")]
        public string tripleRewardCardId;
        public Sprite cardArt;
    }
}