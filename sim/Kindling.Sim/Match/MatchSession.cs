using System;
using System.Text;
using Kindling.Sim.Catalog;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;

namespace Kindling.Sim.Match
{
    /// <summary>
    /// Authoritative in-process match host. Timer expiry auto-starts combat.
    /// WebSockets and Redis wrap this later; the sim does not talk to the network.
    /// </summary>
    public sealed class MatchSession
    {
        public MatchLoop Loop;
        public Catalog.Catalog Cat;
        public DateTime RecruitEndsAtUtc;
        public readonly int[] LastSeq = new int[Rules.LobbySize];
        public readonly string[] ResumeTokens = new string[Rules.LobbySize];
        public int SnapshotVersion;

        public static MatchSession Create(Catalog.Catalog cat, Guid matchId, uint salt, int humanSeats = 1)
        {
            var s = new MatchSession
            {
                Cat = cat,
                Loop = MatchLoop.Create(cat, matchId, salt, humanSeats)
            };
            s.Loop.AutoPickBotCaptains();
            for (int i = 0; i < Rules.LobbySize; i++)
                s.ResumeTokens[i] = matchId.ToString("N") + "-" + i.ToString();
            if (s.Loop.State.Phase == Phase.CaptainPick)
                s.ArmTimer(Rules.CaptainPickSeconds);
            return s;
        }

        public void StartRecruit()
        {
            if (Loop.State.Phase == Phase.CaptainPick)
                Loop.StartFromCaptainPick();
            ArmTimer(Rules.RecruitSeconds(Loop.State.Round));
        }

        public void ArmTimer(int seconds)
        {
            if (seconds < 1) seconds = 1;
            RecruitEndsAtUtc = DateTime.UtcNow.AddSeconds(seconds);
        }

        public int SecondsLeft(DateTime utcNow)
        {
            int s = (int)Math.Ceiling((RecruitEndsAtUtc - utcNow).TotalSeconds);
            return s < 0 ? 0 : s;
        }

        public bool Tick(DateTime utcNow)
        {
            if (Loop.State.MatchOver) return false;
            if (utcNow < RecruitEndsAtUtc) return false;
            if (Loop.State.Phase == Phase.CaptainPick)
            {
                Loop.StartFromCaptainPick();
                ArmTimer(Rules.RecruitSeconds(Loop.State.Round));
                SnapshotVersion++;
                return true;
            }
            if (Loop.State.Phase == Phase.Recruit)
            {
                Loop.ResolveRecruitAndCombat();
                SnapshotVersion++;
                if (!Loop.State.MatchOver)
                {
                    Loop.ContinueToNextRecruit();
                    ArmTimer(Rules.RecruitSeconds(Loop.State.Round));
                }
                return true;
            }
            return false;
        }

        public string Handle(int seat, string json)
        {
            if (seat < 0 || seat >= Rules.LobbySize)
                return Error("BAD_SEAT", 0);
            if (json != null && json.IndexOf("\"op\":\"Ping\"", StringComparison.Ordinal) >= 0)
                return SnapshotFor(seat, 0);
            if (json != null && json.IndexOf("\"op\":\"Join\"", StringComparison.Ordinal) >= 0)
                return Welcome(seat);
            RecruitAction a = Protocol.Parse(json, seat);
            if (a == null) return Error("BAD_ACTION", 0);
            if (a.Seq > 0 && a.Seq <= LastSeq[seat])
                return Error("DUP", a.Seq);
            SimResult r = Loop.Try(a);
            if (!r.Ok) return Error(r.Code ?? "FAIL", a.Seq);
            if (a.Seq > LastSeq[seat]) LastSeq[seat] = a.Seq;
            SnapshotVersion++;
            if (a.Op == RecruitOp.CaptainPick && Loop.State.Phase == Phase.CaptainPick)
            {
                bool all = true;
                for (int i = 0; i < Loop.State.Seats.Length; i++)
                    if (Loop.State.Seats[i].Alive && Loop.State.Seats[i].Captain.IsEmpty) all = false;
                if (all)
                {
                    Loop.StartFromCaptainPick();
                    ArmTimer(Rules.RecruitSeconds(Loop.State.Round));
                }
            }
            return SnapshotFor(seat, a.Seq);
        }

        public string Welcome(int seat)
        {
            var sb = new StringBuilder();
            sb.Append("{\"op\":\"Welcome\",\"protocolVersion\":").Append(Protocol.Version);
            sb.Append(",\"seat\":").Append(seat);
            sb.Append(",\"catalogVersion\":\"").Append(Escape(Cat.ContentVersion)).Append('"');
            sb.Append(",\"deviceResumeToken\":\"").Append(Escape(ResumeTokens[seat])).Append('"');
            sb.Append(",\"displayNames\":[");
            for (int i = 0; i < Loop.State.Seats.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(Escape(Loop.State.Seats[i].DisplayName)).Append('"');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public string SnapshotFor(int seat, int seqAck)
        {
            PlayerState you = Loop.State.Seats[seat];
            var sb = new StringBuilder();
            sb.Append("{\"op\":\"Snapshot\",\"seqAck\":").Append(seqAck);
            sb.Append(",\"version\":").Append(SnapshotVersion);
            sb.Append(",\"phase\":\"").Append(Loop.State.Phase).Append('"');
            sb.Append(",\"round\":").Append(Loop.State.Round);
            sb.Append(",\"timer\":").Append(SecondsLeft(DateTime.UtcNow));
            sb.Append(",\"you\":{");
            WriteYou(sb, you);
            sb.Append("},\"public\":[");
            for (int i = 0; i < Loop.State.Seats.Length; i++)
            {
                if (i > 0) sb.Append(',');
                PlayerState p = Loop.State.Seats[i];
                sb.Append("{\"seat\":").Append(i);
                sb.Append(",\"displayName\":\"").Append(Escape(p.DisplayName)).Append('"');
                sb.Append(",\"wick\":").Append(p.Wick);
                sb.Append(",\"depth\":").Append(p.Depth);
                sb.Append(",\"alive\":").Append(p.Alive ? "true" : "false");
                sb.Append(",\"chorusTags\":\"").Append(Escape(ChorusTags(p))).Append('"');
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        void WriteYou(StringBuilder sb, PlayerState p)
        {
            sb.Append("\"wick\":").Append(p.Wick);
            sb.Append(",\"embers\":").Append(p.Embers);
            sb.Append(",\"depth\":").Append(p.Depth);
            sb.Append(",\"upgradeCost\":").Append(p.UpgradeCost);
            sb.Append(",\"hold\":").Append(p.Hold ? "true" : "false");
            sb.Append(",\"ready\":").Append(p.Ready ? "true" : "false");
            sb.Append(",\"board\":"); WriteUnits(sb, p.Board);
            sb.Append(",\"hand\":"); WriteUnits(sb, p.Hand);
            sb.Append(",\"stall\":"); WriteStall(sb, p);
        }

        void WriteUnits(StringBuilder sb, System.Collections.Generic.List<UnitInstance> list)
        {
            sb.Append('[');
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                WriteUnit(sb, list[i], i);
            }
            sb.Append(']');
        }

        void WriteStall(StringBuilder sb, PlayerState p)
        {
            sb.Append('[');
            bool first = true;
            for (int i = 0; i < p.Stall.Count; i++)
            {
                if (p.Stall[i] == null) continue;
                if (!first) sb.Append(',');
                first = false;
                WriteUnit(sb, p.Stall[i], i);
            }
            sb.Append(']');
        }

        void WriteUnit(StringBuilder sb, UnitInstance u, int slot)
        {
            sb.Append("{\"slot\":").Append(slot);
            sb.Append(",\"instanceId\":\"").Append(u.InstanceId).Append('"');
            sb.Append(",\"catalogId\":\"").Append(Escape(u.CatalogId.Value)).Append('"');
            sb.Append(",\"atk\":").Append(u.EffectiveAtk);
            sb.Append(",\"hp\":").Append(u.Hp);
            sb.Append(",\"awakened\":").Append(u.Awakened ? "true" : "false");
            sb.Append('}');
        }

        string ChorusTags(PlayerState p)
        {
            var seen = new System.Collections.Generic.List<string>();
            void add(UnitInstance u)
            {
                UnitDef d = Cat.GetUnit(u.CatalogId);
                if (d == null || d.Chorus == Chorus.Neutral) return;
                string n = d.Chorus.ToString().ToLowerInvariant();
                for (int i = 0; i < seen.Count; i++)
                    if (seen[i] == n) return;
                seen.Add(n);
            }
            for (int i = 0; i < p.Board.Count; i++) add(p.Board[i]);
            var sb = new StringBuilder();
            for (int i = 0; i < seen.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(seen[i]);
            }
            return sb.ToString();
        }

        static string Error(string code, int seq)
        {
            return "{\"op\":\"Error\",\"code\":\"" + Escape(code) + "\",\"seq\":" + seq + "}";
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
