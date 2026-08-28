using System;
using Kindling.Sim.Match;
using Npgsql;

namespace Kindling.Tools.MatchHost
{
    public sealed class PostgresMatchStore : IMatchStore
    {
        readonly string _cs;

        public PostgresMatchStore(string url)
        {
            _cs = Normalize(url);
            using var conn = new NpgsqlConnection(_cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "CREATE TABLE IF NOT EXISTS accounts (id text PRIMARY KEY, json text NOT NULL);"
                + "CREATE TABLE IF NOT EXISTS devices (hash text PRIMARY KEY, account_id text NOT NULL);"
                + "CREATE TABLE IF NOT EXISTS matches (id text PRIMARY KEY, json text NOT NULL, updated timestamptz DEFAULT now());"
                + "CREATE TABLE IF NOT EXISTS history (id bigserial PRIMARY KEY, account_id text NOT NULL, json text NOT NULL, at timestamptz DEFAULT now());"
                + "CREATE TABLE IF NOT EXISTS logins (login text PRIMARY KEY, account_id text NOT NULL);";
            cmd.ExecuteNonQuery();
        }

        public void PutMatch(string matchId, string json)
        {
            Exec("INSERT INTO matches(id,json) VALUES(@id,@json) ON CONFLICT (id) DO UPDATE SET json=EXCLUDED.json, updated=now()",
                matchId, json);
        }

        public string GetMatch(string matchId) => Scalar("SELECT json FROM matches WHERE id=@id", matchId);

        public void PutAccount(string accountId, string json)
        {
            Exec("INSERT INTO accounts(id,json) VALUES(@id,@json) ON CONFLICT (id) DO UPDATE SET json=EXCLUDED.json",
                accountId, json);
        }

        public string GetAccount(string accountId) => Scalar("SELECT json FROM accounts WHERE id=@id", accountId);

        public void PutDevice(string deviceHash, string accountId)
        {
            Exec("INSERT INTO devices(hash,account_id) VALUES(@id,@json) ON CONFLICT (hash) DO UPDATE SET account_id=EXCLUDED.account_id",
                deviceHash, accountId);
        }

        public string GetDevice(string deviceHash) => Scalar("SELECT account_id FROM devices WHERE hash=@id", deviceHash);

        public void AppendHistory(string accountId, string json)
        {
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(json)) return;
            using var conn = new NpgsqlConnection(_cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO history(account_id,json) VALUES(@a,@j)";
            cmd.Parameters.AddWithValue("a", accountId);
            cmd.Parameters.AddWithValue("j", json);
            cmd.ExecuteNonQuery();
        }

        public string ListHistory(string accountId)
        {
            if (string.IsNullOrEmpty(accountId)) return "[]";
            using var conn = new NpgsqlConnection(_cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT json FROM history WHERE account_id=@a ORDER BY id DESC LIMIT 50";
            cmd.Parameters.AddWithValue("a", accountId);
            using var r = cmd.ExecuteReader();
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            while (r.Read())
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(r.GetString(0));
            }
            sb.Append(']');
            return sb.ToString();
        }

        public void PutLogin(string login, string accountId)
        {
            Exec("INSERT INTO logins(login,account_id) VALUES(@id,@json) ON CONFLICT (login) DO UPDATE SET account_id=EXCLUDED.account_id",
                login, accountId);
        }

        public string GetLogin(string login) => Scalar("SELECT account_id FROM logins WHERE login=@id", login);

        void Exec(string sql, string id, string json)
        {
            if (string.IsNullOrEmpty(id)) return;
            using var conn = new NpgsqlConnection(_cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("json", json ?? "");
            cmd.ExecuteNonQuery();
        }

        string Scalar(string sql, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            using var conn = new NpgsqlConnection(_cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("id", id);
            object v = cmd.ExecuteScalar();
            return v == null || v is DBNull ? null : v.ToString();
        }

        static string Normalize(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (!url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
                return url;
            var uri = new Uri(url);
            string user = "", pass = "";
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                int c = uri.UserInfo.IndexOf(':');
                if (c >= 0)
                {
                    user = Uri.UnescapeDataString(uri.UserInfo.Substring(0, c));
                    pass = Uri.UnescapeDataString(uri.UserInfo.Substring(c + 1));
                }
                else user = Uri.UnescapeDataString(uri.UserInfo);
            }
            string db = uri.AbsolutePath.Trim('/');
            int port = uri.Port > 0 ? uri.Port : 5432;
            return "Host=" + uri.Host + ";Port=" + port + ";Database=" + db
                + ";Username=" + user + ";Password=" + pass + ";SSL Mode=Prefer";
        }
    }

    public sealed class CompositeMatchStore : IMatchStore
    {
        readonly IMatchStore _matches;
        readonly IMatchStore _accounts;

        public CompositeMatchStore(IMatchStore matches, IMatchStore accounts)
        {
            _matches = matches ?? accounts;
            _accounts = accounts ?? matches;
        }

        public void PutMatch(string matchId, string json) => _matches.PutMatch(matchId, json);
        public string GetMatch(string matchId) => _matches.GetMatch(matchId);
        public void PutAccount(string accountId, string json) => _accounts.PutAccount(accountId, json);
        public string GetAccount(string accountId) => _accounts.GetAccount(accountId);
        public void PutDevice(string deviceHash, string accountId) => _accounts.PutDevice(deviceHash, accountId);
        public string GetDevice(string deviceHash) => _accounts.GetDevice(deviceHash);
        public void AppendHistory(string accountId, string json) => _accounts.AppendHistory(accountId, json);
        public string ListHistory(string accountId) => _accounts.ListHistory(accountId);
        public void PutLogin(string login, string accountId) => _accounts.PutLogin(login, accountId);
        public string GetLogin(string login) => _accounts.GetLogin(login);
    }
}
