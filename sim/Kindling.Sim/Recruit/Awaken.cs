using System.Collections.Generic;
using Kindling.Sim.Captains;
using Kindling.Sim.Catalog;
using Kindling.Sim.Effects;
using Kindling.Sim.Model;

namespace Kindling.Sim.Recruit
{
    public static class Awaken
    {
        public static bool TryAwaken(MatchState m, PlayerState p, Catalog.Catalog cat)
        {
            bool any = false;
            while (TryAwakenOnce(m, p, cat))
                any = true;
            return any;
        }

        public static bool TryAwakenOnce(MatchState m, PlayerState p, Catalog.Catalog cat)
        {
            var counts = new List<CountRow>();
            Tally(p.Board, counts, cat);
            Tally(p.Hand, counts, cat);
            CountRow best = default;
            bool found = false;
            for (int i = 0; i < counts.Count; i++)
            {
                if (counts[i].Count >= 3)
                {
                    if (!found || counts[i].Id.CompareTo(best.Id) < 0)
                    {
                        best = counts[i];
                        found = true;
                    }
                }
            }
            if (!found) return false;

            var taken = new List<UnitInstance>(3);
            TakeLeftmost(p.Board, best.Id, taken, 3);
            if (taken.Count < 3)
                TakeLeftmost(p.Hand, best.Id, taken, 3);
            if (taken.Count < 3) return false;

            for (int i = 0; i < taken.Count; i++)
                RemoveOwned(p, taken[i]);

            UnitDef def = cat.GetUnit(best.Id);
            int extraAtk = 0, extraHp = 0, cinders = 0;
            Keyword kw = Keyword.None;
            var extraFx = new List<EffectDef>();
            for (int i = 0; i < taken.Count; i++)
            {
                extraAtk += taken[i].ExtraAtk;
                extraHp += taken[i].ExtraHp;
                cinders += taken[i].Cinders;
                kw |= taken[i].Keywords;
                for (int e = 0; e < taken[i].ExtraEffects.Count; e++)
                    extraFx.Add(taken[i].ExtraEffects[e]);
            }
            int atk = def.Atk * 2 + extraAtk;
            int hp = def.Hp * 2 + extraHp;
            var neu = new UnitInstance
            {
                InstanceId = m.Rng.NextId(),
                CatalogId = best.Id,
                Atk = atk,
                Hp = hp,
                MaxHp = hp,
                ExtraAtk = extraAtk,
                ExtraHp = extraHp,
                Cinders = cinders,
                Keywords = kw,
                Awakened = true,
                AttackCharges = 1,
                ExtraEffects = extraFx
            };
            CaptainPassives.OnAwaken(p, cat, neu);

            bool placed = false;
            if (p.Board.Count < Rules.BoardMax)
            {
                p.Board.Add(neu);
                placed = true;
            }
            else if (p.Hand.Count < Rules.HandMax)
            {
                p.Hand.Add(neu);
                placed = true;
            }
            else
            {
                p.SetFlag(PlayerFlags.AwakenPending);
                p.Hand.Add(taken[0]);
                p.Board.Insert(0, taken[1]);
                if (p.Board.Count <= Rules.BoardMax)
                    p.Board.Add(taken[2]);
                else
                    p.Hand.Add(taken[2]);
                return false;
            }

            if (placed)
            {
                m.AwakenEvents++;
                p.ClearFlag(PlayerFlags.AwakenPending);
                EffectHooks.Fire(m, cat, Trigger.OnAwaken, neu, p, null);
                Glimpse.Enqueue(m, p, cat, neu, DepthMode.TriplePlusOne, def.Depth);
            }
            return placed;
        }

        struct CountRow
        {
            public UnitId Id;
            public int Count;
        }

        static void Tally(List<UnitInstance> list, List<CountRow> counts, Catalog.Catalog cat)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                UnitDef def = cat != null ? cat.GetUnit(list[i].CatalogId) : null;
                if (def != null && (def.Spell || def.Token)) continue;
                UnitId id = list[i].CatalogId;
                bool hit = false;
                for (int c = 0; c < counts.Count; c++)
                {
                    if (counts[c].Id.Equals(id))
                    {
                        CountRow row = counts[c];
                        row.Count++;
                        counts[c] = row;
                        hit = true;
                        break;
                    }
                }
                if (!hit) counts.Add(new CountRow { Id = id, Count = 1 });
            }
        }

        static void TakeLeftmost(List<UnitInstance> list, UnitId id, List<UnitInstance> taken, int need)
        {
            for (int i = 0; i < list.Count && taken.Count < need; i++)
            {
                if (list[i].CatalogId.Equals(id))
                    taken.Add(list[i]);
            }
        }

        static void RemoveOwned(PlayerState p, UnitInstance u)
        {
            for (int i = 0; i < p.Board.Count; i++)
            {
                if (ReferenceEquals(p.Board[i], u) || p.Board[i].InstanceId == u.InstanceId)
                {
                    p.Board.RemoveAt(i);
                    return;
                }
            }
            for (int i = 0; i < p.Hand.Count; i++)
            {
                if (ReferenceEquals(p.Hand[i], u) || p.Hand[i].InstanceId == u.InstanceId)
                {
                    p.Hand.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
