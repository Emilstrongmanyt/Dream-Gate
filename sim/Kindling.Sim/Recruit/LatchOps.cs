using Kindling.Sim.Catalog;
using Kindling.Sim.Effects;
using Kindling.Sim.Model;

namespace Kindling.Sim.Recruit
{
    public static class LatchOps
    {
        public static SimResult TryLatch(MatchState m, PlayerState p, Catalog.Catalog cat, DestLoc from, int fromIndex, int hostIndex)
        {
            UnitInstance latch = null;
            if (from == DestLoc.Hand)
            {
                if (fromIndex < 0 || fromIndex >= p.Hand.Count) return SimResult.Fail("BAD_INDEX");
                latch = p.Hand[fromIndex];
            }
            else if (from == DestLoc.Board)
            {
                if (fromIndex < 0 || fromIndex >= p.Board.Count) return SimResult.Fail("BAD_INDEX");
                latch = p.Board[fromIndex];
            }
            else return SimResult.Fail("BAD_LOC");

            if (hostIndex < 0 || hostIndex >= p.Board.Count) return SimResult.Fail("BAD_HOST");
            UnitInstance host = p.Board[hostIndex];
            if (ReferenceEquals(host, latch) || host.InstanceId == latch.InstanceId)
                return SimResult.Fail("LATCH_SELF");

            UnitDef latchDef = cat.GetUnit(latch.CatalogId);
            UnitDef hostDef = cat.GetUnit(host.CatalogId);
            if (latchDef == null || !latch.Has(Keyword.Latch))
                return SimResult.Fail("NOT_LATCH");
            if (!LegalHost(latchDef, host, hostDef))
                return SimResult.Fail("BAD_HOST");
            if (from == DestLoc.Board)
            {
                int li = IndexOf(p.Board, latch);
                if (li < 0 || System.Math.Abs(li - hostIndex) != 1)
                    return SimResult.Fail("NOT_ADJACENT");
            }

            Attach(m, p, cat, host, latch, latchDef, hostDef, consumeOwned: true);
            p.LatchPlaysThisMatch++;
            m.LatchHost = host;
            EffectHooks.Fire(m, cat, Trigger.OnLatch, latch, p, host);
            m.LatchHost = null;
            Awaken.TryAwaken(m, p, cat);
            return SimResult.Success();
        }

        public static void AttachToken(MatchState m, PlayerState p, Catalog.Catalog cat, UnitInstance host, UnitId tokenId)
        {
            UnitDef latchDef = cat.GetUnit(tokenId);
            if (latchDef == null || host == null) return;
            UnitInstance latch = Units.Create(cat, m.Rng, tokenId);
            UnitDef hostDef = cat.GetUnit(host.CatalogId);
            Attach(m, p, cat, host, latch, latchDef, hostDef, consumeOwned: false);
            p.LatchPlaysThisMatch++;
            m.TokenSpawned++;
            m.TokenDestroyed++;
            m.LatchHost = host;
            EffectHooks.Fire(m, cat, Trigger.OnLatch, latch, p, host);
            m.LatchHost = null;
        }

        public static void Attach(MatchState m, PlayerState p, Catalog.Catalog cat, UnitInstance host, UnitInstance latch, UnitDef latchDef, UnitDef hostDef, bool consumeOwned)
        {
            int mulN = hostDef != null ? hostDef.OnLatchedMulN : 1;
            int mulD = hostDef != null && hostDef.OnLatchedMulD > 0 ? hostDef.OnLatchedMulD : 1;
            int addAtk = latch.Atk * mulN / mulD;
            int addHp = latch.Hp * mulN / mulD;
            host.Atk += addAtk;
            host.Hp += addHp;
            host.MaxHp += addHp;
            host.ExtraAtk += addAtk;
            host.ExtraHp += addHp;
            host.Keywords |= latch.Keywords;
            host.Latches.Add(new LatchAttachment
            {
                CatalogId = latch.CatalogId,
                Atk = latch.Atk,
                Hp = latch.Hp,
                Keywords = latch.Keywords
            });
            bool transfer = latchDef == null || latchDef.LatchTransferEffects;
            if (transfer)
            {
                var fx = latch.AllEffects(latchDef);
                for (int i = 0; i < fx.Count; i++)
                {
                    if (fx[i].Trigger == Trigger.Echo || fx[i].Trigger == Trigger.Kindle)
                        host.ExtraEffects.Add(fx[i]);
                }
                for (int i = 0; i < latch.ExtraEffects.Count; i++)
                    host.ExtraEffects.Add(latch.ExtraEffects[i]);
            }

            if (consumeOwned)
            {
                bool shopLegal = latchDef != null && !latchDef.Token;
                RemoveOwned(p, latch);
                if (shopLegal) m.ShopLatchDestroyed++;
            }
        }

        public static bool LegalHost(UnitDef latchDef, UnitInstance host, UnitDef hostDef)
        {
            LatchHost rule = latchDef != null ? latchDef.LatchHost : LatchHost.Gearwights;
            if (rule == LatchHost.Any) return true;
            if (hostDef != null) return hostDef.Chorus == Chorus.Gearwights;
            return false;
        }

        static int IndexOf(System.Collections.Generic.List<UnitInstance> list, UnitInstance u)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], u) || list[i].InstanceId == u.InstanceId)
                    return i;
            }
            return -1;
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
