using System.Collections.Generic;
using Kindling.Sim;
using Kindling.Sim.Catalog;
using Kindling.Sim.Match;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Kindling.Sim.Validation;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class PoolInvariantTests
    {
        [Fact]
        public void Pool_matches_shop_units_and_copy_limits()
        {
            var m = new MatchState();
            Pool.Init(m, TestSupport.Cat);
            int shop = 0;
            foreach (UnitDef _ in TestSupport.Cat.ShopUnits()) shop++;
            Assert.Equal(shop, m.Pool.Count);
            for (int i = 0; i < m.Pool.Count; i++)
            {
                PoolEntry e = m.Pool[i];
                UnitDef def = TestSupport.Cat.GetUnit(e.Id);
                Assert.NotNull(def);
                Assert.False(def.Token);
                Assert.False(def.Disabled);
                Assert.Equal(def.CopyLimit, e.Remaining);
            }
        }

        [Fact]
        public void Tokens_are_not_pooled()
        {
            var m = new MatchState();
            Pool.Init(m, TestSupport.Cat);
            for (int i = 0; i < TestSupport.Cat.Tokens.Count; i++)
            {
                UnitDef t = TestSupport.Cat.Tokens[i];
                PoolEntry e = m.GetPool(t.Id);
                Assert.Null(e);
            }
        }

        [Fact]
        public void Spells_are_pooled()
        {
            var m = new MatchState();
            Pool.Init(m, TestSupport.Cat);
            Assert.True(TestSupport.Cat.Spells.Count > 0);
            for (int i = 0; i < TestSupport.Cat.Spells.Count; i++)
            {
                UnitDef s = TestSupport.Cat.Spells[i];
                PoolEntry e = m.GetPool(s.Id);
                Assert.NotNull(e);
                Assert.Equal(s.CopyLimit, e.Remaining);
            }
        }

        [Fact]
        public void Sell_returns_a_shop_copy_to_the_pool()
        {
            var cat = TestSupport.Cat;
            MatchLoop loop = MatchLoop.Create(cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 9u, 1);
            loop.StartFromCaptainPick();
            PlayerState p = loop.Human;
            p.Stall[0] = Units.Create(cat, loop.State.Rng, new UnitId("ck_urchin"));
            int before = loop.State.GetPool(new UnitId("ck_urchin")).Remaining;
            Assert.True(loop.Try(new RecruitAction
            {
                Op = RecruitOp.Buy,
                Seat = p.Seat,
                StallIndex = 0,
                Dest = DestLoc.Hand,
                DestIndex = 0
            }).Ok);
            Assert.Equal(before, loop.State.GetPool(new UnitId("ck_urchin")).Remaining);
            Assert.True(loop.Try(new RecruitAction
            {
                Op = RecruitOp.Sell,
                Seat = p.Seat,
                Loc = DestLoc.Hand,
                Index = 0
            }).Ok);
            Assert.Equal(before + 1, loop.State.GetPool(new UnitId("ck_urchin")).Remaining);
        }

        [Fact]
        public void Headless_places_unique_across_seeds()
        {
            ulong[] seeds = { 1, 2, 7, 11, 42, 99, 128, 256 };
            for (int s = 0; s < seeds.Length; s++)
            {
                MatchLoop loop = MatchLoop.CreateHeadless(TestSupport.Cat, seeds[s]);
                loop.RunToEnd();
                Assert.True(loop.State.MatchOver);
                var seen = new HashSet<int>();
                for (int i = 0; i < loop.State.Seats.Length; i++)
                {
                    Assert.True(loop.State.Seats[i].Place.HasValue);
                    Assert.True(seen.Add(loop.State.Seats[i].Place.Value));
                }
                Assert.Equal(8, seen.Count);
            }
        }
    }
}
