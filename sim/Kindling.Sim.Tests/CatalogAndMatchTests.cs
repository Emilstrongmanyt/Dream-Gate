using System.Collections.Generic;
using Kindling.Sim.Match;
using Kindling.Sim.Seasons;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class CatalogAndMatchTests
    {
        [Fact]
        public void Catalog_loads_full_mvp_roster()
        {
            var cat = TestSupport.Cat;
            Assert.Equal(48, cat.Units.Count);
            Assert.Equal(7, cat.Tokens.Count);
            Assert.Equal(12, cat.Captains.Count);
            Assert.NotNull(cat.GetUnit("ck_urchin"));
            Assert.NotNull(cat.GetUnit("ne_smelter"));
            Assert.NotNull(cat.GetUnit("ck_sovereign"));
            Assert.NotNull(cat.GetUnit("tok_dummy"));
            Assert.NotNull(cat.GetCaptain("cap_vesper"));
            Assert.NotNull(cat.GetCaptain("cap_dredger"));
            Assert.NotNull(cat.GetCaptain("cap_debt"));
            Assert.Equal("none", cat.Season.Id);
        }

        [Fact]
        public void SeasonNone_id()
        {
            var s = new SeasonNone();
            Assert.Equal("none", s.Id);
        }

        [Fact]
        public void Headless_8_bot_match_assigns_places_1_to_8()
        {
            MatchLoop loop = MatchLoop.CreateHeadless(TestSupport.Cat, 42UL);
            loop.RunToEnd();
            Assert.True(loop.State.MatchOver);
            var seen = new HashSet<int>();
            for (int i = 0; i < loop.State.Seats.Length; i++)
            {
                Assert.True(loop.State.Seats[i].Place.HasValue, "seat " + i + " missing place");
                int place = loop.State.Seats[i].Place.Value;
                Assert.InRange(place, 1, 8);
                Assert.True(seen.Add(place), "duplicate place " + place);
            }
            Assert.Equal(8, seen.Count);
        }
    }
}
