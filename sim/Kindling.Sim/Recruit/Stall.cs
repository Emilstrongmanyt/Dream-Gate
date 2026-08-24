using Kindling.Sim.Catalog;
using Kindling.Sim.Model;
using Kindling.Sim.Rng;

namespace Kindling.Sim.Recruit
{
    public static class Stall
    {
        public static int Size(PlayerState p)
        {
            return Rules.StallSize(p.Depth, p.StallSizeDelta);
        }

        public static void ReturnAll(MatchState m, PlayerState p)
        {
            for (int i = 0; i < p.Stall.Count; i++)
            {
                UnitInstance u = p.Stall[i];
                if (u != null)
                    Pool.Return(m, BaseId(u));
            }
            p.Stall.Clear();
        }

        public static void Fill(MatchState m, PlayerState p, Catalog.Catalog cat, MatchRng rng, bool respectHold)
        {
            int size = Size(p);
            if (!respectHold || !p.Hold)
            {
                ReturnAll(m, p);
            }
            else
            {
                while (p.Stall.Count > size)
                {
                    int last = p.Stall.Count - 1;
                    UnitInstance u = p.Stall[last];
                    if (u != null) Pool.Return(m, BaseId(u));
                    p.Stall.RemoveAt(last);
                }
            }
            while (p.Stall.Count < size)
            {
                UnitId id = Pool.DrawWeighted(m, cat, p, rng);
                if (id.IsEmpty) break;
                p.Stall.Add(Units.Create(cat, rng, id));
            }
        }

        public static void Reroll(MatchState m, PlayerState p, Catalog.Catalog cat, MatchRng rng)
        {
            p.Hold = false;
            ReturnAll(m, p);
            Fill(m, p, cat, rng, respectHold: false);
        }

        public static UnitId BaseId(UnitInstance u)
        {
            return u.CatalogId;
        }
    }
}
