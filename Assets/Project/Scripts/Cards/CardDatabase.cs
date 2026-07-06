using System.Collections.Generic;
using UnityEngine;

namespace DreamGate.Battlegrounds.Cards
{
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "Dream Gate/Card Database")]
    public class CardDatabase : ScriptableObject
    {
        public List<MinionCardDefinition> allCards = new();
    }
}