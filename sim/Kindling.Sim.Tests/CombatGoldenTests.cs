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
    }
}
