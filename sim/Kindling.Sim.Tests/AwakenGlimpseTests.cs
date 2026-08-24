using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Kindling.Sim.Rng;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class AwakenGlimpseTests
    {
        [Fact]
        public void Smelter_x3_awaken_cinders_not_doubled()
        {
            var rng = new MatchRng(9UL);
            var m = new MatchState { Rng = rng };
            var p = m.Seats[0];
            p.Depth = 4;
            for (int i = 0; i < 3; i++)
            {
                UnitInstance u = Units.Create(TestSupport.Cat, rng, new UnitId("ne_smelter"));
                Units.GiveCinder(u, 2);
                Assert.Equal(2, u.Cinders);
                Assert.Equal(2, u.ExtraAtk);
                p.Board.Add(u);
            }
            bool ok = Awaken.TryAwaken(m, p, TestSupport.Cat);
            Assert.True(ok);
            Assert.Single(p.Board);
            UnitInstance aw = p.Board[0];
            Assert.True(aw.Awakened);
            Assert.Equal(6, aw.ExtraAtk);
            Assert.Equal(6, aw.ExtraHp);
            Assert.Equal(6, aw.Cinders);
            Assert.Equal(2 * 3 + 6, aw.Atk);
            Assert.Equal(2 * 5 + 6, aw.Hp);
            Assert.NotEqual(2 * 3 + 12, aw.Atk);
        }

        [Fact]
        public void Empty_glimpse_logs_GlimpseEmpty()
        {
            var rng = new MatchRng(8UL);
            var m = new MatchState { Rng = rng };
            Pool.Init(m, TestSupport.Cat);
            for (int i = 0; i < m.Pool.Count; i++)
                m.Pool[i].Remaining = 0;
            var p = m.Seats[0];
            p.Depth = 6;
            Glimpse.Enqueue(m, p, TestSupport.Cat, null, DepthMode.Current, 6);
            Assert.True(m.HasLog("GlimpseEmpty"));
            Assert.False(p.HasFlag(PlayerFlags.GlimpseOpen));
            Assert.Empty(p.GlimpseQueue);
        }
    }
}
