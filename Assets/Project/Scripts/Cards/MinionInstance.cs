using System;

namespace DreamGate.Battlegrounds.Cards
{
    [Serializable]
    public class MinionInstance
    {
        public string instanceId;
        public string cardId;
        public int attack;
        public int health;
        public int maxHealth;
        public bool isGolden;
        public bool isDead;
        public bool hasDivineShield;
        public bool divineShieldGranted;

        public MinionInstance Clone()
        {
            return new MinionInstance
            {
                instanceId = instanceId,
                cardId = cardId,
                attack = attack,
                health = health,
                maxHealth = maxHealth,
                isGolden = isGolden,
                isDead = isDead,
                hasDivineShield = hasDivineShield,
                divineShieldGranted = divineShieldGranted
            };
        }

        public static MinionInstance FromDefinition(MinionCardDefinition definition, bool golden = false)
        {
            return new MinionInstance
            {
                instanceId = Guid.NewGuid().ToString("N"),
                cardId = definition.cardId,
                attack = definition.attack,
                health = definition.health,
                maxHealth = definition.health,
                isGolden = golden,
                isDead = false
            };
        }
    }
}