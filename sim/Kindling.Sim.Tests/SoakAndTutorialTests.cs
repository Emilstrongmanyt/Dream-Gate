using Kindling.Sim.Match;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class SoakAndTutorialTests
    {
        [Fact]
        public void Eight_headless_matches_assign_places()
        {
            for (ulong seed = 1; seed <= 8; seed++)
            {
                MatchLoop loop = MatchLoop.CreateHeadless(TestSupport.Cat, seed);
                loop.RunToEnd();
                Assert.True(loop.State.MatchOver);
                bool[] seen = new bool[9];
                int n = 0;
                for (int i = 0; i < loop.State.Seats.Length; i++)
                {
                    int place = loop.State.Seats[i].Place ?? 0;
                    Assert.InRange(place, 1, 8);
                    if (!seen[place]) n++;
                    seen[place] = true;
                }
                Assert.Equal(8, n);
            }
        }

        [Fact]
        public void Tutorial_wick_floor_keeps_human_alive()
        {
            MatchLoop loop = MatchLoop.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 9u, 1);
            loop.TutorialWickFloor = true;
            loop.StartFromCaptainPick();
            loop.Human.Wick = 0;
            loop.PlaceNewlyDead();
            Assert.Equal(1, loop.Human.Wick);
            Assert.False(loop.Human.Place.HasValue);
        }

        [Fact]
        public void Cosmetics_cycle_and_grant()
        {
            Assert.Equal("ember", Cosmetics.NextFrame("gold"));
            Assert.True(Cosmetics.IsFrame("night"));
            Assert.False(Cosmetics.IsFrame("legendary"));
            string json = AccountAuth.CreateAccount("a", "Ada", "ada", "s", "h", "");
            string next = Cosmetics.PatchEquip(json, "ember");
            Assert.Contains("ember", Protocol.ReadString(next, "cosmetics"));
            Assert.Equal("ember", Protocol.ReadString(next, "frame"));
            string all = AccountAuth.WithCosmetic(next, Cosmetics.GrantAll(), "gold");
            Assert.Contains("spirit", Protocol.ReadString(all, "cosmetics"));
        }
    }
}
