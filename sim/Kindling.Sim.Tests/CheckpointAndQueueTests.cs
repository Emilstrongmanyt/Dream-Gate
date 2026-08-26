using Kindling.Sim.Match;
using Kindling.Sim.Model;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class CheckpointAndQueueTests
    {
        [Fact]
        public void Checkpoint_restores_hand_and_embers()
        {
            var s = MatchSession.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 9u, 1);
            s.StartRecruit();
            PlayerState p = s.Loop.Human;
            p.Stall[0] = Kindling.Sim.Recruit.Units.Create(TestSupport.Cat, s.Loop.State.Rng, new UnitId("ck_urchin"));
            s.Handle(0, "{\"op\":\"Buy\",\"seq\":2,\"stallIndex\":0,\"dest\":\"Hand\",\"destIndex\":0}");
            Assert.Single(p.Hand);
            int embers = p.Embers;
            int round = s.Loop.State.Round;
            string blob = CheckpointMatch.Save(s);
            Assert.Contains("ck_urchin", blob);

            MatchSession restored = CheckpointMatch.Load(TestSupport.Cat, blob);
            Assert.Equal(round, restored.Loop.State.Round);
            Assert.Equal(Phase.Recruit, restored.Loop.State.Phase);
            Assert.Equal(embers, restored.Loop.Human.Embers);
            Assert.Single(restored.Loop.Human.Hand);
            Assert.Equal("ck_urchin", restored.Loop.Human.Hand[0].CatalogId.Value);
        }

        [Fact]
        public void Casual_queue_starts_1v7()
        {
            var q = new CasualQueue();
            MatchSession s = q.Enqueue(TestSupport.Cat, "Ada", 11u);
            Assert.Equal(1, q.LiveCount);
            Assert.False(s.Loop.State.Seats[0].IsBot);
            Assert.Equal("Ada", s.Loop.State.Seats[0].DisplayName);
            int bots = 0;
            for (int i = 0; i < s.Loop.State.Seats.Length; i++)
                if (s.Loop.State.Seats[i].IsBot) bots++;
            Assert.Equal(7, bots);
            Assert.Same(s, q.Get(s.Loop.State.MatchId.ToString("D")));
        }

        [Fact]
        public void Casual_queue_tick_advances_expired_timer()
        {
            var q = new CasualQueue();
            MatchSession s = q.Enqueue(TestSupport.Cat, "Ada", 12u);
            s.StartRecruit();
            int round = s.Loop.State.Round;
            int n = q.TickAll(s.RecruitEndsAtUtc.AddSeconds(2));
            Assert.True(n >= 1);
            Assert.True(s.Loop.State.Round > round || s.Loop.State.MatchOver);
        }

        [Fact]
        public void Reconnect_returns_snapshot()
        {
            var s = MatchSession.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 13u, 1);
            string snap = s.Handle(0, "{\"op\":\"Reconnect\"}");
            Assert.Contains("\"op\":\"Snapshot\"", snap);
            Assert.Contains("\"phase\":", snap);
        }
    }
}
