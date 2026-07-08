using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DreamGate.Battlegrounds.Networking
{
    public static class MatchSnapshotJson
    {
        public static string ToJson(MatchSnapshot snapshot) => JsonUtility.ToJson(snapshot);

        public static bool TryParse(string json, out MatchSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                snapshot = JsonUtility.FromJson<MatchSnapshot>(json);
                return snapshot != null;
            }
            catch
            {
                return false;
            }
        }

        public static string BuildActionPayload(Dictionary<string, int> payload)
        {
            if (payload == null || payload.Count == 0)
            {
                return "{}";
            }

            var builder = new StringBuilder("{");
            var first = true;
            foreach (var pair in payload)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append('"').Append(pair.Key).Append("\":").Append(pair.Value);
            }

            builder.Append('}');
            return builder.ToString();
        }
    }
}