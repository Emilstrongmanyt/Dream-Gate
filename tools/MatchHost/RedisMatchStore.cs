using System;
using Kindling.Sim.Match;
using StackExchange.Redis;

namespace Kindling.Tools.MatchHost
{
    public sealed class RedisMatchStore : IMatchStore
    {
        readonly IDatabase _db;

        public RedisMatchStore(string url)
        {
            var mux = ConnectionMultiplexer.Connect(url);
            _db = mux.GetDatabase();
        }

        public void PutMatch(string matchId, string json)
        {
            _db.StringSet("kindling:match:" + matchId, json ?? "", TimeSpan.FromHours(2));
        }

        public string GetMatch(string matchId)
        {
            RedisValue v = _db.StringGet("kindling:match:" + matchId);
            return v.HasValue ? v.ToString() : null;
        }

        public void PutAccount(string accountId, string json)
        {
            _db.StringSet("kindling:account:" + accountId, json ?? "");
        }

        public string GetAccount(string accountId)
        {
            RedisValue v = _db.StringGet("kindling:account:" + accountId);
            return v.HasValue ? v.ToString() : null;
        }

        public void PutDevice(string deviceHash, string accountId)
        {
            _db.StringSet("kindling:device:" + deviceHash, accountId ?? "");
        }

        public string GetDevice(string deviceHash)
        {
            RedisValue v = _db.StringGet("kindling:device:" + deviceHash);
            return v.HasValue ? v.ToString() : null;
        }
    }
}
