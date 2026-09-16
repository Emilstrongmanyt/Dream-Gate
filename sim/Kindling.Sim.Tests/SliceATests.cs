using System.Collections.Generic;
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
    public class SliceATests
    {
        [Fact]
        public void Echoist_adds_one_echo_not_board_count()
        {
            var rng = new MatchRng(41UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            var echoer = Units.CreateRaw(rng, "ck_urchin", 1, 1, Keyword.None);
            echoer.ExtraEffects.Add(new EffectDef
            {
                Trigger = Trigger.Echo,
                Persist = Persist.CombatCopy,
                Actions = new List<ActionDef>
                {
                    new ActionDef { Type = ActionType.Summon, Unit = "tok_ash_mote", Count = 1 }
                }
            });
            a.Board.Add(echoer);
            a.Board.Add(Units.Create(TestSupport.Cat, rng, new UnitId("ne_echoist")));
            b.Board.Add(Units.CreateRaw(rng, "ne_wall", 20, 20, Keyword.None));
            CombatResult r = CombatSim.Run(a, b, rng, TestSupport.Cat);
            int summons = 0;
            for (int i = 0; i < r.Events.Count; i++)
            {
                if (r.Events[i].Op == CombatOp.Summon && r.Events[i].CatalogId == "tok_ash_mote")
                    summons++;
            }
            Assert.Equal(2, summons);
        }

        [Fact]
        public void Widow_edict_adds_one_echo_row()
        {
            MatchLoop loop = MatchLoop.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 9u, 1);
            loop.Human.Captain = new CaptainId("cap_widow");
            Captains.CaptainPassives.OnCaptainPicked(loop.Human, TestSupport.Cat);
            loop.StartFromCaptainPick();
            PlayerState p = loop.Human;
            p.Embers = 10;
            p.Board.Add(Units.Create(TestSupport.Cat, loop.State.Rng, new UnitId("ck_urchin")));
            SimResult r = loop.Try(new RecruitAction { Op = RecruitOp.Edict, Seat = p.Seat, TargetIndex = 0 });
            Assert.True(r.Ok, r.Code);
            int echoRows = 0;
            var extra = p.Board[0].ExtraEffects;
            Assert.NotNull(extra);
            for (int i = 0; i < extra.Count; i++)
                if (extra[i].Trigger == Trigger.Echo) echoRows++;
            Assert.Equal(1, echoRows);
        }

        [Fact]
        public void Ghost_uses_worst_place_in_the_latest_round()
        {
            MatchLoop loop = MatchLoop.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 11u, 0);
            loop.State.Round = 3;
            loop.State.Seats[1].Wick = 0;
            loop.State.Seats[2].Wick = 0;
            loop.State.Seats[1].RingDamageTaken = 4;
            loop.State.Seats[2].RingDamageTaken = 12;
            loop.PlaceNewlyDead();
            Assert.True(loop.State.Seats[2].Place > loop.State.Seats[1].Place);
            Assert.Equal(2, loop.FindGhostSource().Seat);
            loop.State.Round = 4;
            loop.State.Seats[3].Wick = 0;
            loop.PlaceNewlyDead();
            Assert.Equal(3, loop.FindGhostSource().Seat);
        }

        [Fact]
        public void Hold_and_reorder_require_recruit()
        {
            MatchLoop loop = MatchLoop.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 13u, 1);
            loop.StartFromCaptainPick();
            PlayerState p = loop.Human;
            p.Board.Add(Units.Create(TestSupport.Cat, loop.State.Rng, new UnitId("ck_urchin")));
            p.Board.Add(Units.Create(TestSupport.Cat, loop.State.Rng, new UnitId("ne_porter")));
            Assert.True(loop.Try(new RecruitAction { Op = RecruitOp.Hold, Seat = p.Seat, Held = true }).Ok);
            loop.State.Phase = Phase.Combat;
            Assert.Equal("WRONG_PHASE", loop.Try(new RecruitAction { Op = RecruitOp.Hold, Seat = p.Seat, Held = false }).Code);
            Assert.Equal("WRONG_PHASE", loop.Try(new RecruitAction
            {
                Op = RecruitOp.Reorder,
                Seat = p.Seat,
                BoardPerm = new[] { 1, 0 }
            }).Code);
        }

        [Fact]
        public void Casual_finish_does_not_apply_glicko()
        {
            var s = MatchSession.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 21u, 1);
            s.Loop.State.Ranked = false;
            s.Loop.State.MatchOver = true;
            s.Loop.State.Seats[0].Place = 1;
            s.Loop.State.Seats[0].Rating = 1500;
            s.Loop.State.Seats[1].Place = 2;
            s.Loop.State.Seats[1].Rating = 1500;
            s.Finish();
            Assert.Equal(1500, s.Loop.State.Seats[0].Rating);
        }

        [Fact]
        public void Ranked_finish_applies_glicko()
        {
            var s = MatchSession.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 22u, 1);
            s.Loop.State.Ranked = true;
            s.Loop.State.MatchOver = true;
            s.Loop.State.Seats[0].Place = 1;
            s.Loop.State.Seats[0].Rating = 1500;
            s.Loop.State.Seats[0].Rd = 350;
            s.Loop.State.Seats[1].Place = 2;
            s.Loop.State.Seats[1].Rating = 1500;
            s.Loop.State.Seats[1].Rd = 350;
            s.Finish();
            Assert.True(s.Loop.State.Seats[0].Rating > 1500);
        }

        [Fact]
        public void Rules_text_names_echo_and_arrival()
        {
            UnitDef echoist = TestSupport.Cat.GetUnit("ne_echoist");
            Assert.Contains("Aura", RulesText.Face(echoist, null));
            Assert.Contains("Echoes", RulesText.Body(echoist));
            UnitDef whet = TestSupport.Cat.GetUnit("sp_whet");
            Assert.Contains("Arrival", RulesText.Body(whet));
            Assert.DoesNotContain("Text comes later.", RulesText.Body(echoist));
        }
    }
}
