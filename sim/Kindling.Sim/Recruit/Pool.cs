using System.Collections.Generic;
using Kindling.Sim.Catalog;
using Kindling.Sim.Model;
using Kindling.Sim.Rng;

namespace Kindling.Sim.Recruit
{
    public static class Pool
    {
        public static void Init(MatchState m, Catalog.Catalog cat)
        {
            m.Pool.Clear();
            foreach (UnitDef def in cat.ShopUnits())
            {
                m.Pool.Add(new PoolEntry
                {
                    Id = def.Id,
                    Remaining = def.CopyLimit
                });
            }
            m.SortPool();
        }

        public static void Return(MatchState m, UnitId id, int n = 1)
        {
            if (id.IsEmpty || n == 0) return;
            PoolEntry e = m.GetPool(id);
            if (e != null) e.Remaining += n;
        }

        public static bool TryConsume(MatchState m, UnitId id)
        {
            PoolEntry e = m.GetPool(id);
            if (e == null || e.Remaining <= 0) return false;
            e.Remaining--;
            return true;
        }

        public static UnitId DrawWeighted(MatchState m, Catalog.Catalog cat, PlayerState p, MatchRng rng)
        {
            int total = 0;
            for (int i = 0; i < m.Pool.Count; i++)
            {
                PoolEntry e = m.Pool[i];
                if (e.Remaining <= 0) continue;
                UnitDef def = cat.GetUnit(e.Id);
                if (def == null || def.Token || def.Disabled) continue;
                if (def.Depth > p.Depth) continue;
                total += e.Remaining;
            }
            if (total <= 0) return default;
            int r = rng.Range(MatchRng.Stream.Stall, 0, total);
            for (int i = 0; i < m.Pool.Count; i++)
            {
                PoolEntry e = m.Pool[i];
                if (e.Remaining <= 0) continue;
                UnitDef def = cat.GetUnit(e.Id);
                if (def == null || def.Token || def.Disabled) continue;
                if (def.Depth > p.Depth) continue;
                if (r < e.Remaining)
                {
                    e.Remaining--;
                    return e.Id;
                }
                r -= e.Remaining;
            }
            return default;
        }

        public static List<UnitId> EligibleIds(MatchState m, Catalog.Catalog cat, int depthExact, int depthMaxFill)
        {
            var ids = new List<UnitId>();
            for (int i = 0; i < m.Pool.Count; i++)
            {
                PoolEntry e = m.Pool[i];
                if (e.Remaining <= 0) continue;
                UnitDef def = cat.GetUnit(e.Id);
                if (def == null || def.Token || def.Disabled) continue;
                if (def.Depth == depthExact) ids.Add(e.Id);
            }
            if (ids.Count > 0) return ids;
            for (int i = 0; i < m.Pool.Count; i++)
            {
                PoolEntry e = m.Pool[i];
                if (e.Remaining <= 0) continue;
                UnitDef def = cat.GetUnit(e.Id);
                if (def == null || def.Token || def.Disabled) continue;
                if (def.Depth <= depthMaxFill) ids.Add(e.Id);
            }
            return ids;
        }

        public static List<PoolEntry> EligibleEntries(MatchState m, Catalog.Catalog cat, int depthMax)
        {
            var list = new List<PoolEntry>();
            for (int i = 0; i < m.Pool.Count; i++)
            {
                PoolEntry e = m.Pool[i];
                if (e.Remaining <= 0) continue;
                UnitDef def = cat.GetUnit(e.Id);
                if (def == null || def.Token || def.Disabled) continue;
                if (def.Depth <= depthMax) list.Add(e);
            }
            return list;
        }
    }
}
