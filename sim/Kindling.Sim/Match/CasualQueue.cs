using System;
using System.Collections.Generic;
using Kindling.Sim.Catalog;

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

        public int LiveCount
        {
            get { lock (_gate) return _live.Count; }
        }

        public MatchSession Enqueue(Catalog.Catalog cat, string displayName, uint salt)
        {
            if (cat == null) throw new ArgumentNullException(nameof(cat));
            Guid id = Guid.NewGuid();
            MatchSession s = MatchSession.Create(cat, id, salt, humanSeats: 1);
            if (!string.IsNullOrEmpty(displayName))
                s.Loop.State.Seats[0].DisplayName = displayName;
            lock (_gate)
                _live[id.ToString("D")] = s;
            return s;
        }

        public MatchSession Get(string matchId)
        {
            if (string.IsNullOrEmpty(matchId)) return null;
            lock (_gate)
            {
                _live.TryGetValue(matchId, out MatchSession s);
                return s;
            }
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
            return null;
        }

        public int TickAll(DateTime utcNow)
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
                if (snap[i].Tick(utcNow)) n++;
            }
            return n;
        }
    }
}
