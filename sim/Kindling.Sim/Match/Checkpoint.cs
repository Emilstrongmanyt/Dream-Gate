using System;
using System.Globalization;
using System.Text;
using Kindling.Sim.Rng;

namespace Kindling.Sim.Match
{
    public static class Checkpoint
    {
        public static string SerializeRng(MatchRng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var sb = new StringBuilder();
            sb.Append("{\"nextInstanceId\":");
            sb.Append(rng.NextInstanceId.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"states\":[");
            for (int s = 1; s <= MatchRng.StreamCount; s++)
            {
                if (s > 1) sb.Append(',');
                Pcg32State st = rng.States[s];
                sb.Append("{\"stream\":");
                sb.Append(s.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"s0\":");
                sb.Append(st.S0.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"s1\":");
                sb.Append(st.S1.ToString(CultureInfo.InvariantCulture));
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static MatchRng DeserializeRng(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("empty rng checkpoint");
            var rng = new MatchRng();
            rng.NextInstanceId = ReadUlong(json, "nextInstanceId");
            int pos = 0;
            while (true)
            {
                int streamIdx = IndexOfFrom(json, "\"stream\":", pos);
                if (streamIdx < 0) break;
                pos = streamIdx + 9;
                int stream = ReadIntAt(json, pos);
                int s0i = IndexOfFrom(json, "\"s0\":", pos);
                int s1i = IndexOfFrom(json, "\"s1\":", pos);
                if (s0i < 0 || s1i < 0) break;
                uint s0 = ReadUintAt(json, s0i + 5);
                uint s1 = ReadUintAt(json, s1i + 5);
                if (stream >= 1 && stream <= MatchRng.StreamCount)
                    rng.States[stream] = Pcg32State.From(((ulong)s1 << 32) | s0);
                pos = s1i + 5;
            }
            return rng;
        }

        static ulong ReadUlong(string json, string key)
        {
            int i = json.IndexOf("\"" + key + "\":", StringComparison.Ordinal);
            if (i < 0) return 0;
            i += key.Length + 3;
            return ReadUlongAt(json, i);
        }

        static int IndexOfFrom(string s, string needle, int start)
        {
            return s.IndexOf(needle, start, StringComparison.Ordinal);
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

        static uint ReadUintAt(string s, int i)
        {
            i = SkipWs(s, i);
            uint v = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
            {
                v = v * 10u + (uint)(s[i] - '0');
                i++;
            }
            return v;
        }

        static ulong ReadUlongAt(string s, int i)
        {
            i = SkipWs(s, i);
            ulong v = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
            {
                v = v * 10UL + (ulong)(s[i] - '0');
                i++;
            }
            return v;
        }

        static int SkipWs(string s, int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t'))
                i++;
            return i;
        }
    }
}
