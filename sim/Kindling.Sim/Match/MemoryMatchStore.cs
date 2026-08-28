using System;
using System.Collections.Generic;
using System.IO;

namespace Kindling.Sim.Match
{
    public sealed class MemoryMatchStore : IMatchStore
    {
        readonly object _gate = new object();
        readonly Dictionary<string, string> _matches = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _accounts = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _devices = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _logins = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, System.Collections.Generic.List<string>> _history =
            new Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.Ordinal);

        public void PutMatch(string matchId, string json)
        {
            if (string.IsNullOrEmpty(matchId)) return;
            lock (_gate) _matches[matchId] = json ?? "";
        }

        public string GetMatch(string matchId)
        {
            if (string.IsNullOrEmpty(matchId)) return null;
            lock (_gate)
            {
                _matches.TryGetValue(matchId, out string v);
                return v;
            }
        }

        public void PutAccount(string accountId, string json)
        {
            if (string.IsNullOrEmpty(accountId)) return;
            lock (_gate) _accounts[accountId] = json ?? "";
        }

        public string GetAccount(string accountId)
        {
            if (string.IsNullOrEmpty(accountId)) return null;
            lock (_gate)
            {
                _accounts.TryGetValue(accountId, out string v);
                return v;
            }
        }

        public void PutDevice(string deviceHash, string accountId)
        {
            if (string.IsNullOrEmpty(deviceHash)) return;
            lock (_gate) _devices[deviceHash] = accountId ?? "";
        }

        public string GetDevice(string deviceHash)
        {
            if (string.IsNullOrEmpty(deviceHash)) return null;
            lock (_gate)
            {
                _devices.TryGetValue(deviceHash, out string v);
                return v;
            }
        }

        public void AppendHistory(string accountId, string json)
        {
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(json)) return;
            lock (_gate)
            {
                if (!_history.TryGetValue(accountId, out System.Collections.Generic.List<string> list))
                {
                    list = new System.Collections.Generic.List<string>();
                    _history[accountId] = list;
                }
                list.Insert(0, json);
                if (list.Count > 50) list.RemoveRange(50, list.Count - 50);
            }
        }

        public string ListHistory(string accountId)
        {
            if (string.IsNullOrEmpty(accountId)) return "[]";
            lock (_gate)
            {
                if (!_history.TryGetValue(accountId, out System.Collections.Generic.List<string> list) || list.Count == 0)
                    return "[]";
                var sb = new System.Text.StringBuilder();
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(list[i]);
                }
                sb.Append(']');
                return sb.ToString();
            }
        }

        public void PutLogin(string login, string accountId)
        {
            if (string.IsNullOrEmpty(login)) return;
            lock (_gate) _logins[login] = accountId ?? "";
        }

        public string GetLogin(string login)
        {
            if (string.IsNullOrEmpty(login)) return null;
            lock (_gate)
            {
                _logins.TryGetValue(login, out string v);
                return v;
            }
        }
    }

    public sealed class FileMatchStore : IMatchStore
    {
        readonly string _root;
        readonly MemoryMatchStore _mem = new MemoryMatchStore();

        public FileMatchStore(string root)
        {
            _root = root ?? "matches";
            Directory.CreateDirectory(Path.Combine(_root, "matches"));
            Directory.CreateDirectory(Path.Combine(_root, "accounts"));
            Directory.CreateDirectory(Path.Combine(_root, "devices"));
            Directory.CreateDirectory(Path.Combine(_root, "history"));
            Directory.CreateDirectory(Path.Combine(_root, "logins"));
        }

        public void PutMatch(string matchId, string json)
        {
            _mem.PutMatch(matchId, json);
            File.WriteAllText(Path.Combine(_root, "matches", Safe(matchId) + ".json"), json ?? "");
        }

        public string GetMatch(string matchId)
        {
            string v = _mem.GetMatch(matchId);
            if (v != null) return v;
            string path = Path.Combine(_root, "matches", Safe(matchId) + ".json");
            if (!File.Exists(path)) return null;
            v = File.ReadAllText(path);
            _mem.PutMatch(matchId, v);
            return v;
        }

        public void PutAccount(string accountId, string json)
        {
            _mem.PutAccount(accountId, json);
            File.WriteAllText(Path.Combine(_root, "accounts", Safe(accountId) + ".json"), json ?? "");
        }

        public string GetAccount(string accountId)
        {
            string v = _mem.GetAccount(accountId);
            if (v != null) return v;
            string path = Path.Combine(_root, "accounts", Safe(accountId) + ".json");
            if (!File.Exists(path)) return null;
            v = File.ReadAllText(path);
            _mem.PutAccount(accountId, v);
            return v;
        }

        public void PutDevice(string deviceHash, string accountId)
        {
            _mem.PutDevice(deviceHash, accountId);
            File.WriteAllText(Path.Combine(_root, "devices", Safe(deviceHash) + ".txt"), accountId ?? "");
        }

        public string GetDevice(string deviceHash)
        {
            string v = _mem.GetDevice(deviceHash);
            if (v != null) return v;
            string path = Path.Combine(_root, "devices", Safe(deviceHash) + ".txt");
            if (!File.Exists(path)) return null;
            v = File.ReadAllText(path).Trim();
            _mem.PutDevice(deviceHash, v);
            return v;
        }

        public void AppendHistory(string accountId, string json)
        {
            _mem.AppendHistory(accountId, json);
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(json)) return;
            string path = Path.Combine(_root, "history", Safe(accountId) + ".jsonl");
            File.AppendAllText(path, json.Trim() + "\n");
        }

        public string ListHistory(string accountId)
        {
            string v = _mem.ListHistory(accountId);
            if (v != "[]") return v;
            string path = Path.Combine(_root, "history", Safe(accountId) + ".jsonl");
            if (!File.Exists(path)) return "[]";
            string[] lines = File.ReadAllLines(path);
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            int n = 0;
            for (int i = lines.Length - 1; i >= 0 && n < 50; i--)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                if (n > 0) sb.Append(',');
                sb.Append(lines[i]);
                n++;
            }
            sb.Append(']');
            return sb.ToString();
        }

        public void PutLogin(string login, string accountId)
        {
            _mem.PutLogin(login, accountId);
            if (string.IsNullOrEmpty(login)) return;
            File.WriteAllText(Path.Combine(_root, "logins", Safe(login) + ".txt"), accountId ?? "");
        }

        public string GetLogin(string login)
        {
            string v = _mem.GetLogin(login);
            if (v != null) return v;
            string path = Path.Combine(_root, "logins", Safe(login) + ".txt");
            if (!File.Exists(path)) return null;
            v = File.ReadAllText(path).Trim();
            _mem.PutLogin(login, v);
            return v;
        }

        static string Safe(string id)
        {
            if (string.IsNullOrEmpty(id)) return "_";
            char[] c = id.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                char ch = c[i];
                if (!((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '_'))
                    c[i] = '_';
            }
            return new string(c);
        }
    }
}
