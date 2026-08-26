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
