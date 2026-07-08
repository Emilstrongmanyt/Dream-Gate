#if !SERVER_BUILD
using UnityEngine;
#endif

namespace DreamGate.Battlegrounds.Cards
{
#if !SERVER_BUILD
    [CreateAssetMenu(fileName = "MinionCard", menuName = "Dream Gate/Minion Card")]
#endif
    public class MinionCardDefinition
#if !SERVER_BUILD
        : ScriptableObject
#endif
    {
        public string cardId;
        public string displayName;
#if !SERVER_BUILD
        [Range(1, 4)]
#endif
        public int tier = 1;
        public int attack = 1;
        public int health = 1;
        public bool isToken;
        public bool canAppearInShop = true;
        public CardKind cardKind = CardKind.Minion;
        public SpellEffect spellEffect = SpellEffect.None;
        public AbilityType abilityType = AbilityType.None;
        public int abilityValue = 1;
        public string abilityText;
        public string cardTribe;
        public string summonCardId;
        public string tripleRewardCardId;
#if !SERVER_BUILD
        public Sprite cardArt;
#endif
    }
}