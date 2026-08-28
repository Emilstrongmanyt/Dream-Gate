namespace Kindling.Sim.Match
{
    public static class Cosmetics
    {
        public static readonly string[] Frames = { "gold", "ember", "spirit", "wick", "night" };

        public static bool IsFrame(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < Frames.Length; i++)
                if (Frames[i] == id) return true;
            return false;
        }

        public static string NextFrame(string current)
        {
            int i = 0;
            for (; i < Frames.Length; i++)
                if (Frames[i] == current) break;
            if (i >= Frames.Length) return Frames[0];
            return Frames[(i + 1) % Frames.Length];
        }

        public static string GrantAll()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Frames.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Frames[i]);
            }
            return sb.ToString();
        }

        public static string PatchEquip(string prev, string frame)
        {
            if (!IsFrame(frame)) frame = "gold";
            if (prev == null) prev = "{}";
            string owned = Protocol.ReadString(prev, "cosmetics");
            if (string.IsNullOrEmpty(owned)) owned = "gold";
            if (("," + owned + ",").IndexOf("," + frame + ",", System.StringComparison.Ordinal) < 0)
                owned = owned + "," + frame;
            return AccountAuth.WithCosmetic(prev, owned, frame);
        }
    }
}
