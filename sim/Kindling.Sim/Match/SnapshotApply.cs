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
            return new UnitInstance
            {
                InstanceId = iid,
                CatalogId = new UnitId(id),
                Atk = Protocol.ReadInt(json, "atk"),
                Hp = hp,
                MaxHp = hp,
                Awakened = Protocol.ReadBool(json, "awakened") || Protocol.ReadBool(json, "aw"),
                AttackCharges = 1
            };
        }
    }
}
