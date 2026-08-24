using Kindling.Sim.Combat;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Kindling.Sim.Rng;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class PersistAndSovereignTests
    {
        [Fact]
        public void Tally_rat_PendingEmbers_persists()
        {
            var rng = new MatchRng(11UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            a.Board.Add(Units.Create(TestSupport.Cat, rng, new UnitId("ck_tally")));
            b.Board.Add(Units.CreateRaw(rng, "ne_porter", 3, 3, Keyword.None));
            Assert.Equal(0, a.PendingEmbers);
            CombatResult r = CombatSim.Run(a, b, rng, TestSupport.Cat);
            Assert.Equal(1, a.PendingEmbers);
            Assert.Single(a.Board);
            Assert.Equal("ck_tally", a.Board[0].CatalogId.Value);
            _ = r;
        }

        [Fact]
        public void Urn_kin_buff_does_not_persist()
        {
            var rng = new MatchRng(12UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            UnitInstance urn = Units.Create(TestSupport.Cat, rng, new UnitId("ab_urn"));
            UnitInstance pal = Units.Create(TestSupport.Cat, rng, new UnitId("ne_porter"));
            a.Board.Add(urn);
            a.Board.Add(pal);
            b.Board.Add(Units.CreateRaw(rng, "ne_wall", 8, 8, Keyword.None));
            int extraBefore = pal.ExtraAtk;
            CombatSim.Run(a, b, rng, TestSupport.Cat);
            Assert.Equal(extraBefore, pal.ExtraAtk);
            Assert.Equal(2, pal.Atk);
        }

        [Fact]
        public void Sovereign_pending_before_burn()
        {
            var rng = new MatchRng(13UL);
            var m = new MatchState { Rng = rng, Round = 3 };
            Pool.Init(m, TestSupport.Cat);
            PlayerState p = m.Seats[0];
            p.Embers = 7;
            p.RerollsThisRecruit = 5;
            p.Board.Add(Units.Create(TestSupport.Cat, rng, new UnitId("ck_sovereign")));
            Grant.RecruitEnd(m, p, TestSupport.Cat);
            Assert.Equal(3, p.PendingEmbers);
            Assert.Equal(0, p.Embers);
            Assert.Equal(0, p.RerollsThisRecruit);
        }
    }
}
