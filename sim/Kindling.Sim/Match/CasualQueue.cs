using System;
using System.Collections.Generic;
using Kindling.Sim.Catalog;
using Kindling.Sim.Model;

namespace Kindling.Sim.Match
{
    /// <summary>
    /// Closed-alpha Casual queue: one human starts immediately, remaining seats are bots.
    /// Ranked (no bots, 8 humans) is a later flag.
    /// </summary>
    public sealed class CasualQueue
    {
        readonly object _gate = new object();
        readonly Dictionary<string, MatchSession> _live = new Dictionary<string, MatchSession>(StringComparer.Ordinal);
        public IMatchStore Store;

        public int LiveCount
        {
            get { lock (_gate) return _live.Count; }
        }

        public MatchSession Enqueue(Catalog.Catalog cat, string displayName, uint salt, string accountId = null)
        {
            if (cat == null) throw new ArgumentNullException(nameof(cat));
            CatForRestore = cat;
            Guid id = Guid.NewGuid();
            MatchSession s = MatchSession.Create(cat, id, salt, humanSeats: 1);
            if (!string.IsNullOrEmpty(displayName))
                s.Loop.State.Seats[0].DisplayName = displayName;
            if (!string.IsNullOrEmpty(accountId))
                s.AccountIds[0] = accountId;
            Telemetry.MatchActive++;
            string key = id.ToString("D");
            lock (_gate)
                _live[key] = s;
            Persist(s);
            return s;
        }

        public MatchSession Get(string matchId)
        {
            if (string.IsNullOrEmpty(matchId)) return null;
            lock (_gate)
            {
                if (_live.TryGetValue(matchId, out MatchSession s))
                    return s;
            }
            if (Store == null || CatForRestore == null) return null;
            string blob = Store.GetMatch(matchId);
            if (string.IsNullOrEmpty(blob)) return null;
            MatchSession loaded = CheckpointMatch.Load(CatForRestore, blob);
            lock (_gate)
                _live[matchId] = loaded;
            return loaded;
        }

        public MatchSession GetByToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            lock (_gate)
            {
                foreach (KeyValuePair<string, MatchSession> kv in _live)
                {
                    for (int i = 0; i < kv.Value.ResumeTokens.Length; i++)
                    {
                        if (kv.Value.ResumeTokens[i] == token)
                            return kv.Value;
                    }
                }
            }
            int dash = token.LastIndexOf('-');
            if (dash == 32)
            {
                string n = token.Substring(0, 32);
                if (Guid.TryParseExact(n, "N", out Guid g))
                    return Get(g.ToString("D"));
            }
            return null;
        }

        public int TickAll(DateTime utcNow, Action<MatchSession> onTicked = null)
        {
            int n = 0;
            MatchSession[] snap;
            lock (_gate)
            {
                snap = new MatchSession[_live.Count];
                _live.Values.CopyTo(snap, 0);
            }
            for (int i = 0; i < snap.Length; i++)
            {
                if (snap[i].Tick(utcNow))
                {
                    n++;
                    Persist(snap[i]);
                    onTicked?.Invoke(snap[i]);
                }
            }
            return n;
        }

        public Catalog.Catalog CatForRestore;

        public void Persist(MatchSession s)
        {
            if (Store == null || s == null) return;
            Store.PutMatch(s.Loop.State.MatchId.ToString("D"), CheckpointMatch.Save(s));
            Telemetry.CheckpointWrites++;
            if (s.Loop.State.MatchOver)
                WriteRatings(s);
        }

        public void Drop(string matchId)
        {
            if (string.IsNullOrEmpty(matchId)) return;
            lock (_gate)
                _live.Remove(matchId);
        }

        void WriteRatings(MatchSession s)
        {
            if (s.RatingsWritten || Store == null) return;
            s.RatingsWritten = true;
            for (int i = 0; i < s.AccountIds.Length; i++)
            {
                string acc = s.AccountIds[i];
                if (string.IsNullOrEmpty(acc)) continue;
                PlayerState p = s.Loop.State.Seats[i];
                string prev = Store.GetAccount(acc) ?? "{}";
                string name = Protocol.ReadString(prev, "displayName");
                if (string.IsNullOrEmpty(name)) name = p.DisplayName ?? "";
                int matches = Protocol.ReadInt(prev, "matches") + 1;
                int mmr = (int)Math.Round(p.Rating);
                int rd = (int)Math.Round(p.Rd);
                int place = p.Place ?? 0;
                string json = AccountAuth.PatchRatings(prev, acc, name, mmr, rd, matches, place);
                Store.PutAccount(acc, json);
                Store.AppendHistory(acc, "{\"matchId\":\"" + s.Loop.State.MatchId.ToString("D")
                    + "\",\"place\":" + place + ",\"mmr\":" + mmr + ",\"rd\":" + rd + "}");
            }
        }
    }
}
