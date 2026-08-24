using Kindling.Sim.Captains;
using Kindling.Sim.Catalog;
using Kindling.Sim.Effects;
using Kindling.Sim.Model;
using Kindling.Sim.Rng;

namespace Kindling.Sim.Recruit
{
    public static class Grant
    {
        public static void GrantEmbers(PlayerState p, int roundIndex, Catalog.Catalog cat)
        {
            int baseGrant = 2 + roundIndex;
            if (baseGrant > 10) baseGrant = 10;
            if (CaptainPassives.Has(p, cat, CaptainPassive.DebtGrantPlus1))
            {
                baseGrant += 1;
                if (baseGrant > 10) baseGrant = 10;
            }
            int dredger = p.DredgerBonus;
            p.DredgerBonus = 0;
            int hardCap = dredger > 0 ? 13 : 10;
            int income = baseGrant + dredger;
            if (income > hardCap) income = hardCap;
            int pending = p.PendingEmbers;
            p.PendingEmbers = 0;
            p.Embers = Rules.ClampEmbers(income + pending);
        }

        public static void RecruitStart(MatchState match, PlayerState p, Catalog.Catalog cat)
        {
            if (match.Round > 1)
            {
                if (p.HasFlag(PlayerFlags.UpgradedThisRecruit))
                {
                }
                else
                {
                    int c = p.UpgradeCost - 1;
                    if (c < 0) c = 0;
                    p.UpgradeCost = c;
                }
            }
            p.ClearFlag(PlayerFlags.UpgradedThisRecruit);
            GrantEmbers(p, match.Round, cat);
            Stall.Fill(match, p, cat, match.Rng, respectHold: true);
            CaptainPassives.OnRecruitStart(p, cat);
            match.Season?.OnRecruitStart(p);
            EffectHooks.Fire(match, cat, Trigger.StartOfRecruit, null, p, null);
            Awaken.TryAwaken(match, p, cat);
            p.Ready = false;
        }

        public static void RecruitEnd(MatchState match, PlayerState p, Catalog.Catalog cat)
        {
            EffectHooks.Fire(match, cat, Trigger.EndOfRecruit, null, p, null);
            Glimpse.DrainQueue(match, p, cat, autoPick: true);
            p.Embers = 0;
            p.ClearFlag(PlayerFlags.NextRerollFree);
            p.ClearFlag(PlayerFlags.TycoonFreeReroll);
            p.ClearFlag(PlayerFlags.VesperFreeReroll);
            p.ClearFlag(PlayerFlags.GlimpseOpen);
            p.RerollsThisRecruit = 0;
            p.BoughtThisRecruit = 0;
            p.Edict.UsedThisRecruit = false;
            p.Ready = false;
            for (int i = 0; i < p.Board.Count; i++)
                p.Board[i].ClearOnceThisRecruit();
            for (int i = 0; i < p.Hand.Count; i++)
                p.Hand[i].ClearOnceThisRecruit();
            p.SnapshotLock();
        }

        public static int RerollCostNow(PlayerState p)
        {
            if (p.HasFlag(PlayerFlags.NextRerollFree) ||
                p.HasFlag(PlayerFlags.TycoonFreeReroll) ||
                p.HasFlag(PlayerFlags.VesperFreeReroll))
                return 0;
            return Rules.RerollCost;
        }

        public static void ConsumeFreeRerollFlags(PlayerState p)
        {
            if (p.HasFlag(PlayerFlags.NextRerollFree))
            {
                p.ClearFlag(PlayerFlags.NextRerollFree);
                return;
            }
            if (p.HasFlag(PlayerFlags.TycoonFreeReroll))
            {
                p.ClearFlag(PlayerFlags.TycoonFreeReroll);
                return;
            }
            if (p.HasFlag(PlayerFlags.VesperFreeReroll))
                p.ClearFlag(PlayerFlags.VesperFreeReroll);
        }
    }
}
