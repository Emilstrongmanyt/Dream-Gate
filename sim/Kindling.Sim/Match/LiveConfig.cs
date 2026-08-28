using Kindling.Sim.Catalog;

namespace Kindling.Sim.Match
{
    public static class LiveConfig
    {
        public static string Json(Catalog.Catalog cat)
        {
            string ver = cat != null && !string.IsNullOrEmpty(cat.ContentVersion) ? cat.ContentVersion : "0";
            return "{\"protocolVersion\":" + Protocol.Version
                + ",\"catalogVersion\":\"" + Esc(ver) + "\""
                + ",\"rankedEnabled\":false"
                + ",\"casualBots\":true"
                + ",\"captainOfferCount\":" + Rules.CaptainOfferCount
                + ",\"buyCost\":" + Rules.BuyCost
                + ",\"sellReward\":" + Rules.SellReward
                + ",\"handMax\":" + Rules.HandMax
                + ",\"boardMax\":" + Rules.BoardMax
                + ",\"recruitSecondsR1\":" + Rules.RecruitSeconds(1)
                + ",\"recruitSecondsR5\":" + Rules.RecruitSeconds(5)
                + ",\"captainPickSeconds\":" + Rules.CaptainPickSeconds
                + "}";
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
