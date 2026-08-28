using Kindling.Sim;
using Kindling.Sim.Match;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class SnapshotReconnectTests
    {
        [Fact]
        public void Snapshot_includes_captain_glimpse_and_keywords()
        {
            var s = MatchSession.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 9u, 1);
            s.Handle(0, "{\"op\":\"CaptainPick\",\"seq\":1,\"offerIndex\":0}");
            if (s.Loop.State.Phase == Phase.CaptainPick)
                s.StartRecruit();
            PlayerState p = s.Loop.Human;
            p.SetFlag(PlayerFlags.GlimpseOpen);
            p.GlimpseQueue.Enqueue(new GlimpseOffer
            {
                Depth = 1,
                Choices = new[] { new UnitId("ck_urchin"), new UnitId("ne_porter") }
            });
            string snap = s.SnapshotFor(0, 1);
            Assert.Contains("\"captain\":", snap);
            Assert.Contains("\"glimpse\":", snap);
            Assert.Contains("ck_urchin", snap);
            Assert.Contains("\"combatSeq\":", snap);
            Assert.Contains("\"pairings\":", snap);
        }

        [Fact]
        public void Snapshot_apply_restores_glimpse_and_captain()
        {
            var m = new MatchState();
            string json = "{\"op\":\"Snapshot\",\"phase\":\"Recruit\",\"round\":2,\"you\":{\"wick\":22,\"embers\":7,\"depth\":3,\"upgradeCost\":6,\"hold\":false,\"ready\":false,\"flags\":16,\"captain\":\"cap_candle\",\"captainOffers\":[\"cap_candle\",\"cap_debt\"],\"glimpse\":{\"open\":true,\"choices\":[\"ck_urchin\",\"ne_porter\"]},\"board\":[],\"hand\":[],\"stall\":[]},\"public\":[{\"seat\":0,\"displayName\":\"Ada\",\"wick\":22,\"depth\":3,\"alive\":true,\"place\":0}]}";
            SnapshotApply.Apply(m, 0, TestSupport.Cat, json);
            Assert.Equal("cap_candle", m.Seats[0].Captain.Value);
            Assert.True(m.Seats[0].HasFlag(PlayerFlags.GlimpseOpen));
            Assert.Single(m.Seats[0].GlimpseQueue);
            Assert.Equal("ck_urchin", m.Seats[0].GlimpseQueue.Peek().Choices[0].Value);
            Assert.Equal(2, m.Seats[0].CaptainOffers.Length);
        }

        [Fact]
        public void Combat_snapshot_roundtrip()
        {
            var cr = new CombatResult
            {
                Damage = 3,
                Draw = false,
                WinnerSeat = 0,
                SeatA = 0,
                SeatB = 1,
                NameA = "You",
                NameB = "Bot",
                DepthA = 2,
                DepthB = 1,
                WickA = 27,
                WickB = 30,
                RemainingA = 1,
                RemainingB = 0
            };
            cr.BoardA.Add(new CombatPiece { InstanceId = 7, CatalogId = new UnitId("ne_porter"), Atk = 2, Hp = 2, Seat = 0 });
            cr.Events.Add(new CombatEvent { Op = CombatOp.Attack, SrcSeat = 0, DstSeat = 1, Amount = 2, Atk = 2 });
            var sb = new System.Text.StringBuilder();
            CombatSnapshot.Write(sb, cr);
            CombatResult back = CombatSnapshot.Read(sb.ToString());
            Assert.Equal(3, back.Damage);
            Assert.Equal(0, back.WinnerSeat);
            Assert.Single(back.BoardA);
            Assert.Equal("ne_porter", back.BoardA[0].CatalogId.Value);
            Assert.Single(back.Events);
            Assert.Equal(CombatOp.Attack, back.Events[0].Op);
        }

        [Fact]
        public void Ready_commits_recruit_when_aged()
        {
            var s = MatchSession.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 11u, 1);
            s.Handle(0, "{\"op\":\"CaptainPick\",\"seq\":1,\"offerIndex\":0}");
            if (s.Loop.State.Phase == Phase.CaptainPick)
                s.StartRecruit();
            int round = s.Loop.State.Round;
            s.RecruitEndsAtUtc = System.DateTime.UtcNow.AddSeconds(Rules.RecruitSeconds(round) - 2);
            string snap = s.Handle(0, "{\"op\":\"Ready\",\"seq\":2}");
            Assert.DoesNotContain("\"op\":\"Error\"", snap);
            Assert.True(s.Loop.State.Round > round || s.Loop.State.MatchOver);
            Assert.True(s.CombatSeq >= 1);
            Assert.Contains("\"combatSeq\":", snap);
        }

        [Fact]
        public void Resume_token_reloads_from_store()
        {
            var store = new MemoryMatchStore();
            var q = new CasualQueue { Store = store, CatForRestore = TestSupport.Cat };
            MatchSession s = q.Enqueue(TestSupport.Cat, "Ada", 3u, "acc-1");
            string token = s.ResumeTokens[0];
            string id = s.Loop.State.MatchId.ToString("D");
            q.Drop(id);
            MatchSession loaded = q.GetByToken(token);
            Assert.NotNull(loaded);
            Assert.Equal(id, loaded.Loop.State.MatchId.ToString("D"));
        }

        [Fact]
        public void History_and_ratings_written_on_finish()
        {
            var store = new MemoryMatchStore();
            var q = new CasualQueue { Store = store, CatForRestore = TestSupport.Cat };
            MatchSession s = q.Enqueue(TestSupport.Cat, "Ada", 13u, "acc-h");
            store.PutAccount("acc-h", "{\"id\":\"acc-h\",\"displayName\":\"Ada\",\"mmr\":1500,\"rd\":350}");
            for (int i = 1; i < s.Loop.State.Seats.Length; i++)
                s.Loop.State.Seats[i].Wick = 0;
            s.Handle(0, "{\"op\":\"Abandon\"}");
            q.Persist(s);
            string hist = store.ListHistory("acc-h");
            Assert.Contains("matchId", hist);
            string acc = store.GetAccount("acc-h");
            Assert.Contains("lastPlace", acc);
        }

        [Fact]
        public void Live_config_json_has_catalog_and_costs()
        {
            string json = LiveConfig.Json(TestSupport.Cat);
            Assert.Contains("\"protocolVersion\":1", json);
            Assert.Contains("\"buyCost\":3", json);
            Assert.Contains("\"rankedEnabled\":false", json);
            Assert.Contains("catalogVersion", json);
        }
    }
}
