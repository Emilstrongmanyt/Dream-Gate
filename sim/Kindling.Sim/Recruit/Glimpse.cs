using System.Collections.Generic;
using Kindling.Sim.Catalog;
using Kindling.Sim.Model;
using Kindling.Sim.Rng;

namespace Kindling.Sim.Recruit
{
    public static class Glimpse
    {
        public static void Enqueue(MatchState m, PlayerState p, Catalog.Catalog cat, UnitInstance source, DepthMode mode, int sourceDepth)
        {
            int depth = ResolveDepth(p, mode, sourceDepth);
            GlimpseOffer offer = BuildOffer(m, p, cat, depth);
            if (offer.Choices == null || offer.Choices.Length == 0)
            {
                m.AddLog("GlimpseEmpty");
                p.AddLog("GlimpseEmpty");
                return;
            }
            if (p.HasFlag(PlayerFlags.GlimpseOpen))
            {
                p.GlimpseQueue.Enqueue(offer);
            }
            else
            {
                p.GlimpseQueue.Enqueue(offer);
                p.SetFlag(PlayerFlags.GlimpseOpen);
            }
        }

        public static GlimpseOffer BuildOffer(MatchState m, PlayerState p, Catalog.Catalog cat, int depth)
        {
            if (depth < 1) depth = 1;
            if (depth > 6) depth = 6;
            var candidates = new List<UnitId>();
            for (int i = 0; i < m.Pool.Count; i++)
            {
                PoolEntry e = m.Pool[i];
                if (e.Remaining <= 0) continue;
                UnitDef def = cat.GetUnit(e.Id);
                if (def == null || def.Token || def.Disabled) continue;
                if (def.Depth == depth) candidates.Add(e.Id);
            }
            if (candidates.Count < Rules.GlimpseOfferCount)
            {
                for (int i = 0; i < m.Pool.Count; i++)
                {
                    PoolEntry e = m.Pool[i];
                    if (e.Remaining <= 0) continue;
                    UnitDef def = cat.GetUnit(e.Id);
                    if (def == null || def.Token || def.Disabled) continue;
                    if (def.Depth <= depth && !ContainsId(candidates, e.Id))
                        candidates.Add(e.Id);
                }
            }
            if (candidates.Count == 0)
                return new GlimpseOffer { Depth = depth, Choices = System.Array.Empty<UnitId>() };

            MatchRng.Stream stream = MatchRng.Stream.Glimpse;
            m.Rng.Shuffle(stream, candidates);
            int take = candidates.Count < Rules.GlimpseOfferCount ? candidates.Count : Rules.GlimpseOfferCount;
            var choices = new UnitId[take];
            for (int i = 0; i < take; i++)
                choices[i] = candidates[i];
            SortIds(choices);
            return new GlimpseOffer { Depth = depth, Choices = choices };
        }

        public static SimResult Pick(MatchState m, PlayerState p, Catalog.Catalog cat, int offerIndex)
        {
            if (!p.HasFlag(PlayerFlags.GlimpseOpen) || p.GlimpseQueue.Count == 0)
                return SimResult.Fail("NO_GLIMPSE");
            GlimpseOffer offer = p.GlimpseQueue.Peek();
            if (offer.Choices == null || offer.Choices.Length == 0)
            {
                m.AddLog("GlimpseEmpty");
                p.AddLog("GlimpseEmpty");
                p.GlimpseQueue.Dequeue();
                OpenNext(p);
                return SimResult.Success();
            }
            if (offerIndex < 0 || offerIndex >= offer.Choices.Length)
                offerIndex = 0;
            UnitId id = offer.Choices[offerIndex];
            GrantChoice(m, p, cat, id);
            p.GlimpseQueue.Dequeue();
            OpenNext(p);
            return SimResult.Success();
        }

        public static void DrainQueue(MatchState m, PlayerState p, Catalog.Catalog cat, bool autoPick)
        {
            while (p.GlimpseQueue.Count > 0)
            {
                GlimpseOffer offer = p.GlimpseQueue.Dequeue();
                if (offer.Choices == null || offer.Choices.Length == 0)
                {
                    m.AddLog("GlimpseEmpty");
                    p.AddLog("GlimpseEmpty");
                    continue;
                }
                if (autoPick)
                    GrantChoice(m, p, cat, offer.Choices[0]);
            }
            p.ClearFlag(PlayerFlags.GlimpseOpen);
        }

        static void OpenNext(PlayerState p)
        {
            if (p.GlimpseQueue.Count == 0)
                p.ClearFlag(PlayerFlags.GlimpseOpen);
            else
                p.SetFlag(PlayerFlags.GlimpseOpen);
        }

        static void GrantChoice(MatchState m, PlayerState p, Catalog.Catalog cat, UnitId id)
        {
            PoolEntry e = m.GetPool(id);
            if (e != null && e.Remaining > 0) e.Remaining--;
            else m.GlimpseOverflowGrants++;
            if (p.Hand.Count >= Rules.HandMax)
            {
                m.AddLog("HandFull");
                p.AddLog("HandFull");
                return;
            }
            p.Hand.Add(Units.Create(cat, m.Rng, id));
            Awaken.TryAwaken(m, p, cat);
        }

        public static int ResolveDepth(PlayerState p, DepthMode mode, int sourceDepth)
        {
            switch (mode)
            {
                case DepthMode.Current:
                    return p.Depth;
                case DepthMode.Fixed:
                    return sourceDepth;
                default:
                {
                    int d = sourceDepth + 1;
                    if (d > 6) d = 6;
                    return d;
                }
            }
        }

        static bool ContainsId(List<UnitId> list, UnitId id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].Equals(id)) return true;
            return false;
        }

        static void SortIds(UnitId[] arr)
        {
            for (int i = 1; i < arr.Length; i++)
            {
                UnitId v = arr[i];
                int j = i - 1;
                while (j >= 0 && arr[j].CompareTo(v) > 0)
                {
                    arr[j + 1] = arr[j];
                    j--;
                }
                arr[j + 1] = v;
            }
        }
    }
}
