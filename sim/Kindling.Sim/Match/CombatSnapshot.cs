using System.Collections.Generic;
using System.Text;
using Kindling.Sim.Model;

namespace Kindling.Sim.Match
{
    public static class CombatSnapshot
    {
        const int MaxEvents = 48;

        public static void Write(StringBuilder sb, CombatResult cr)
        {
            if (sb == null) return;
            if (cr == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append("{\"damage\":").Append(cr.Damage);
            sb.Append(",\"draw\":").Append(cr.Draw ? "true" : "false");
            sb.Append(",\"winnerSeat\":").Append(cr.WinnerSeat);
            sb.Append(",\"seatA\":").Append(cr.SeatA);
            sb.Append(",\"seatB\":").Append(cr.SeatB);
            sb.Append(",\"nameA\":\"").Append(Esc(cr.NameA)).Append('"');
            sb.Append(",\"nameB\":\"").Append(Esc(cr.NameB)).Append('"');
            sb.Append(",\"depthA\":").Append(cr.DepthA);
            sb.Append(",\"depthB\":").Append(cr.DepthB);
            sb.Append(",\"wickA\":").Append(cr.WickA);
            sb.Append(",\"wickB\":").Append(cr.WickB);
            sb.Append(",\"remainingA\":").Append(cr.RemainingA);
            sb.Append(",\"remainingB\":").Append(cr.RemainingB);
            sb.Append(",\"boardA\":");
            WritePieces(sb, cr.BoardA);
            sb.Append(",\"boardB\":");
            WritePieces(sb, cr.BoardB);
            sb.Append(",\"events\":[");
            int n = cr.Events != null ? cr.Events.Count : 0;
            if (n > MaxEvents) n = MaxEvents;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append(',');
                CombatEvent e = cr.Events[i];
                sb.Append("{\"op\":\"").Append(e.Op).Append('"');
                sb.Append(",\"srcSeat\":").Append(e.SrcSeat);
                sb.Append(",\"dstSeat\":").Append(e.DstSeat);
                sb.Append(",\"srcInstance\":\"").Append(e.SrcInstance).Append('"');
                sb.Append(",\"dstInstance\":\"").Append(e.DstInstance).Append('"');
                sb.Append(",\"srcSlot\":").Append(e.SrcSlot);
                sb.Append(",\"dstSlot\":").Append(e.DstSlot);
                sb.Append(",\"amount\":").Append(e.Amount);
                sb.Append(",\"atk\":").Append(e.Atk);
                sb.Append(",\"hpAfter\":").Append(e.HpAfter);
                if (!string.IsNullOrEmpty(e.CatalogId))
                    sb.Append(",\"catalogId\":\"").Append(Esc(e.CatalogId)).Append('"');
                sb.Append('}');
            }
            sb.Append("]}");
        }

        static void WritePieces(StringBuilder sb, List<CombatPiece> list)
        {
            sb.Append('[');
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    CombatPiece p = list[i];
                    sb.Append("{\"instanceId\":\"").Append(p.InstanceId).Append('"');
                    sb.Append(",\"catalogId\":\"").Append(Esc(p.CatalogId.Value)).Append('"');
                    sb.Append(",\"atk\":").Append(p.Atk);
                    sb.Append(",\"hp\":").Append(p.Hp);
                    sb.Append(",\"kw\":").Append((int)p.Keywords);
                    sb.Append(",\"awakened\":").Append(p.Awakened ? "true" : "false");
                    sb.Append(",\"seat\":").Append(p.Seat);
                    sb.Append('}');
                }
            }
            sb.Append(']');
        }

        public static CombatResult Read(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "null") return null;
            var cr = new CombatResult
            {
                Damage = Protocol.ReadInt(json, "damage"),
                Draw = Protocol.ReadBool(json, "draw"),
                WinnerSeat = Protocol.ReadInt(json, "winnerSeat"),
                SeatA = Protocol.ReadInt(json, "seatA"),
                SeatB = Protocol.ReadInt(json, "seatB"),
                NameA = Protocol.ReadString(json, "nameA"),
                NameB = Protocol.ReadString(json, "nameB"),
                DepthA = Protocol.ReadInt(json, "depthA"),
                DepthB = Protocol.ReadInt(json, "depthB"),
                WickA = Protocol.ReadInt(json, "wickA"),
                WickB = Protocol.ReadInt(json, "wickB"),
                RemainingA = Protocol.ReadInt(json, "remainingA"),
                RemainingB = Protocol.ReadInt(json, "remainingB")
            };
            if (Protocol.ReadString(json, "winnerSeat") == "" && json.IndexOf("\"winnerSeat\":-", System.StringComparison.Ordinal) >= 0)
                cr.WinnerSeat = Protocol.ReadInt(json, "winnerSeat");
            List<string> a = Protocol.ExtractObjects(json, "boardA");
            for (int i = 0; i < a.Count; i++)
                cr.BoardA.Add(ReadPiece(a[i]));
            List<string> b = Protocol.ExtractObjects(json, "boardB");
            for (int i = 0; i < b.Count; i++)
                cr.BoardB.Add(ReadPiece(b[i]));
            List<string> ev = Protocol.ExtractObjects(json, "events");
            for (int i = 0; i < ev.Count; i++)
                cr.Events.Add(ReadEvent(ev[i]));
            return cr;
        }

        static CombatPiece ReadPiece(string json)
        {
            ulong iid = 0;
            string iidS = Protocol.ReadString(json, "instanceId");
            if (!string.IsNullOrEmpty(iidS)) ulong.TryParse(iidS, out iid);
            int hp = Protocol.ReadInt(json, "hp");
            return new CombatPiece
            {
                InstanceId = iid,
                CatalogId = new UnitId(Protocol.ReadString(json, "catalogId")),
                Atk = Protocol.ReadInt(json, "atk"),
                Hp = hp,
                MaxHp = hp,
                Keywords = (Keyword)Protocol.ReadInt(json, "kw"),
                Awakened = Protocol.ReadBool(json, "awakened"),
                Seat = Protocol.ReadInt(json, "seat")
            };
        }

        static CombatEvent ReadEvent(string json)
        {
            ulong src = 0, dst = 0;
            string s = Protocol.ReadString(json, "srcInstance");
            if (!string.IsNullOrEmpty(s)) ulong.TryParse(s, out src);
            string d = Protocol.ReadString(json, "dstInstance");
            if (!string.IsNullOrEmpty(d)) ulong.TryParse(d, out dst);
            CombatOp op = CombatOp.Attack;
            System.Enum.TryParse(Protocol.ReadString(json, "op"), true, out op);
            return new CombatEvent
            {
                Op = op,
                SrcSeat = Protocol.ReadInt(json, "srcSeat"),
                DstSeat = Protocol.ReadInt(json, "dstSeat"),
                SrcInstance = src,
                DstInstance = dst,
                SrcSlot = Protocol.ReadInt(json, "srcSlot"),
                DstSlot = Protocol.ReadInt(json, "dstSlot"),
                Amount = Protocol.ReadInt(json, "amount"),
                Atk = Protocol.ReadInt(json, "atk"),
                HpAfter = Protocol.ReadInt(json, "hpAfter"),
                CatalogId = Protocol.ReadString(json, "catalogId")
            };
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
