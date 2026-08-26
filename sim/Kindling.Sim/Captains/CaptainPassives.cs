using Kindling.Sim.Catalog;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;

namespace Kindling.Sim.Captains
{
    public static class CaptainPassives
    {
        public static bool Has(PlayerState p, Catalog.Catalog cat, CaptainPassive passive)
        {
            if (p == null || cat == null) return false;
            CaptainDef def = cat.GetCaptain(p.Captain);
            if (def == null) return false;
            for (int i = 0; i < def.Passives.Count; i++)
            {
                if (def.Passives[i] == passive) return true;
            }
            return false;
        }

        public static void OnCaptainPicked(PlayerState p, Catalog.Catalog cat)
        {
            CaptainDef def = cat.GetCaptain(p.Captain);
            if (def == null) return;
            p.Wick = def.Wick;
            p.Edict = new EdictState { Repeatable = false, UsedThisRecruit = false };
            for (int i = 0; i < def.Passives.Count; i++)
                ApplyNamed(def.Passives[i], p, cat, "pick", null);
        }

        public static void OnRecruitStart(PlayerState p, Catalog.Catalog cat)
        {
            if (Has(p, cat, CaptainPassive.VesperFirstRerollFree))
                p.SetFlag(PlayerFlags.VesperFreeReroll);
        }

        public static void OnBuy(PlayerState p, Catalog.Catalog cat, UnitInstance bought)
        {
            if (bought == null) return;
            if (!Has(p, cat, CaptainPassive.SkivBeastOnBuyPlus1Atk)) return;
            UnitDef def = cat.GetUnit(bought.CatalogId);
            if (def != null && def.Chorus == Chorus.Beast)
                Units.BuffPermanent(bought, 1, 0);
        }

        public static void OnUpgrade(PlayerState p, Catalog.Catalog cat)
        {
            if (Has(p, cat, CaptainPassive.DredgerNextGrantPlus2))
                p.DredgerBonus = 2;
        }

        public static void OnAwaken(PlayerState p, Catalog.Catalog cat, UnitInstance awakened)
        {
            if (awakened == null) return;
            if (!Has(p, cat, CaptainPassive.CandleAwakenPlus2)) return;
            Units.BuffPermanent(awakened, 2, 2);
        }

        public static void OnKindle(PlayerState p, Catalog.Catalog cat, Combat.CombatRuntime rt)
        {
            if (!Has(p, cat, CaptainPassive.GlassKindleLeftAegis)) return;
            if (p.Board.Count == 0) return;
            UnitInstance u = p.Board[0];
            if (u == null) return;
            if (!u.Has(Keyword.Aegis))
            {
                u.AddKeyword(Keyword.Aegis);
                u.Mods.Add(new Modifier
                {
                    Tag = ModTag.ThisCombat,
                    Keywords = Keyword.Aegis
                });
            }
            rt?.Log(CombatOp.Kindle, p.Seat, p.Seat, u.InstanceId, 0, 0, 0, 0, u.Atk, u.Hp, u.CatalogId.Value, "GlassAegis");
        }

        static void ApplyNamed(CaptainPassive passive, PlayerState p, Catalog.Catalog cat, string hook, UnitInstance unit)
        {
            switch (passive)
            {
                case CaptainPassive.KettleStallPlus1:
                    p.StallSizeDelta += 1;
                    break;
                case CaptainPassive.VesperFirstRerollFree:
                case CaptainPassive.DebtGrantPlus1:
                case CaptainPassive.DredgerNextGrantPlus2:
                case CaptainPassive.CandleAwakenPlus2:
                case CaptainPassive.GlassKindleLeftAegis:
                case CaptainPassive.SkivBeastOnBuyPlus1Atk:
                    break;
            }
        }
    }
}
