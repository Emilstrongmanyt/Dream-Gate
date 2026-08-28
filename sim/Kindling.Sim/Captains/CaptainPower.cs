using Kindling.Sim.Catalog;
using Kindling.Sim.Model;

namespace Kindling.Sim.Captains
{
    public static class CaptainPower
    {
        public static string Line(CaptainDef def)
        {
            if (def == null) return "";
            if (!string.IsNullOrEmpty(def.Text)) return def.Text;
            if (def.HasEdict)
                return "Edict " + def.EdictCost + (def.EdictNeedsTarget ? " (target a Kindled)." : ".");
            if (def.Passives != null && def.Passives.Count > 0)
                return PassiveLine(def.Passives[0]);
            return "No power listed.";
        }

        public static string PassiveLine(CaptainPassive p)
        {
            switch (p)
            {
                case CaptainPassive.VesperFirstRerollFree:
                    return "The first Reroll each recruit is free.";
                case CaptainPassive.DebtGrantPlus1:
                    return "25 Wick. You gain 1 extra Ember each recruit.";
                case CaptainPassive.DredgerNextGrantPlus2:
                    return "After you Upgrade, the next Ember grant is +2.";
                case CaptainPassive.KettleStallPlus1:
                    return "Your stall has +1 slot.";
                case CaptainPassive.CandleAwakenPlus2:
                    return "When a Kindled Awakens, it gains +2/+2.";
                case CaptainPassive.GlassKindleLeftAegis:
                    return "Kindle: your leftmost Kindled gains Aegis.";
                case CaptainPassive.SkivBeastOnBuyPlus1Atk:
                    return "Beasts you buy gain +1 Attack.";
                case CaptainPassive.FlintKindleRightPlus1Atk:
                    return "Kindle: your rightmost Kindled gains +1 Attack this combat.";
                case CaptainPassive.NollOnBuyPendingEmber:
                    return "Each buy banks 1 Ember for next recruit.";
                default:
                    return "Passive.";
            }
        }
    }
}
