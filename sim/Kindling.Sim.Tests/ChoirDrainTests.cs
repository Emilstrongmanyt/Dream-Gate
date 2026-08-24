using Kindling.Sim.Combat;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Kindling.Sim.Rng;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class ChoirDrainTests
    {
        [Fact]
        public void Fire_never_reenters_DrainDeaths_choir_on_7_board()
        {
            var rng = new MatchRng(21UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            UnitInstance choir = Units.Create(TestSupport.Cat, rng, new UnitId("ab_choir"));
            a.Board.Add(choir);
            for (int i = 0; i < 6; i++)
                a.Board.Add(Units.CreateRaw(rng, "ne_wall", 0, 12, Keyword.None));
            Assert.Equal(7, a.Board.Count);
            b.Board.Add(Units.CreateRaw(rng, "ne_wall", 4, 20, Keyword.None));
            b.Board.Add(Units.CreateRaw(rng, "ne_wall", 4, 20, Keyword.None));
            b.Board.Add(Units.CreateRaw(rng, "ne_wall", 4, 20, Keyword.None));
            var match = new MatchState { Rng = rng };
            CombatResult r = CombatSim.Run(match, a, b, rng, TestSupport.Cat);
            Assert.Equal(0, match.DrainReentryAttempts);

            int firstEcho = -1, firstAfter = -1, summonsAfterEcho = 0;
            int boardFullAfterglow = 0;
            for (int i = 0; i < r.Events.Count; i++)
            {
                CombatEvent e = r.Events[i];
                if (e.Op == CombatOp.Echo && e.SrcInstance == choir.InstanceId && firstEcho < 0)
                    firstEcho = i;
                if (e.Op == CombatOp.Afterglow && firstAfter < 0)
                    firstAfter = i;
                if (e.Op == CombatOp.Summon && firstEcho >= 0 && (firstAfter < 0 || i < firstAfter))
                    summonsAfterEcho++;
                if (e.Op == CombatOp.BoardFull && e.Note == "Afterglow")
                    boardFullAfterglow++;
            }
            Assert.True(firstEcho >= 0);
            if (firstAfter >= 0)
                Assert.True(firstEcho < firstAfter);
            Assert.True(summonsAfterEcho >= 1);
            Assert.True(boardFullAfterglow >= 1);
        }
    }
}
