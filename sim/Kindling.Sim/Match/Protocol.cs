using System;
using Kindling.Sim.Model;

namespace Kindling.Sim.Match
{
    public static class Protocol
    {
        public const int Version = 1;

        public static RecruitAction Parse(string json, int seat)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string op = ReadString(json, "op");
            if (string.IsNullOrEmpty(op)) return null;
            var a = new RecruitAction { Seat = seat, Seq = ReadInt(json, "seq") };
            switch (op)
            {
                case "CaptainPick":
                    a.Op = RecruitOp.CaptainPick;
                    a.CaptainId = ReadString(json, "captainId");
                    a.OfferIndex = ReadInt(json, "offerIndex");
                    break;
                case "Buy":
                    a.Op = RecruitOp.Buy;
                    a.StallIndex = ReadInt(json, "stallIndex");
                    a.Dest = ParseDest(ReadString(json, "dest"), DestLoc.Hand);
                    a.DestIndex = ReadInt(json, "destIndex");
                    break;
                case "Sell":
                    a.Op = RecruitOp.Sell;
                    a.Loc = ParseDest(ReadString(json, "loc"), DestLoc.Board);
                    a.Index = ReadInt(json, "index");
                    break;
                case "Reroll":
                    a.Op = RecruitOp.Reroll;
                    break;
                case "Hold":
                    a.Op = RecruitOp.Hold;
                    a.Held = ReadBool(json, "held");
                    break;
                case "Upgrade":
                    a.Op = RecruitOp.Upgrade;
                    break;
                case "Play":
                    a.Op = RecruitOp.Play;
                    a.HandIndex = ReadInt(json, "handIndex");
                    a.DestIndex = ReadInt(json, "destIndex");
                    a.Dest = DestLoc.Board;
                    break;
                case "Latch":
                    a.Op = RecruitOp.Latch;
                    a.From = ParseDest(ReadString(json, "from"), DestLoc.Hand);
                    a.FromIndex = ReadInt(json, "fromIndex");
                    a.HostIndex = ReadInt(json, "hostIndex");
                    break;
                case "Edict":
                    a.Op = RecruitOp.Edict;
                    a.TargetIndex = ReadInt(json, "targetIndex");
                    if (a.TargetIndex == 0 && json.IndexOf("\"index\":", StringComparison.Ordinal) >= 0)
                        a.TargetIndex = ReadNestedIndex(json);
                    break;
                case "GlimpsePick":
                    a.Op = RecruitOp.GlimpsePick;
                    a.OfferIndex = ReadInt(json, "offerIndex");
                    break;
                case "Ready":
                    a.Op = RecruitOp.Ready;
                    break;
                case "Reorder":
                    a.Op = RecruitOp.Reorder;
                    a.BoardPerm = ReadIntArray(json, "board");
                    break;
                default:
                    return null;
            }
            return a;
        }

        static DestLoc ParseDest(string s, DestLoc fallback)
        {
            if (s == "Hand") return DestLoc.Hand;
            if (s == "Board") return DestLoc.Board;
            if (s == "Stall") return DestLoc.Stall;
            return fallback;
        }

        static int ReadNestedIndex(string json)
        {
            int t = json.IndexOf("\"target\"", StringComparison.Ordinal);
            if (t < 0) return ReadInt(json, "index");
            int i = json.IndexOf("\"index\":", t, StringComparison.Ordinal);
            if (i < 0) return -1;
            return ReadIntAt(json, i + 8);
        }

        public static string ReadString(string json, string key)
        {
            string needle = "\"" + key + "\":";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return "";
            i = SkipWs(json, i + needle.Length);
            if (i >= json.Length || json[i] != '"') return "";
            i++;
            int start = i;
            while (i < json.Length && json[i] != '"') i++;
            return json.Substring(start, i - start);
        }

        public static int ReadInt(string json, string key)
        {
            string needle = "\"" + key + "\":";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return 0;
            return ReadIntAt(json, i + needle.Length);
        }

        public static bool ReadBool(string json, string key)
        {
            string needle = "\"" + key + "\":";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return false;
            i = SkipWs(json, i + needle.Length);
            return i + 4 <= json.Length && json.Substring(i, 4) == "true";
        }

        public static int[] ReadIntArrayPublic(string json, string key)
        {
            return ReadIntArray(json, key);
        }

        static int[] ReadIntArray(string json, string key)
        {
            string needle = "\"" + key + "\":";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return null;
            i = json.IndexOf('[', i);
            if (i < 0) return null;
            i++;
            var list = new System.Collections.Generic.List<int>();
            while (i < json.Length && json[i] != ']')
            {
                i = SkipWs(json, i);
                if (json[i] == ']') break;
                if (json[i] == ',') { i++; continue; }
                int v = ReadIntAt(json, i);
                list.Add(v);
                while (i < json.Length && ((json[i] >= '0' && json[i] <= '9') || json[i] == '-')) i++;
            }
            return list.ToArray();
        }

        static int ReadIntAt(string s, int i)
        {
            i = SkipWs(s, i);
            int sign = 1;
            if (i < s.Length && s[i] == '-') { sign = -1; i++; }
            int v = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
            {
                v = v * 10 + (s[i] - '0');
                i++;
            }
            return v * sign;
        }

        public static string ExtractObject(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
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

        public static System.Collections.Generic.List<string> ExtractObjects(string json, string key)
        {
            var list = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(json)) return list;
            string needle = "\"" + key + "\":";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return list;
            i = json.IndexOf('[', i);
            if (i < 0) return list;
            i++;
            while (i < json.Length)
            {
                while (i < json.Length && json[i] != '{' && json[i] != ']') i++;
                if (i >= json.Length || json[i] == ']') break;
                int start = i;
                int depth = 0;
                do
                {
                    if (json[i] == '{') depth++;
                    else if (json[i] == '}') depth--;
                    i++;
                } while (i < json.Length && depth > 0);
                list.Add(json.Substring(start, i - start));
            }
            return list;
        }

        static int SkipWs(string s, int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) i++;
            return i;
        }
    }
}
