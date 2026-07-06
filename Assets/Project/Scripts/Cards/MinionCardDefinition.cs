using UnityEngine;

namespace DreamGate.Battlegrounds.Cards
{
    [CreateAssetMenu(fileName = "MinionCard", menuName = "Dream Gate/Minion Card")]
    public class MinionCardDefinition : ScriptableObject
    {
        public string cardId;
        public string displayName;
        [Range(1, 6)] public int tier = 1;
        public int attack = 1;
        public int health = 1;
        public bool isToken;
        public bool canAppearInShop = true;
        public AbilityType abilityType = AbilityType.None;
        public int abilityValue = 1;
        public string abilityText;
        [Tooltip("Used by DeathrattleSummon abilities.")]
        public string summonCardId;
        public Sprite cardArt;
    }
}