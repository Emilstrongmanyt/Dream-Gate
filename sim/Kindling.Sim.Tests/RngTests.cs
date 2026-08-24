using Kindling.Sim.Match;
using Kindling.Sim.Rng;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class RngTests
    {
        [Fact]
        public void FixtureSeed_is_deterministic()
        {
            var a = new MatchRng(12345UL);
            var b = new MatchRng(12345UL);
            for (int i = 0; i < 50; i++)
            {
                Assert.Equal(
                    a.Range(MatchRng.Stream.Combat, 0, 1000),
                    b.Range(MatchRng.Stream.Combat, 0, 1000));
            }
        }

        [Fact]
        public void Streams_are_independent()
        {
            var rng = new MatchRng(99UL);
            int stall = rng.Range(MatchRng.Stream.Stall, 0, 100);
            var other = new MatchRng(99UL);
            int combat = other.Range(MatchRng.Stream.Combat, 0, 100);
            int stall2 = other.Range(MatchRng.Stream.Stall, 0, 100);
            Assert.Equal(stall, stall2);
            Assert.NotEqual(stall, combat);
        }

        [Fact]
        public void Serialize_deserialize_continues_pcg()
        {
            var rng = new MatchRng(777UL);
            rng.NextId();
            int first = rng.Range(MatchRng.Stream.Combat, 0, 10000);
            int stallFirst = rng.Range(MatchRng.Stream.Stall, 0, 50);
            string json = Checkpoint.SerializeRng(rng);
            Assert.Contains("\"s0\":", json);
            Assert.Contains("nextInstanceId", json);

            MatchRng restored = Checkpoint.DeserializeRng(json);
            int a = restored.Range(MatchRng.Stream.Combat, 0, 10000);
            int b = restored.Range(MatchRng.Stream.Stall, 0, 50);

            var control = new MatchRng(777UL);
            control.NextId();
            control.Range(MatchRng.Stream.Combat, 0, 10000);
            control.Range(MatchRng.Stream.Stall, 0, 50);
            Assert.Equal(control.Range(MatchRng.Stream.Combat, 0, 10000), a);
            Assert.Equal(control.Range(MatchRng.Stream.Stall, 0, 50), b);
            Assert.Equal(control.NextInstanceId, restored.NextInstanceId);
            Assert.NotEqual(first, a);
            Assert.NotEqual(stallFirst, b);
        }

        [Fact]
        public void Match_create_seed_is_stable()
        {
            var id = new System.Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            MatchRng a = MatchRng.Create(id, 42);
            MatchRng b = MatchRng.Create(id, 42);
            Assert.Equal(a.Range(MatchRng.Stream.Glimpse, 0, 100), b.Range(MatchRng.Stream.Glimpse, 0, 100));
            MatchRng c = MatchRng.Create(id, 43);
            Assert.NotEqual(a.Range(MatchRng.Stream.Bot, 0, 1000), c.Range(MatchRng.Stream.Bot, 0, 1000));
        }

        [Fact]
        public void Bit_and_shuffle_are_deterministic()
        {
            var a = new MatchRng(5UL);
            var b = new MatchRng(5UL);
            Assert.Equal(a.Bit(MatchRng.Stream.Combat), b.Bit(MatchRng.Stream.Combat));
            var la = new System.Collections.Generic.List<int> { 1, 2, 3, 4, 5 };
            var lb = new System.Collections.Generic.List<int> { 1, 2, 3, 4, 5 };
            a.Shuffle(MatchRng.Stream.Glimpse, la);
            b.Shuffle(MatchRng.Stream.Glimpse, lb);
            Assert.Equal(la, lb);
        }
    }
}
