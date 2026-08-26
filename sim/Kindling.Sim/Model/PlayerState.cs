using System.Collections.Generic;

namespace Kindling.Sim.Model
{
    public sealed class PlayerState
    {
        public int Seat;
        public int Wick;
        public int Embers;
        public int Depth = 1;
        public int UpgradeCost = 5;
        public bool Hold;
        public int? Place;
        public List<UnitInstance> Board = new List<UnitInstance>(7);
        public List<UnitInstance> Hand = new List<UnitInstance>(10);
        public List<UnitInstance> Stall = new List<UnitInstance>(7);
        public CaptainId Captain;
        public EdictState Edict = new EdictState();
        public PlayerFlags Flags;
        public int PendingEmbers;
        public int DredgerBonus;
        public int StallSizeDelta;
        public int RerollsThisRecruit;
        public int BoughtThisRecruit;
        public int LatchPlaysThisMatch;
        public int RingDamageDealt;
        public int RingDamageTaken;
        public int GlimpseDepthOverride;
        public Queue<GlimpseOffer> GlimpseQueue = new Queue<GlimpseOffer>();
        public bool Ready;
        public bool IsBot;
        public string DisplayName;
        public CaptainId[] CaptainOffers;
        public List<UnitInstance> LastLockedBoard = new List<UnitInstance>();
        public int LastLockedBoardSum;
        public int DepthAtDeath;
        public int PlayerSeq;
        public List<string> Logs = new List<string>();
        public int GhostRingDepth;
        public double Rating = 1500;
        public double Rd = 350;

        public bool Alive => Wick > 0;

        public bool HasFlag(PlayerFlags f) => (Flags & f) != 0;

        public void SetFlag(PlayerFlags f) => Flags |= f;

        public void ClearFlag(PlayerFlags f) => Flags &= ~f;

        public void AddLog(string code)
        {
            if (Logs == null) Logs = new List<string>();
            Logs.Add(code);
        }

        public bool HasLog(string code)
        {
            if (Logs == null) return false;
            for (int i = 0; i < Logs.Count; i++)
            {
                if (Logs[i] == code) return true;
            }
            return false;
        }

        public PlayerState CloneForCombat()
        {
            var c = new PlayerState();
            c.Seat = Seat;
            c.Wick = Wick;
            c.Embers = Embers;
            c.Depth = Depth;
            c.Captain = Captain;
            c.Flags = Flags;
            c.PendingEmbers = PendingEmbers;
            c.StallSizeDelta = StallSizeDelta;
            c.LatchPlaysThisMatch = LatchPlaysThisMatch;
            c.GhostRingDepth = GhostRingDepth;
            c.Board = new List<UnitInstance>(Board.Count);
            for (int i = 0; i < Board.Count; i++)
                c.Board.Add(Board[i].CloneForCombat(Seat));
            return c;
        }

        public UnitInstance FindOwned(ulong instanceId)
        {
            for (int i = 0; i < Board.Count; i++)
            {
                if (Board[i].InstanceId == instanceId) return Board[i];
            }
            for (int i = 0; i < Hand.Count; i++)
            {
                if (Hand[i].InstanceId == instanceId) return Hand[i];
            }
            return null;
        }

        public void SnapshotLock()
        {
            LastLockedBoard = new List<UnitInstance>(Board.Count);
            int sum = 0;
            for (int i = 0; i < Board.Count; i++)
            {
                LastLockedBoard.Add(Board[i].Clone());
                sum += Board[i].Atk + Board[i].Hp;
            }
            LastLockedBoardSum = sum;
        }
    }
}
