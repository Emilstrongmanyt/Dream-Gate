using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kindling.Sim.Catalog
{
    public sealed class YamlNode
    {
        public enum Kind { Scalar, Mapping, List }

        public Kind Type;
        public string Scalar;
        public List<KeyValuePair<string, YamlNode>> Map;
        public List<YamlNode> Items;

        public static YamlNode FromScalar(string s)
        {
            return new YamlNode { Type = Kind.Scalar, Scalar = s ?? "" };
        }

        public static YamlNode FromMap()
        {
            return new YamlNode { Type = Kind.Mapping, Map = new List<KeyValuePair<string, YamlNode>>() };
        }

        public static YamlNode FromList()
        {
            return new YamlNode { Type = Kind.List, Items = new List<YamlNode>() };
        }

        public bool IsNullOrEmpty()
        {
            if (Type == Kind.Scalar)
                return string.IsNullOrEmpty(Scalar) || Scalar == "null" || Scalar == "~";
            return false;
        }

        public string GetString(string key, string fallback = "")
        {
            YamlNode n = Get(key);
            if (n == null || n.Type != Kind.Scalar) return fallback;
            return n.Scalar ?? fallback;
        }

        public int GetInt(string key, int fallback = 0)
        {
            YamlNode n = Get(key);
            if (n == null || n.Type != Kind.Scalar) return fallback;
            if (int.TryParse(n.Scalar, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return v;
            return fallback;
        }

        public bool GetBool(string key, bool fallback = false)
        {
            YamlNode n = Get(key);
            if (n == null || n.Type != Kind.Scalar) return fallback;
            string s = n.Scalar;
            if (s == "true" || s == "True" || s == "yes") return true;
            if (s == "false" || s == "False" || s == "no") return false;
            return fallback;
        }

        public bool TryGetInt(string key, out int value)
        {
            value = 0;
            YamlNode n = Get(key);
            if (n == null || n.Type != Kind.Scalar) return false;
            return int.TryParse(n.Scalar, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public YamlNode Get(string key)
        {
            if (Type != Kind.Mapping || Map == null) return null;
            for (int i = 0; i < Map.Count; i++)
            {
                if (Map[i].Key == key) return Map[i].Value;
            }
            return null;
        }

        public void Set(string key, YamlNode value)
        {
            if (Map == null) Map = new List<KeyValuePair<string, YamlNode>>();
            Map.Add(new KeyValuePair<string, YamlNode>(key, value));
        }

        public List<string> StringList(string key)
        {
            var result = new List<string>();
            YamlNode n = Get(key);
            if (n == null) return result;
            if (n.Type == Kind.Scalar && !n.IsNullOrEmpty())
            {
                result.Add(n.Scalar);
                return result;
            }
            if (n.Type == Kind.List && n.Items != null)
            {
                for (int i = 0; i < n.Items.Count; i++)
                {
                    if (n.Items[i].Type == Kind.Scalar) result.Add(n.Items[i].Scalar);
                }
            }
            return result;
        }
    }

    public static class TinyYaml
    {
        public static YamlNode Parse(string text)
        {
            if (text == null) text = "";
            string[] raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var lines = new List<Line>();
            for (int i = 0; i < raw.Length; i++)
            {
                string t = StripComment(raw[i]);
                if (t.Trim().Length == 0) continue;
                int indent = 0;
                while (indent < t.Length && t[indent] == ' ') indent++;
                lines.Add(new Line { Indent = indent, Text = t.Substring(indent) });
            }
            int idx = 0;
            if (lines.Count == 0) return YamlNode.FromMap();
            return ParseNode(lines, ref idx, lines[0].Indent);
        }

        struct Line
        {
            public int Indent;
            public string Text;
        }

        static string StripComment(string line)
        {
            bool quote = false;
            char q = '\0';
            var sb = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quote)
                {
                    sb.Append(c);
                    if (c == q) quote = false;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    quote = true;
                    q = c;
                    sb.Append(c);
                    continue;
                }
                if (c == '#') break;
                sb.Append(c);
            }
            return sb.ToString().TrimEnd();
        }

        static YamlNode ParseNode(List<Line> lines, ref int idx, int indent)
        {
            if (idx >= lines.Count) return YamlNode.FromMap();
            Line cur = lines[idx];
            if (cur.Text.StartsWith("- ") || cur.Text == "-")
                return ParseList(lines, ref idx, indent);
            return ParseMap(lines, ref idx, indent);
        }

        static YamlNode ParseMap(List<Line> lines, ref int idx, int indent)
        {
            YamlNode map = YamlNode.FromMap();
            while (idx < lines.Count)
            {
                Line cur = lines[idx];
                if (cur.Indent < indent) break;
                if (cur.Indent > indent)
                    throw new InvalidOperationException("YAML indent error near: " + cur.Text);
                if (cur.Text.StartsWith("- ") || cur.Text == "-")
                    break;
                int colon = IndexOfKeyColon(cur.Text);
                if (colon < 0)
                    throw new InvalidOperationException("YAML expected key: " + cur.Text);
                string key = Unquote(cur.Text.Substring(0, colon).Trim());
                string rest = cur.Text.Substring(colon + 1).Trim();
                idx++;
                YamlNode val;
                if (rest.Length == 0)
                {
                    if (idx < lines.Count && lines[idx].Indent > indent)
                        val = ParseNode(lines, ref idx, lines[idx].Indent);
                    else
                        val = YamlNode.FromScalar("");
                }
                else
                {
                    val = ParseValue(rest);
                }
                map.Set(key, val);
            }
            return map;
        }

        static YamlNode ParseList(List<Line> lines, ref int idx, int indent)
        {
            YamlNode list = YamlNode.FromList();
            while (idx < lines.Count)
            {
                Line cur = lines[idx];
                if (cur.Indent < indent) break;
                if (!(cur.Text.StartsWith("- ") || cur.Text == "-"))
                    break;
                string rest = cur.Text == "-" ? "" : cur.Text.Substring(2).Trim();
                idx++;
                YamlNode item;
                if (rest.Length == 0)
                {
                    if (idx < lines.Count && lines[idx].Indent > indent)
                        item = ParseNode(lines, ref idx, lines[idx].Indent);
                    else
                        item = YamlNode.FromScalar("");
                }
                else if (rest.IndexOf(':') >= 0 && !rest.StartsWith("{") && !rest.StartsWith("["))
                {
                    var fake = new List<Line>();
                    fake.Add(new Line { Indent = 0, Text = rest });
                    while (idx < lines.Count && lines[idx].Indent > cur.Indent)
                    {
                        fake.Add(new Line
                        {
                            Indent = lines[idx].Indent - cur.Indent - 2,
                            Text = lines[idx].Text
                        });
                        if (fake[fake.Count - 1].Indent < 0)
                            fake[fake.Count - 1] = new Line { Indent = 0, Text = lines[idx].Text };
                        idx++;
                    }
                    int f = 0;
                    item = ParseMap(fake, ref f, 0);
                    if (f < fake.Count)
                    {
                    }
                }
                else
                {
                    item = ParseValue(rest);
                    if (idx < lines.Count && lines[idx].Indent > cur.Indent)
                    {
                        YamlNode nested = ParseNode(lines, ref idx, lines[idx].Indent);
                        if (item.Type == YamlNode.Kind.Scalar && (item.Scalar == "" || item.IsNullOrEmpty()))
                            item = nested;
                        else if (item.Type == YamlNode.Kind.Mapping && nested.Type == YamlNode.Kind.Mapping && nested.Map != null)
                        {
                            for (int i = 0; i < nested.Map.Count; i++)
                                item.Set(nested.Map[i].Key, nested.Map[i].Value);
                        }
                    }
                }
                list.Items.Add(item);
            }
            return list;
        }

        static int IndexOfKeyColon(string text)
        {
            bool quote = false;
            char q = '\0';
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quote)
                {
                    if (c == q) quote = false;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    quote = true;
                    q = c;
                    continue;
                }
                if (c == ':' && (i + 1 >= text.Length || text[i + 1] == ' ' || text[i + 1] == '\t'))
                    return i;
                if (c == ':' && i == text.Length - 1)
                    return i;
            }
            int c2 = text.IndexOf(':');
            return c2;
        }

        static YamlNode ParseValue(string rest)
        {
            if (rest.StartsWith("[")) return ParseInlineList(rest);
            if (rest.StartsWith("{")) return ParseInlineMap(rest);
            return YamlNode.FromScalar(Unquote(rest));
        }

        static YamlNode ParseInlineList(string s)
        {
            YamlNode list = YamlNode.FromList();
            if (s.Length < 2) return list;
            string inner = s.Trim();
            if (inner.StartsWith("[")) inner = inner.Substring(1);
            if (inner.EndsWith("]")) inner = inner.Substring(0, inner.Length - 1);
            inner = inner.Trim();
            if (inner.Length == 0) return list;
            List<string> parts = SplitTop(inner, ',');
            for (int i = 0; i < parts.Count; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                list.Items.Add(ParseValue(p));
            }
            return list;
        }

        static YamlNode ParseInlineMap(string s)
        {
            YamlNode map = YamlNode.FromMap();
            string inner = s.Trim();
            if (inner.StartsWith("{")) inner = inner.Substring(1);
            if (inner.EndsWith("}")) inner = inner.Substring(0, inner.Length - 1);
            inner = inner.Trim();
            if (inner.Length == 0) return map;
            List<string> parts = SplitTop(inner, ',');
            for (int i = 0; i < parts.Count; i++)
            {
                string p = parts[i].Trim();
                int c = IndexOfKeyColon(p);
                if (c < 0) continue;
                string k = Unquote(p.Substring(0, c).Trim());
                string v = p.Substring(c + 1).Trim();
                map.Set(k, ParseValue(v));
            }
            return map;
        }

        static List<string> SplitTop(string s, char sep)
        {
            var parts = new List<string>();
            int depthSq = 0, depthBr = 0;
            bool quote = false;
            char q = '\0';
            int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (quote)
                {
                    if (c == q) quote = false;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    quote = true;
                    q = c;
                    continue;
                }
                if (c == '[') depthSq++;
                else if (c == ']') depthSq--;
                else if (c == '{') depthBr++;
                else if (c == '}') depthBr--;
                else if (c == sep && depthSq == 0 && depthBr == 0)
                {
                    parts.Add(s.Substring(start, i - start));
                    start = i + 1;
                }
            }
            parts.Add(s.Substring(start));
            return parts;
        }

        static string Unquote(string s)
        {
            if (s == null) return "";
            s = s.Trim();
            if (s.Length >= 2)
            {
                if ((s[0] == '"' && s[s.Length - 1] == '"') || (s[0] == '\'' && s[s.Length - 1] == '\''))
                    return s.Substring(1, s.Length - 2);
            }
            return s;
        }
    }
}
