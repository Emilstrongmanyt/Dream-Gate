using System.Collections.Generic;
using Kindling.Sim.Catalog;
using Kindling.Sim.Model;

namespace Kindling.Sim.Match
{
    public static class SnapshotApply
    {
        public static void Apply(MatchState m, int seat, Catalog.Catalog cat, string json)
        {
            if (m == null || json == null) return;
            string phase = Protocol.ReadString(json, "phase");
            if (!string.IsNullOrEmpty(phase) && System.Enum.TryParse(phase, true, out Phase p))
                m.Phase = p;
            int round = Protocol.ReadInt(json, "round");
            if (round > 0) m.Round = round;
            m.MatchOver = Protocol.ReadBool(json, "matchOver");
            string you = Protocol.ExtractObject(json, "you");
            if (!string.IsNullOrEmpty(you) && seat >= 0 && seat < m.Seats.Length)
                ApplyYou(m.Seats[seat], you);
            List<string> pub = Protocol.ExtractObjects(json, "public");
            for (int i = 0; i < pub.Count; i++)
            {
                int s = Protocol.ReadInt(pub[i], "seat");
                if (s < 0 || s >= m.Seats.Length) continue;
                PlayerState ps = m.Seats[s];
                string name = Protocol.ReadString(pub[i], "displayName");
                if (!string.IsNullOrEmpty(name)) ps.DisplayName = name;
                ps.Wick = Protocol.ReadInt(pub[i], "wick");
                ps.Depth = Protocol.ReadInt(pub[i], "depth");
                int place = Protocol.ReadInt(pub[i], "place");
                ps.Place = place > 0 ? (int?)place : ps.Place;
                if (!Protocol.ReadBool(pub[i], "alive") && ps.Wick > 0 && pub[i].IndexOf("\"alive\":false", System.StringComparison.Ordinal) >= 0)
                    ps.Wick = 0;
                string cap = Protocol.ReadString(pub[i], "captain");
                if (!string.IsNullOrEmpty(cap)) ps.Captain = new CaptainId(cap);
            }
            List<string> pairs = Protocol.ExtractObjects(json, "pairings");
            if (pairs.Count > 0)
            {
                m.Pairings = new Pairing[pairs.Count];
                for (int i = 0; i < pairs.Count; i++)
                {
                    m.Pairings[i] = new Pairing
                    {
                        PairIndex = i,
                        SeatA = Protocol.ReadInt(pairs[i], "a"),
                        SeatB = Protocol.ReadInt(pairs[i], "b"),
                        Ghost = Protocol.ReadBool(pairs[i], "g")
                    };
                }
            }
        }

        public static void ApplyYou(PlayerState p, string you)
        {
            if (p == null || string.IsNullOrEmpty(you)) return;
            p.Wick = Protocol.ReadInt(you, "wick");
            p.Embers = Protocol.ReadInt(you, "embers");
            p.Depth = Protocol.ReadInt(you, "depth");
            p.UpgradeCost = Protocol.ReadInt(you, "upgradeCost");
            p.Hold = Protocol.ReadBool(you, "hold");
            p.Ready = Protocol.ReadBool(you, "ready");
            p.Flags = (PlayerFlags)(uint)Protocol.ReadInt(you, "flags");
            int place = Protocol.ReadInt(you, "place");
            if (place > 0) p.Place = place;
            string cap = Protocol.ReadString(you, "captain");
            if (!string.IsNullOrEmpty(cap)) p.Captain = new CaptainId(cap);
            string[] offers = Protocol.ReadStringArray(you, "captainOffers");
            if (offers != null && offers.Length > 0)
            {
                p.CaptainOffers = new CaptainId[offers.Length];
                for (int i = 0; i < offers.Length; i++)
                    p.CaptainOffers[i] = new CaptainId(offers[i]);
            }
            if (p.Edict == null) p.Edict = new EdictState();
            p.Edict.UsedThisRecruit = Protocol.ReadBool(you, "edictUsed");
            string glimpse = Protocol.ExtractObject(you, "glimpse");
            p.GlimpseQueue.Clear();
            p.ClearFlag(PlayerFlags.GlimpseOpen);
            if (!string.IsNullOrEmpty(glimpse) && Protocol.ReadBool(glimpse, "open"))
            {
                p.SetFlag(PlayerFlags.GlimpseOpen);
                string[] choices = Protocol.ReadStringArray(glimpse, "choices");
                if (choices != null && choices.Length > 0)
                {
                    var ids = new UnitId[choices.Length];
                    for (int i = 0; i < choices.Length; i++)
                        ids[i] = new UnitId(choices[i]);
                    p.GlimpseQueue.Enqueue(new GlimpseOffer { Choices = ids });
                }
            }
            p.Board.Clear();
            List<string> board = Protocol.ExtractObjects(you, "board");
            for (int i = 0; i < board.Count; i++)
                p.Board.Add(ReadUnit(board[i]));
            p.Hand.Clear();
            List<string> hand = Protocol.ExtractObjects(you, "hand");
            for (int i = 0; i < hand.Count; i++)
                p.Hand.Add(ReadUnit(hand[i]));
            p.Stall.Clear();
            List<string> stall = Protocol.ExtractObjects(you, "stall");
            int max = 2;
            for (int i = 0; i < stall.Count; i++)
            {
                int slot = Protocol.ReadInt(stall[i], "slot");
                if (slot > max) max = slot;
            }
            for (int i = 0; i <= max; i++) p.Stall.Add(null);
            for (int i = 0; i < stall.Count; i++)
            {
                int slot = Protocol.ReadInt(stall[i], "slot");
                if (slot >= 0 && slot < p.Stall.Count)
                    p.Stall[slot] = ReadUnit(stall[i]);
            }
        }

        static UnitInstance ReadUnit(string json)
        {
            ulong iid = 0;
            string iidS = Protocol.ReadString(json, "instanceId");
            if (!string.IsNullOrEmpty(iidS))
                ulong.TryParse(iidS, out iid);
            string id = Protocol.ReadString(json, "catalogId");
            if (string.IsNullOrEmpty(id)) id = Protocol.ReadString(json, "id");
            int hp = Protocol.ReadInt(json, "hp");
            var u = new UnitInstance
            {
                InstanceId = iid,
                CatalogId = new UnitId(id),
                Atk = Protocol.ReadInt(json, "atk"),
                Hp = hp,
                MaxHp = hp,
                Keywords = (Keyword)Protocol.ReadInt(json, "kw"),
                Awakened = Protocol.ReadBool(json, "awakened") || Protocol.ReadBool(json, "aw"),
                AttackCharges = 1
            };
            return u;
        }
    }
}
