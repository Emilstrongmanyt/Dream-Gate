using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    public static class ApiJson
    {
        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        public static string BuildObject(Dictionary<string, object> values)
        {
            var builder = new StringBuilder();
            builder.Append('{');
            var first = true;
            foreach (var pair in values)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append('"').Append(pair.Key).Append("\":");
                builder.Append(SerializeValue(pair.Value));
            }

            builder.Append('}');
            return builder.ToString();
        }

        public static string TryGetString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return null;
            }

            var pattern = $"\"{key}\":";
            var index = json.IndexOf(pattern, StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            index += pattern.Length;
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }

            if (index >= json.Length)
            {
                return null;
            }

            if (json[index] == '"')
            {
                index++;
                var end = index;
                while (end < json.Length)
                {
                    if (json[end] == '"' && json[end - 1] != '\\')
                    {
                        break;
                    }

                    end++;
                }

                return json.Substring(index, end - index);
            }

            if (json.Substring(index).StartsWith("null", StringComparison.Ordinal))
            {
                return null;
            }

            var terminator = json.IndexOfAny(new[] { ',', '}', ']' }, index);
            if (terminator < 0)
            {
                terminator = json.Length;
            }

            return json.Substring(index, terminator - index).Trim();
        }

        public static int TryGetInt(string json, string key, int fallback = 0)
        {
            var raw = TryGetString(json, key);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        }

        public static bool TryGetBool(string json, string key, bool fallback = false)
        {
            var raw = TryGetString(json, key);
            return bool.TryParse(raw, out var value) ? value : fallback;
        }

        public static List<string> ExtractObjectChunks(string arrayJson)
        {
            var results = new List<string>();
            if (string.IsNullOrEmpty(arrayJson))
            {
                return results;
            }

            var depth = 0;
            var start = -1;
            for (var i = 0; i < arrayJson.Length; i++)
            {
                var ch = arrayJson[i];
                if (ch == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                    }

                    depth++;
                }
                else if (ch == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        results.Add(arrayJson.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }

            return results;
        }

        private static string SerializeValue(object value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case string s:
                    return $"\"{Escape(s)}\"";
                case bool b:
                    return b ? "true" : "false";
                case int i:
                    return i.ToString(CultureInfo.InvariantCulture);
                case long l:
                    return l.ToString(CultureInfo.InvariantCulture);
                case float f:
                    return f.ToString(CultureInfo.InvariantCulture);
                case double d:
                    return d.ToString(CultureInfo.InvariantCulture);
                default:
                    return $"\"{Escape(value.ToString())}\"";
            }
        }
    }
}