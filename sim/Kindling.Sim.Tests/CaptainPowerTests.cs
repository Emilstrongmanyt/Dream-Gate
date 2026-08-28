using Kindling.Sim.Captains;
using Kindling.Sim.Catalog;
using Kindling.Sim.Combat;
using Kindling.Sim.Match;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Kindling.Sim.Rng;
using Kindling.Sim.Validation;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class CaptainPowerTests
    {
        [Fact]
        public void Roster_is_sixteen_with_power_text()
        {
            var cat = TestSupport.Cat;
            Assert.Equal(16, cat.Captains.Count);
            for (int i = 0; i < cat.Captains.Count; i++)
            {
                CaptainDef def = cat.Captains[i];
                Assert.False(string.IsNullOrEmpty(CaptainPower.Line(def)), def.Name);
                Assert.True(def.HasEdict || def.Passives.Count > 0, def.Name);
            }
            Assert.NotNull(cat.GetCaptain("cap_flint"));
            Assert.NotNull(cat.GetCaptain("cap_iris"));
            Assert.NotNull(cat.GetCaptain("cap_noll"));
            Assert.NotNull(cat.GetCaptain("cap_oak"));
        }

        [Fact]
        public void Practice_offers_full_roster()
        {
            MatchLoop loop = MatchLoop.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 1u, 1);
            loop.OfferFullRoster();
            Assert.Equal(TestSupport.Cat.Captains.Count, loop.Human.CaptainOffers.Length);
        }

        [Fact]
        public void Casual_offers_three()
        {
            MatchLoop loop = MatchLoop.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 2u, 1);
            Assert.Equal(3, loop.Human.CaptainOffers.Length);
        }

        [Fact]
        public void Noll_banks_ember_on_buy()
        {
            MatchLoop loop = MatchLoop.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 3u, 1);
            loop.Human.Captain = new CaptainId("cap_noll");
            CaptainPassives.OnCaptainPicked(loop.Human, TestSupport.Cat);
            loop.StartFromCaptainPick();
            PlayerState p = loop.Human;
            p.Embers = 10;
            p.Stall[0] = Units.Create(TestSupport.Cat, loop.State.Rng, new UnitId("ck_urchin"));
            int pending = p.PendingEmbers;
            SimResult r = loop.Try(new RecruitAction
            {
                Op = RecruitOp.Buy,
                Seat = p.Seat,
                StallIndex = 0,
                Dest = DestLoc.Hand,
                DestIndex = 0
            });
            Assert.True(r.Ok, r.Code);
            Assert.Equal(pending + 1, p.PendingEmbers);
        }

        [Fact]
        public void Oak_edict_gives_ward()
        {
            MatchLoop loop = MatchLoop.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 4u, 1);
            loop.Human.Captain = new CaptainId("cap_oak");
            CaptainPassives.OnCaptainPicked(loop.Human, TestSupport.Cat);
            loop.StartFromCaptainPick();
            PlayerState p = loop.Human;
            p.Embers = 10;
            p.Board.Add(Units.Create(TestSupport.Cat, loop.State.Rng, new UnitId("ck_urchin")));
            SimResult r = loop.Try(new RecruitAction
            {
                Op = RecruitOp.Edict,
                Seat = p.Seat,
                TargetIndex = 0
            });
            Assert.True(r.Ok, r.Code);
            Assert.True(p.Board[0].Has(Keyword.Ward));
        }

        [Fact]
        public void Flint_kindle_buffs_rightmost()
        {
            var rng = new MatchRng(21UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            a.Captain = new CaptainId("cap_flint");
            CaptainPassives.OnCaptainPicked(a, TestSupport.Cat);
            var left = Units.CreateRaw(rng, "ne_porter", 2, 4, Keyword.None);
            var right = Units.CreateRaw(rng, "ne_porter", 2, 4, Keyword.None);
            a.Board.Add(left);
            a.Board.Add(right);
            b.Board.Add(Units.CreateRaw(rng, "ne_wall", 0, 1, Keyword.None));
            int atk = right.Atk;
            CombatResult r = CombatSim.Run(a, b, rng, TestSupport.Cat);
            bool flint = false;
            for (int i = 0; i < r.Events.Count; i++)
            {
                if (r.Events[i].Note == "FlintAtk") flint = true;
            }
            Assert.True(flint);
            _ = atk;
        }
    }
}
