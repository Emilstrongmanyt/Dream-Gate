using UnityEngine;

namespace DreamGate.Battlegrounds.Heroes
{
    [CreateAssetMenu(fileName = "Hero", menuName = "Dream Gate/Hero")]
    public class HeroDefinition : ScriptableObject
    {
        public string heroId;
        public string displayName;
        public Sprite portrait;
    }
}