using System;
using System.Collections.Generic;
using Kindling.Sim;
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
    public class SliceBTests
    {
        [Fact]
        public void Mutual_kill_both_echo()
        {
            var rng = new MatchRng(31UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            a.Board.Add(EchoSummoner(rng, 2, 2));
            b.Board.Add(EchoSummoner(rng, 2, 2));
            CombatResult r = CombatSim.Run(a, b, rng, TestSupport.Cat);
            int echoes = CountOp(r, CombatOp.Echo);
            int summons = CountOp(r, CombatOp.Summon);
            Assert.Equal(2, echoes);
            Assert.True(summons >= 2);
        }

        [Fact]
        public void Afterglow_and_echo_both_resolve()
        {
            var rng = new MatchRng(32UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            a.Board.Add(Units.Create(TestSupport.Cat, rng, new UnitId("ab_choir")));
            b.Board.Add(Units.CreateRaw(rng, "ne_wall", 10, 1, Keyword.None));
            CombatResult r = CombatSim.Run(a, b, rng, TestSupport.Cat);
            Assert.True(CountOp(r, CombatOp.Echo) >= 1);
            Assert.True(CountOp(r, CombatOp.Afterglow) >= 1);
            Assert.True(CountOp(r, CombatOp.Summon) >= 1);
        }

        [Fact]
        public void Night_summonfill_caps_at_seven()
        {
            var rng = new MatchRng(33UL);
            var a = TestSupport.Player(0);
            var b = TestSupport.Player(1);
            a.Board.Add(Units.Create(TestSupport.Cat, rng, new UnitId("ab_night")));
            for (int i = 0; i < 6; i++)
                a.Board.Add(Units.CreateRaw(rng, "ne_wall", 0, 12, Keyword.None));
            Assert.Equal(7, a.Board.Count);
            b.Board.Add(Units.CreateRaw(rng, "ne_wall", 20, 20, Keyword.None));
            var match = new MatchState { Rng = rng };
            CombatResult r = CombatSim.Run(match, a, b, rng, TestSupport.Cat);
            Assert.True(a.Board.Count <= Rules.BoardMax);
            Assert.Equal(0, match.DrainReentryAttempts);
            int motes = 0;
            for (int i = 0; i < a.Board.Count; i++)
                if (a.Board[i].CatalogId.Value == "tok_ash_mote") motes++;
            Assert.True(motes <= 1);
            _ = r;
        }

        [Fact]
        public void Throne_spark_kindle_strips_aura_before_first_attack()
        {
            bool hit = false;
            for (ulong seed = 1; seed <= 80 && !hit; seed++)
            {
                var rng = new MatchRng(seed);
                var a = TestSupport.Player(0);
                var b = TestSupport.Player(1);
                var pal = Units.CreateRaw(rng, "ne_porter", 2, 2, Keyword.None);
                var throne = Units.CreateRaw(rng, "gw_throne", 7, 1, Keyword.Ward);
                a.Board.Add(pal);
                a.Board.Add(throne);
                b.Board.Add(Units.Create(TestSupport.Cat, rng, new UnitId("gw_spark")));
                b.Board.Add(Units.CreateRaw(rng, "ne_wall", 0, 8, Keyword.None));
                b.Board.Add(Units.CreateRaw(rng, "ne_wall", 0, 8, Keyword.None));
                CombatResult r = CombatSim.Run(a, b, rng, TestSupport.Cat);
                int throneDeath = -1;
                int firstAtk = -1;
                for (int i = 0; i < r.Events.Count; i++)
                {
                    CombatEvent e = r.Events[i];
                    if (e.Op == CombatOp.Death && e.SrcInstance == throne.InstanceId && throneDeath < 0)
                        throneDeath = i;
                    if (e.Op == CombatOp.Attack && firstAtk < 0)
                        firstAtk = i;
                }
                if (throneDeath < 0) continue;
                hit = true;
                if (firstAtk >= 0)
                    Assert.True(throneDeath < firstAtk);
                for (int i = 0; i < r.Events.Count; i++)
                {
                    CombatEvent e = r.Events[i];
                    if (e.Op == CombatOp.Attack && e.SrcInstance == pal.InstanceId)
                        Assert.Equal(2, e.Atk);
                }
            }
            Assert.True(hit);
        }

        [Fact]
        public void Investor_start_of_recruit_grants_one_ember()
        {
            var rng = new MatchRng(34UL);
            var m = new MatchState { Rng = rng, Round = 1 };
            Pool.Init(m, TestSupport.Cat);
            PlayerState p = m.Seats[0];
            p.Depth = 4;
            p.Embers = 0;
            p.Board.Add(Units.Create(TestSupport.Cat, rng, new UnitId("ck_investor")));
            Grant.RecruitStart(m, p, TestSupport.Cat);
            Assert.Equal(4, p.Embers);
        }

        [Fact]
        public void Investor_below_depth_4_does_not_grant()
        {
            var rng = new MatchRng(35UL);
            var m = new MatchState { Rng = rng, Round = 1 };
            Pool.Init(m, TestSupport.Cat);
            PlayerState p = m.Seats[0];
            p.Depth = 3;
            p.Embers = 0;
            p.Board.Add(Units.Create(TestSupport.Cat, rng, new UnitId("ck_investor")));
            Grant.RecruitStart(m, p, TestSupport.Cat);
            Assert.Equal(3, p.Embers);
        }

        [Fact]
        public void Checkpoint_buy_reroll_next_stall_matches_control()
        {
            var s = MatchSession.Create(TestSupport.Cat, Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 41u, 1);
            s.StartRecruit();
            PlayerState p = s.Loop.Human;
            p.Embers = 20;
            if (p.Stall.Count == 0 || p.Stall[0] == null)
                p.Stall.Insert(0, Units.Create(TestSupport.Cat, s.Loop.State.Rng, new UnitId("ck_urchin")));
            else
                p.Stall[0] = Units.Create(TestSupport.Cat, s.Loop.State.Rng, new UnitId("ck_urchin"));
            Assert.True(s.Loop.Try(new RecruitAction
            {
                Op = RecruitOp.Buy,
                Seat = p.Seat,
                StallIndex = 0,
                Dest = DestLoc.Hand,
                DestIndex = 0
            }).Ok);
            Assert.True(s.Loop.Try(new RecruitAction { Op = RecruitOp.Reroll, Seat = p.Seat }).Ok);
            string blob = CheckpointMatch.Save(s);
            MatchSession restored = CheckpointMatch.Load(TestSupport.Cat, blob);
            Assert.True(s.Loop.Try(new RecruitAction { Op = RecruitOp.Reroll, Seat = p.Seat }).Ok);
            Assert.True(restored.Loop.Try(new RecruitAction { Op = RecruitOp.Reroll, Seat = restored.Loop.Human.Seat }).Ok);
            Assert.Equal(StallIds(s.Loop.Human), StallIds(restored.Loop.Human));
        }

        [Fact]
        public void Pool_identity_holds_after_awaken_and_sell()
        {
            var cat = TestSupport.Cat;
            var rng = new MatchRng(19UL);
            var m = new MatchState { Rng = rng };
            Pool.Init(m, cat);
            int start = Pool.StartingCopies(cat);
            Assert.Equal(start, Pool.AccountedCopies(m, cat));
            PlayerState p = m.Seats[0];
            p.Depth = 4;
            for (int i = 0; i < 3; i++)
            {
                Assert.True(Pool.TryConsume(m, new UnitId("ne_smelter")));
                UnitInstance u = Units.Create(cat, rng, new UnitId("ne_smelter"));
                Units.GiveCinder(u, 2);
                p.Board.Add(u);
            }
            Assert.Equal(start, Pool.AccountedCopies(m, cat));
            Assert.True(Awaken.TryAwaken(m, p, cat));
            Assert.True(p.Board.Count == 1 && p.Board[0].Awakened);
            Assert.Equal(start, Pool.AccountedCopies(m, cat));
            Pool.Return(m, p.Board[0].CatalogId, 1);
            p.Board.Clear();
            Assert.Equal(start, Pool.AccountedCopies(m, cat));
        }

        [Fact]
        public void Combat_fuzz_100_seeds_no_throw_or_drain_reentry()
        {
            var cat = TestSupport.Cat;
            var units = cat.Units;
            Assert.True(units.Count > 8);
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var rng = new MatchRng(seed);
                var a = TestSupport.Player(0);
                var b = TestSupport.Player(1);
                int na = 1 + rng.Range(MatchRng.Stream.Combat, 0, 7);
                int nb = 1 + rng.Range(MatchRng.Stream.Combat, 0, 7);
                FillRandom(a, rng, units, na);
                FillRandom(b, rng, units, nb);
                var match = new MatchState { Rng = rng };
                CombatResult r = CombatSim.Run(match, a, b, rng, cat);
                Assert.NotNull(r);
                Assert.Equal(0, match.DrainReentryAttempts);
                Assert.True(a.Board.Count <= Rules.BoardMax);
                Assert.True(b.Board.Count <= Rules.BoardMax);
            }
        }

        [Fact]
        public void Catalog_yaml_unique_ids_and_known_keys()
        {
            List<string> errors = CatalogValidate.ValidateDirectory(TestSupport.FindContent());
            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }

        [Fact]
        public void Fifty_headless_matches_assign_places()
        {
            for (ulong seed = 1; seed <= 50; seed++)
            {
                MatchLoop loop = MatchLoop.CreateHeadless(TestSupport.Cat, seed);
                loop.RunToEnd();
                Assert.True(loop.State.MatchOver);
                var seen = new HashSet<int>();
                for (int i = 0; i < loop.State.Seats.Length; i++)
                {
                    Assert.True(loop.State.Seats[i].Place.HasValue);
                    Assert.True(seen.Add(loop.State.Seats[i].Place.Value));
                }
                Assert.Equal(8, seen.Count);
            }
        }

        static UnitInstance EchoSummoner(MatchRng rng, int atk, int hp)
        {
            var u = Units.CreateRaw(rng, "ck_urchin", atk, hp, Keyword.None);
            u.ExtraEffects.Add(new EffectDef
            {
                Trigger = Trigger.Echo,
                Persist = Persist.CombatCopy,
                Actions = new List<ActionDef>
                {
                    new ActionDef { Type = ActionType.Summon, Unit = "tok_ash_mote", Count = 1 }
                }
            });
            return u;
        }

        static int CountOp(CombatResult r, CombatOp op)
        {
            int n = 0;
            for (int i = 0; i < r.Events.Count; i++)
                if (r.Events[i].Op == op) n++;
            return n;
        }

        static string StallIds(PlayerState p)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < p.Stall.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(p.Stall[i] != null ? p.Stall[i].CatalogId.Value : "-");
            }
            return sb.ToString();
        }

        static void FillRandom(PlayerState p, MatchRng rng, List<UnitDef> units, int n)
        {
            for (int i = 0; i < n; i++)
            {
                int idx = rng.Range(MatchRng.Stream.Combat, 0, units.Count);
                p.Board.Add(Units.Create(TestSupport.Cat, rng, units[idx].Id));
            }
        }
    }
}
