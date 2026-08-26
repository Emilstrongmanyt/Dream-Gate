using Kindling.Sim;
using Kindling.Sim.Match;
using Kindling.Sim.Model;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class MatchSessionTests
    {
        [Fact]
        public void Join_returns_welcome_then_snapshot()
        {
            var s = MatchSession.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 3u, 1);
            string w = s.Handle(0, "{\"op\":\"Join\"}");
            Assert.Contains("\"op\":\"Welcome\"", w);
            Assert.Contains("\"seat\":0", w);
            Assert.Contains("deviceResumeToken", w);
        }

        [Fact]
        public void Buy_via_protocol_enters_hand()
        {
            var s = MatchSession.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 3u, 1);
            s.Handle(0, "{\"op\":\"CaptainPick\",\"seq\":1,\"offerIndex\":0}");
            if (s.Loop.State.Phase == Phase.CaptainPick)
                s.StartRecruit();
            PlayerState p = s.Loop.Human;
            p.Stall[0] = Kindling.Sim.Recruit.Units.Create(TestSupport.Cat, s.Loop.State.Rng, new UnitId("ck_urchin"));
            string snap = s.Handle(0, "{\"op\":\"Buy\",\"seq\":2,\"stallIndex\":0,\"dest\":\"Hand\",\"destIndex\":0}");
            Assert.DoesNotContain("\"op\":\"Error\"", snap);
            Assert.Single(p.Hand);
            Assert.Contains("ck_urchin", snap);
        }

        [Fact]
        public void Duplicate_seq_is_rejected()
        {
            var s = MatchSession.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 4u, 1);
            s.Handle(0, "{\"op\":\"CaptainPick\",\"seq\":1,\"offerIndex\":0}");
            string again = s.Handle(0, "{\"op\":\"CaptainPick\",\"seq\":1,\"offerIndex\":0}");
            Assert.Contains("DUP", again);
        }

        [Fact]
        public void Timer_expiry_starts_combat_and_next_recruit()
        {
            var s = MatchSession.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 5u, 1);
            s.StartRecruit();
            int round = s.Loop.State.Round;
            Assert.Equal(Phase.Recruit, s.Loop.State.Phase);
            bool moved = s.Tick(s.RecruitEndsAtUtc.AddSeconds(1));
            Assert.True(moved);
            Assert.True(s.Loop.State.Round > round || s.Loop.State.MatchOver);
        }

        [Fact]
        public void Protocol_parses_play_and_latch()
        {
            RecruitAction play = Protocol.Parse("{\"op\":\"Play\",\"seq\":8,\"handIndex\":1,\"destIndex\":3}", 0);
            Assert.Equal(RecruitOp.Play, play.Op);
            Assert.Equal(1, play.HandIndex);
            Assert.Equal(3, play.DestIndex);
            RecruitAction latch = Protocol.Parse("{\"op\":\"Latch\",\"seq\":9,\"from\":\"Hand\",\"fromIndex\":0,\"hostIndex\":2}", 0);
            Assert.Equal(RecruitOp.Latch, latch.Op);
            Assert.Equal(DestLoc.Hand, latch.From);
            Assert.Equal(2, latch.HostIndex);
        }
    }
}
