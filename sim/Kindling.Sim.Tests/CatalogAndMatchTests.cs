using System.Collections.Generic;
using Kindling.Sim.Match;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Kindling.Sim.Rng;
using Kindling.Sim.Seasons;
using Kindling.Sim.Validation;
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
            Assert.Equal(7, cat.Spells.Count);
            Assert.Equal(16, cat.Captains.Count);
            Assert.NotNull(cat.GetUnit("ck_urchin"));
            Assert.NotNull(cat.GetUnit("ne_smelter"));
            Assert.NotNull(cat.GetUnit("ck_sovereign"));
            Assert.NotNull(cat.GetUnit("tok_dummy"));
            Assert.NotNull(cat.GetUnit("sp_whet"));
            Assert.True(cat.GetUnit("sp_whet").Spell);
            Assert.Equal(Chorus.Undead, cat.GetUnit("ab_cinderling").Chorus);
            Assert.Equal(Chorus.Beast, cat.GetUnit("gt_skulk").Chorus);
            Assert.Equal(Chorus.Humanoid, cat.GetUnit("ck_urchin").Chorus);
            Assert.Equal(Chorus.Humanoid, cat.GetUnit("gw_cog").Chorus);
            Assert.Equal(Chorus.Dragon, cat.GetUnit("ne_lantern").Chorus);
            Assert.Equal(Chorus.Spirit, cat.GetUnit("ne_echoist").Chorus);
            Assert.Equal(LatchHost.Humanoid, cat.GetUnit("gw_cog").LatchHost);
            Assert.NotNull(cat.GetCaptain("cap_vesper"));
            Assert.NotNull(cat.GetCaptain("cap_dredger"));
            Assert.NotNull(cat.GetCaptain("cap_debt"));
            Assert.Contains(CaptainPassive.SkivBeastOnBuyPlus1Atk, cat.GetCaptain("cap_skiv").Passives);
            Assert.Equal("none", cat.Season.Id);
        }

        [Fact]
        public void Spell_play_casts_and_leaves_board()
        {
            var cat = TestSupport.Cat;
            MatchLoop loop = MatchLoop.Create(cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 1u, 1);
            loop.StartFromCaptainPick();
            PlayerState p = loop.Human;
            p.Board.Add(Units.Create(cat, loop.State.Rng, new UnitId("ck_urchin")));
            int atk = p.Board[0].Atk;
            p.Hand.Add(Units.Create(cat, loop.State.Rng, new UnitId("sp_whet")));
            SimResult r = loop.Try(new RecruitAction
            {
                Op = RecruitOp.Play,
                Seat = p.Seat,
                HandIndex = 0,
                Dest = DestLoc.Board,
                DestIndex = 0
            });
            Assert.True(r.Ok, r.Code);
            Assert.Empty(p.Hand);
            Assert.Single(p.Board);
            Assert.Equal(atk + 1, p.Board[0].Atk);
        }

        [Fact]
        public void Spells_do_not_awaken()
        {
            var rng = new MatchRng(11UL);
            var m = new MatchState { Rng = rng };
            var p = m.Seats[0];
            for (int i = 0; i < 3; i++)
                p.Hand.Add(Units.Create(TestSupport.Cat, rng, new UnitId("sp_mark")));
            Assert.False(Awaken.TryAwaken(m, p, TestSupport.Cat));
            Assert.Equal(3, p.Hand.Count);
            Assert.Empty(p.Board);
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

        [Fact]
        public void Buy_always_enters_hand_not_board()
        {
            var cat = TestSupport.Cat;
            MatchLoop loop = MatchLoop.Create(cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 1u, 1);
            loop.StartFromCaptainPick();
            PlayerState p = loop.Human;
            Assert.Equal(Phase.Recruit, loop.State.Phase);
            int stall = 0;
            p.Stall[0] = Units.Create(cat, loop.State.Rng, new UnitId("ck_urchin"));
            string id = p.Stall[stall].CatalogId.Value;
            SimResult r = loop.Try(new RecruitAction
            {
                Op = RecruitOp.Buy,
                Seat = p.Seat,
                StallIndex = stall,
                Dest = DestLoc.Board,
                DestIndex = 0
            });
            Assert.True(r.Ok, r.Code);
            Assert.Empty(p.Board);
            Assert.Single(p.Hand);
            Assert.Equal(id, p.Hand[0].CatalogId.Value);

            r = loop.Try(new RecruitAction
            {
                Op = RecruitOp.Play,
                Seat = p.Seat,
                HandIndex = 0,
                DestIndex = 0,
                Dest = DestLoc.Board
            });
            Assert.True(r.Ok, r.Code);
            Assert.Single(p.Board);
            Assert.Empty(p.Hand);
        }
    }
}
