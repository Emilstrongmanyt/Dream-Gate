using Kindling.Sim.Combat;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Kindling.Sim.Rng;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class CombatGoldenTests
    {
        [Fact]
        public void Mutual_kill_is_draw_zero_damage()
        {
            var rng = new MatchRng(1UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            a.Board.Add(Units.CreateRaw(rng, "ne_porter", 2, 2, Keyword.None));
            b.Board.Add(Units.CreateRaw(rng, "ne_porter", 2, 2, Keyword.None));
            CombatResult r = CombatSim.Run(a, b, rng, TestSupport.Cat);
            Assert.True(r.Draw);
            Assert.Equal(0, r.Damage);
            Assert.Equal(0, r.RemainingA);
            Assert.Equal(0, r.RemainingB);
            Assert.Single(r.BoardA);
            Assert.Single(r.BoardB);
            Assert.Equal(0, r.SeatA);
            Assert.Equal(1, r.SeatB);
        }

        [Fact]
        public void Ward_targeting()
        {
            var rng = new MatchRng(2UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            var bait = Units.CreateRaw(rng, "ck_urchin", 1, 10, Keyword.None);
            var ward = Units.CreateRaw(rng, "ne_warden", 1, 10, Keyword.Ward);
            a.Board.Add(bait);
            a.Board.Add(ward);
            var atk = Units.CreateRaw(rng, "ne_wall", 5, 5, Keyword.None);
            var atk2 = Units.CreateRaw(rng, "ne_wall", 5, 5, Keyword.None);
            var atk3 = Units.CreateRaw(rng, "ne_wall", 5, 5, Keyword.None);
            b.Board.Add(atk);
            b.Board.Add(atk2);
            b.Board.Add(atk3);
            CombatResult r = CombatSim.Run(a, b, rng, TestSupport.Cat);
            bool firstHitWard = false;
            for (int i = 0; i < r.Events.Count; i++)
            {
                CombatEvent e = r.Events[i];
                if (e.Op != CombatOp.Attack) continue;
                if (e.DstSeat != a.Seat) continue;
                Assert.Equal(ward.InstanceId, e.DstInstance);
                firstHitWard = true;
                break;
            }
            Assert.True(firstHitWard);
        }

        [Fact]
        public void Draw_zero_atk_zero_damage()
        {
            var rng = new MatchRng(3UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            a.Board.Add(Units.CreateRaw(rng, "ne_wall", 0, 5, Keyword.None));
            b.Board.Add(Units.CreateRaw(rng, "ne_wall", 0, 5, Keyword.None));
            CombatResult r = CombatSim.Run(a, b, rng, TestSupport.Cat);
            Assert.True(r.Draw);
            Assert.Equal(0, r.Damage);
            Assert.Equal(1, r.RemainingA);
            Assert.Equal(1, r.RemainingB);
        }

        [Fact]
        public void Aegis_blocks_Venom()
        {
            var rng = new MatchRng(4UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            a.Board.Add(Units.CreateRaw(rng, "gt_skulk", 2, 1, Keyword.Venom));
            b.Board.Add(Units.CreateRaw(rng, "ne_porter", 2, 2, Keyword.Aegis));
            CombatResult r = CombatSim.Run(a, b, rng, TestSupport.Cat);
            bool aegis = false;
            bool venom = false;
            for (int i = 0; i < r.Events.Count; i++)
            {
                if (r.Events[i].Op == CombatOp.AegisBreak) aegis = true;
                if (r.Events[i].Op == CombatOp.Venom) venom = true;
            }
            Assert.True(aegis);
            Assert.False(venom);
            Assert.False(r.Draw);
            Assert.Equal(b.Seat, r.WinnerSeat);
        }

        [Fact]
        public void Empty_board_loses_and_takes_ring_damage()
        {
            var rng = new MatchRng(5UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            b.Board.Add(Units.Create(TestSupport.Cat, rng, new UnitId("ne_porter")));
            CombatResult r = CombatSim.Run(a, b, rng, TestSupport.Cat);
            Assert.False(r.Draw);
            Assert.Equal(b.Seat, r.WinnerSeat);
            Assert.Equal(0, r.RemainingA);
            Assert.Equal(1, r.RemainingB);
            Assert.Equal(1 + 1, r.Damage);
        }

        [Fact]
        public void Venom_kills_through_high_hp()
        {
            var rng = new MatchRng(6UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            a.Board.Add(Units.CreateRaw(rng, "gt_skulk", 2, 4, Keyword.Venom));
            b.Board.Add(Units.CreateRaw(rng, "ne_wall", 0, 20, Keyword.None));
            CombatResult r = CombatSim.Run(a, b, rng, TestSupport.Cat);
            bool venom = false;
            for (int i = 0; i < r.Events.Count; i++)
                if (r.Events[i].Op == CombatOp.Venom) venom = true;
            Assert.True(venom);
            Assert.Equal(a.Seat, r.WinnerSeat);
        }

        [Fact]
        public void Afterglow_leaves_a_body()
        {
            var rng = new MatchRng(7UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            a.Board.Add(Units.CreateRaw(rng, "ab_cinderling", 1, 1, Keyword.Afterglow));
            b.Board.Add(Units.CreateRaw(rng, "ne_wall", 5, 5, Keyword.None));
            CombatResult r = CombatSim.Run(a, b, rng, TestSupport.Cat);
            bool ag = false;
            for (int i = 0; i < r.Events.Count; i++)
                if (r.Events[i].Op == CombatOp.Afterglow) ag = true;
            Assert.True(ag);
        }
    }
}
