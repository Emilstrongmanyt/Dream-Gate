using System.IO;
using Kindling.Sim.Match;
using Kindling.Sim.Model;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class GlickoAuthStoreTests
    {
        [Fact]
        public void Glicko_first_place_gains_rating()
        {
            var seats = new PlayerState[2];
            seats[0] = new PlayerState { Seat = 0, Place = 1, Rating = 1500, Rd = 350 };
            seats[1] = new PlayerState { Seat = 1, Place = 2, Rating = 1500, Rd = 350 };
            Glicko2.ApplyPlaces(seats);
            Assert.True(seats[0].Rating > 1500);
            Assert.True(seats[1].Rating < 1500);
            Assert.True(seats[0].Rd < 350);
        }

        [Fact]
        public void Device_token_roundtrip()
        {
            string acc = DeviceAuth.NewAccountId();
            string tok = DeviceAuth.IssueToken(acc, "pepper");
            Assert.True(DeviceAuth.Verify(tok, "pepper"));
            Assert.False(DeviceAuth.Verify(tok, "other"));
            Assert.Equal(acc, DeviceAuth.AccountId(tok));
        }

        [Fact]
        public void File_store_match_roundtrip()
        {
            string dir = Path.Combine(Path.GetTempPath(), "kindling-store-" + Path.GetRandomFileName());
            var store = new FileMatchStore(dir);
            store.PutMatch("abc", "{\"ok\":1}");
            Assert.Equal("{\"ok\":1}", store.GetMatch("abc"));
            store.PutAccount("a1", "{\"mmr\":1600}");
            Assert.Contains("1600", store.GetAccount("a1"));
            store.PutDevice("dev", "a1");
            Assert.Equal("a1", store.GetDevice("dev"));
        }

        [Fact]
        public void Snapshot_apply_fills_you()
        {
            var m = new MatchState();
            string json = "{\"op\":\"Snapshot\",\"phase\":\"Recruit\",\"round\":2,\"you\":{\"wick\":22,\"embers\":7,\"depth\":3,\"upgradeCost\":6,\"hold\":false,\"ready\":false,\"board\":[],\"hand\":[{\"slot\":0,\"catalogId\":\"ck_urchin\",\"atk\":2,\"hp\":1}],\"stall\":[]},\"public\":[{\"seat\":0,\"displayName\":\"Ada\",\"wick\":22,\"depth\":3}]}";
            SnapshotApply.Apply(m, 0, TestSupport.Cat, json);
            Assert.Equal(Phase.Recruit, m.Phase);
            Assert.Equal(2, m.Round);
            Assert.Equal(22, m.Seats[0].Wick);
            Assert.Equal(7, m.Seats[0].Embers);
            Assert.Single(m.Seats[0].Hand);
            Assert.Equal("ck_urchin", m.Seats[0].Hand[0].CatalogId.Value);
        }

        [Fact]
        public void Abandon_eliminates_and_rates()
        {
            var s = MatchSession.Create(TestSupport.Cat, System.Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), 21u, 1);
            s.StartRecruit();
            string snap = s.Handle(0, "{\"op\":\"Abandon\"}");
            Assert.Contains("Snapshot", snap);
            Assert.True(s.Loop.State.Seats[0].Wick <= 0);
            Assert.True(s.Loop.State.Seats[0].Place.HasValue);
        }

        [Fact]
        public void Memory_store_used_by_queue_persist()
        {
            var store = new MemoryMatchStore();
            var q = new CasualQueue { Store = store, CatForRestore = TestSupport.Cat };
            MatchSession s = q.Enqueue(TestSupport.Cat, "Ada", 3u);
            string id = s.Loop.State.MatchId.ToString("D");
            Assert.False(string.IsNullOrEmpty(store.GetMatch(id)));
        }
    }
}
