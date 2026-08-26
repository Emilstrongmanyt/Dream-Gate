using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Kindling.Sim.Catalog;
using Kindling.Sim.Model;
using Kindling.Sim.Rng;

namespace Kindling.Sim.Match
{
    public static class CheckpointMatch
    {
        public static string Save(MatchSession s)
        {
            if (s == null || s.Loop == null) throw new ArgumentNullException(nameof(s));
            MatchState m = s.Loop.State;
            var sb = new StringBuilder(4096);
            sb.Append("{\"v\":1");
            sb.Append(",\"matchId\":\"").Append(m.MatchId.ToString("D")).Append('"');
            sb.Append(",\"salt\":").Append(m.Salt.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"humanSeat\":").Append(s.Loop.HumanSeat);
            sb.Append(",\"round\":").Append(m.Round);
            sb.Append(",\"phase\":\"").Append(m.Phase).Append('"');
            sb.Append(",\"matchOver\":").Append(m.MatchOver ? "true" : "false");
            sb.Append(",\"seq\":").Append(m.Seq);
            sb.Append(",\"snapshotVersion\":").Append(s.SnapshotVersion);
            sb.Append(",\"awakenEvents\":").Append(m.AwakenEvents);
            sb.Append(",\"recruitEndsUnix\":").Append(ToUnix(s.RecruitEndsAtUtc));
            sb.Append(",\"lastSeq\":[");
            for (int i = 0; i < s.LastSeq.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(s.LastSeq[i]);
            }
            sb.Append("],\"rng\":");
            sb.Append(Checkpoint.SerializeRng(m.Rng));
            sb.Append(",\"ghost\":").Append(m.GhostSeat.HasValue ? m.GhostSeat.Value.ToString(CultureInfo.InvariantCulture) : "null");
            sb.Append(",\"pairings\":[");
            if (m.Pairings != null)
            {
                for (int i = 0; i < m.Pairings.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    Pairing p = m.Pairings[i];
                    sb.Append("{\"a\":").Append(p.SeatA).Append(",\"b\":").Append(p.SeatB).Append(",\"g\":").Append(p.Ghost ? "true" : "false").Append('}');
                }
            }
            sb.Append("],\"pool\":[");
            for (int i = 0; i < m.Pool.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":\"").Append(Esc(m.Pool[i].Id.Value)).Append("\",\"n\":").Append(m.Pool[i].Remaining).Append('}');
            }
            sb.Append("],\"seats\":[");
            for (int i = 0; i < m.Seats.Length; i++)
            {
                if (i > 0) sb.Append(',');
                WriteSeat(sb, m.Seats[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static MatchSession Load(Catalog.Catalog cat, string json)
        {
            if (cat == null) throw new ArgumentNullException(nameof(cat));
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("empty checkpoint");
            Guid matchId;
            if (!Guid.TryParse(Protocol.ReadString(json, "matchId"), out matchId))
                matchId = Guid.NewGuid();
            uint salt = (uint)Protocol.ReadInt(json, "salt");
            int human = Protocol.ReadInt(json, "humanSeat");
            MatchSession s = MatchSession.Create(cat, matchId, salt, human >= 0 ? 1 : 0);
            s.Loop.State.Salt = salt;
            s.Loop.State.MatchId = matchId;
            s.Loop.HumanSeat = human;
            s.Loop.State.Round = Protocol.ReadInt(json, "round");
            s.Loop.State.Phase = ParsePhase(Protocol.ReadString(json, "phase"));
            s.Loop.State.MatchOver = Protocol.ReadBool(json, "matchOver");
            s.Loop.State.Seq = Protocol.ReadInt(json, "seq");
            s.SnapshotVersion = Protocol.ReadInt(json, "snapshotVersion");
            s.Loop.State.AwakenEvents = Protocol.ReadInt(json, "awakenEvents");
            long unix = ReadLong(json, "recruitEndsUnix");
            if (unix > 0) s.RecruitEndsAtUtc = FromUnix(unix);
            string rngJson = ExtractObject(json, "rng");
            if (!string.IsNullOrEmpty(rngJson))
                s.Loop.State.Rng = Checkpoint.DeserializeRng(rngJson);

            int[] seq = Protocol.ReadIntArrayPublic(json, "lastSeq");
            if (seq != null)
            {
                int n = seq.Length < s.LastSeq.Length ? seq.Length : s.LastSeq.Length;
                for (int i = 0; i < n; i++) s.LastSeq[i] = seq[i];
            }

            s.Loop.State.Pool.Clear();
            List<string> pool = Protocol.ExtractObjects(json, "pool");
            for (int i = 0; i < pool.Count; i++)
            {
                s.Loop.State.Pool.Add(new PoolEntry
                {
                    Id = new UnitId(Protocol.ReadString(pool[i], "id")),
                    Remaining = Protocol.ReadInt(pool[i], "n")
                });
            }

            List<string> pairs = Protocol.ExtractObjects(json, "pairings");
            s.Loop.State.Pairings = new Pairing[pairs.Count];
            for (int i = 0; i < pairs.Count; i++)
            {
                s.Loop.State.Pairings[i] = new Pairing
                {
                    PairIndex = i,
                    SeatA = Protocol.ReadInt(pairs[i], "a"),
                    SeatB = Protocol.ReadInt(pairs[i], "b"),
                    Ghost = Protocol.ReadBool(pairs[i], "g")
                };
            }
            int ghost = Protocol.ReadInt(json, "ghost");
            string ghostRaw = SliceAfter(json, "\"ghost\":");
            s.Loop.State.GhostSeat = ghostRaw != null && ghostRaw.StartsWith("null") ? (int?)null : ghost;

            List<string> seats = Protocol.ExtractObjects(json, "seats");
            for (int i = 0; i < seats.Count && i < s.Loop.State.Seats.Length; i++)
                ReadSeat(s.Loop.State.Seats[i], seats[i]);
            return s;
        }

        static void WriteSeat(StringBuilder sb, PlayerState p)
        {
            sb.Append("{\"seat\":").Append(p.Seat);
            sb.Append(",\"wick\":").Append(p.Wick);
            sb.Append(",\"embers\":").Append(p.Embers);
            sb.Append(",\"depth\":").Append(p.Depth);
            sb.Append(",\"up\":").Append(p.UpgradeCost);
            sb.Append(",\"hold\":").Append(p.Hold ? "true" : "false");
            sb.Append(",\"ready\":").Append(p.Ready ? "true" : "false");
            sb.Append(",\"bot\":").Append(p.IsBot ? "true" : "false");
            sb.Append(",\"name\":\"").Append(Esc(p.DisplayName)).Append('"');
            sb.Append(",\"captain\":\"").Append(Esc(p.Captain.Value)).Append('"');
            sb.Append(",\"flags\":").Append((uint)p.Flags);
            sb.Append(",\"pending\":").Append(p.PendingEmbers);
            sb.Append(",\"dredger\":").Append(p.DredgerBonus);
            sb.Append(",\"place\":").Append(p.Place.HasValue ? p.Place.Value.ToString(CultureInfo.InvariantCulture) : "null");
            sb.Append(",\"board\":"); WriteUnits(sb, p.Board, indexed: false);
            sb.Append(",\"hand\":"); WriteUnits(sb, p.Hand, indexed: false);
            sb.Append(",\"stall\":"); WriteStall(sb, p.Stall);
            sb.Append('}');
        }

        static void WriteStall(StringBuilder sb, List<UnitInstance> stall)
        {
            sb.Append('[');
            bool first = true;
            for (int i = 0; i < stall.Count; i++)
            {
                if (stall[i] == null) continue;
                if (!first) sb.Append(',');
                first = false;
                WriteUnit(sb, stall[i], i);
            }
            sb.Append(']');
        }

        static void WriteUnits(StringBuilder sb, List<UnitInstance> list, bool indexed)
        {
            sb.Append('[');
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                WriteUnit(sb, list[i], i);
            }
            sb.Append(']');
        }

        static void WriteUnit(StringBuilder sb, UnitInstance u, int slot)
        {
            sb.Append("{\"slot\":").Append(slot);
            sb.Append(",\"iid\":").Append(u.InstanceId.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"id\":\"").Append(Esc(u.CatalogId.Value)).Append('"');
            sb.Append(",\"atk\":").Append(u.Atk);
            sb.Append(",\"hp\":").Append(u.Hp);
            sb.Append(",\"max\":").Append(u.MaxHp);
            sb.Append(",\"kw\":").Append((ushort)u.Keywords);
            sb.Append(",\"aw\":").Append(u.Awakened ? "true" : "false");
            sb.Append(",\"ea\":").Append(u.ExtraAtk);
            sb.Append(",\"eh\":").Append(u.ExtraHp);
            sb.Append(",\"cinder\":").Append(u.Cinders);
            sb.Append('}');
        }

        static void ReadSeat(PlayerState p, string json)
        {
            p.Wick = Protocol.ReadInt(json, "wick");
            p.Embers = Protocol.ReadInt(json, "embers");
            p.Depth = Protocol.ReadInt(json, "depth");
            p.UpgradeCost = Protocol.ReadInt(json, "up");
            p.Hold = Protocol.ReadBool(json, "hold");
            p.Ready = Protocol.ReadBool(json, "ready");
            p.IsBot = Protocol.ReadBool(json, "bot");
            p.DisplayName = Protocol.ReadString(json, "name");
            p.Captain = new CaptainId(Protocol.ReadString(json, "captain"));
            p.Flags = (PlayerFlags)(uint)Protocol.ReadInt(json, "flags");
            p.PendingEmbers = Protocol.ReadInt(json, "pending");
            p.DredgerBonus = Protocol.ReadInt(json, "dredger");
            string placeRaw = SliceAfter(json, "\"place\":");
            if (placeRaw != null && !placeRaw.StartsWith("null"))
                p.Place = Protocol.ReadInt(json, "place");
            p.Board.Clear();
            List<string> board = Protocol.ExtractObjects(json, "board");
            for (int i = 0; i < board.Count; i++) p.Board.Add(ReadUnit(board[i]));
            p.Hand.Clear();
            List<string> hand = Protocol.ExtractObjects(json, "hand");
            for (int i = 0; i < hand.Count; i++) p.Hand.Add(ReadUnit(hand[i]));
            p.Stall.Clear();
            List<string> stall = Protocol.ExtractObjects(json, "stall");
            int maxSlot = -1;
            var stallUnits = new List<UnitInstance>();
            var stallSlots = new List<int>();
            for (int i = 0; i < stall.Count; i++)
            {
                int slot = Protocol.ReadInt(stall[i], "slot");
                stallSlots.Add(slot);
                stallUnits.Add(ReadUnit(stall[i]));
                if (slot > maxSlot) maxSlot = slot;
            }
            int size = maxSlot + 1;
            if (size < 3) size = 3;
            for (int i = 0; i < size; i++) p.Stall.Add(null);
            for (int i = 0; i < stallUnits.Count; i++)
            {
                int slot = stallSlots[i];
                if (slot >= 0 && slot < p.Stall.Count)
                    p.Stall[slot] = stallUnits[i];
            }
        }

        static UnitInstance ReadUnit(string json)
        {
            int hp = Protocol.ReadInt(json, "hp");
            int max = Protocol.ReadInt(json, "max");
            if (max < hp) max = hp;
            return new UnitInstance
            {
                InstanceId = ReadUlong(json, "iid"),
                CatalogId = new UnitId(Protocol.ReadString(json, "id")),
                Atk = Protocol.ReadInt(json, "atk"),
                Hp = hp,
                MaxHp = max,
                Keywords = (Keyword)(ushort)Protocol.ReadInt(json, "kw"),
                Awakened = Protocol.ReadBool(json, "aw"),
                ExtraAtk = Protocol.ReadInt(json, "ea"),
                ExtraHp = Protocol.ReadInt(json, "eh"),
                Cinders = Protocol.ReadInt(json, "cinder"),
                AttackCharges = 1
            };
        }

        static Phase ParsePhase(string s)
        {
            if (Enum.TryParse(s, true, out Phase p)) return p;
            return Phase.Recruit;
        }

        static string ExtractObject(string json, string key)
        {
            string needle = "\"" + key + "\":";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return "";
            i = json.IndexOf('{', i);
            if (i < 0) return "";
            int start = i;
            int depth = 0;
            do
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') depth--;
                i++;
            } while (i < json.Length && depth > 0);
            return json.Substring(start, i - start);
        }

        static string SliceAfter(string json, string needle)
        {
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return null;
            i += needle.Length;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t')) i++;
            return i < json.Length ? json.Substring(i) : null;
        }

        static long ToUnix(DateTime utc)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(utc.ToUniversalTime() - epoch).TotalSeconds;
        }

        static DateTime FromUnix(long sec)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(sec);
        }

        static long ReadLong(string json, string key)
        {
            string needle = "\"" + key + "\":";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return 0;
            i += needle.Length;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t')) i++;
            long v = 0;
            while (i < json.Length && json[i] >= '0' && json[i] <= '9')
            {
                v = v * 10 + (json[i] - '0');
                i++;
            }
            return v;
        }

        static ulong ReadUlong(string json, string key)
        {
            string needle = "\"" + key + "\":";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return 0;
            i += needle.Length;
            while (i < json.Length && (json[i] < '0' || json[i] > '9')) i++;
            ulong v = 0;
            while (i < json.Length && json[i] >= '0' && json[i] <= '9')
            {
                v = v * 10UL + (ulong)(json[i] - '0');
                i++;
            }
            return v;
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
