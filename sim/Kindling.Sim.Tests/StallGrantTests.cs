using Kindling.Sim.Catalog;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Kindling.Sim.Rng;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class StallGrantTests
    {
        [Fact]
        public void Copy_weighted_draw_A1_B99()
        {
            var cat = new Catalog.Catalog();
            cat.AddUnit(new UnitDef { Id = new UnitId("u_a"), Name = "A", Depth = 1, Atk = 1, Hp = 1 });
            cat.AddUnit(new UnitDef { Id = new UnitId("u_b"), Name = "B", Depth = 1, Atk = 1, Hp = 1 });
            var m = new MatchState();
            m.Rng = new MatchRng(1UL);
            m.Pool.Add(new PoolEntry { Id = new UnitId("u_a"), Remaining = 1 });
            m.Pool.Add(new PoolEntry { Id = new UnitId("u_b"), Remaining = 99 });
            m.SortPool();
            var p = TestSupport.Player();
            p.Depth = 1;
            int b = 0;
            const int n = 10000;
            for (int i = 0; i < n; i++)
            {
                m.GetPool(new UnitId("u_a")).Remaining = 1;
                m.GetPool(new UnitId("u_b")).Remaining = 99;
                UnitId id = Pool.DrawWeighted(m, cat, p, m.Rng);
                if (id.Value == "u_b") b++;
            }
            double ratio = b / (double)n;
            Assert.InRange(ratio, 0.98, 1.00);
        }

        [Fact]
        public void GrantEmbers_R1_is_3()
        {
            var p = TestSupport.Player();
            Grant.GrantEmbers(p, 1, TestSupport.Cat);
            Assert.Equal(3, p.Embers);
        }

        [Fact]
        public void GrantEmbers_Debt_plus1_inside_cap()
        {
            var p = TestSupport.Player();
            p.Captain = new CaptainId("cap_debt");
            Grant.GrantEmbers(p, 1, TestSupport.Cat);
            Assert.Equal(4, p.Embers);
            Grant.GrantEmbers(p, 8, TestSupport.Cat);
            Assert.Equal(10, p.Embers);
        }

        [Fact]
        public void GrantEmbers_Dredger_next_grant()
        {
            var p = TestSupport.Player();
            p.Captain = new CaptainId("cap_dredger");
            Grant.GrantEmbers(p, 1, TestSupport.Cat);
            Assert.Equal(3, p.Embers);
            p.DredgerBonus = 2;
            p.Embers = 0;
            Grant.GrantEmbers(p, 2, TestSupport.Cat);
            Assert.Equal(6, p.Embers);
            Assert.Equal(0, p.DredgerBonus);
        }

        [Fact]
        public void PendingEmbers_not_eaten_by_hardCap()
        {
            var p = TestSupport.Player();
            p.PendingEmbers = 5;
            Grant.GrantEmbers(p, 8, TestSupport.Cat);
            Assert.Equal(15, p.Embers);
            Assert.Equal(0, p.PendingEmbers);

            p.PendingEmbers = 5;
            p.DredgerBonus = 2;
            Grant.GrantEmbers(p, 8, TestSupport.Cat);
            Assert.Equal(17, p.Embers);
        }
    }
}
