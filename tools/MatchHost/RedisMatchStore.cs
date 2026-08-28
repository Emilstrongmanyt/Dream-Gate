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

        public void AppendHistory(string accountId, string json)
        {
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(json)) return;
            string key = "kindling:history:" + accountId;
            _db.ListLeftPush(key, json);
            _db.ListTrim(key, 0, 49);
        }

        public string ListHistory(string accountId)
        {
            if (string.IsNullOrEmpty(accountId)) return "[]";
            RedisValue[] rows = _db.ListRange("kindling:history:" + accountId, 0, 49);
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            for (int i = 0; i < rows.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(rows[i].ToString());
            }
            sb.Append(']');
            return sb.ToString();
        }

        public void PutLogin(string login, string accountId)
        {
            if (string.IsNullOrEmpty(login)) return;
            _db.StringSet("kindling:login:" + login, accountId ?? "");
        }

        public string GetLogin(string login)
        {
            if (string.IsNullOrEmpty(login)) return null;
            RedisValue v = _db.StringGet("kindling:login:" + login);
            return v.HasValue ? v.ToString() : null;
        }
    }
}
