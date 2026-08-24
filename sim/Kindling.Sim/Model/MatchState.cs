using System;
using System.Collections.Generic;
using Kindling.Sim.Rng;
using Kindling.Sim.Seasons;

namespace Kindling.Sim.Model
{
    public sealed class MatchState
    {
        public Guid MatchId;
        public uint Salt;
        public MatchRng Rng;
        public string CatalogVersion = "0.1.0";
        public ISeasonModule Season;
        public int Round;
        public Phase Phase = Phase.CaptainPick;
        public List<PoolEntry> Pool = new List<PoolEntry>();
        public PlayerState[] Seats = new PlayerState[Rules.LobbySize];
        public Pairing[] Pairings = Array.Empty<Pairing>();
        public int? GhostSeat;
        public List<int> EliminationOrder = new List<int>();
        public int Seq;
        public int AwakenEvents;
        public int ShopLatchDestroyed;
        public int GlimpseOverflowGrants;
        public int MirrorGrants;
        public int AddToHandFromPoolOverflow;
        public int TokenSpawned;
        public int TokenDestroyed;
        public bool MatchOver;
        public List<string> Logs = new List<string>();
        public int DrainReentryAttempts;
        public UnitInstance BoughtUnit;
        public UnitInstance LatchHost;
        public int CombatLifetimeSummons;
        public bool InCombat;

        public MatchState()
        {
            for (int i = 0; i < Rules.LobbySize; i++)
            {
                Seats[i] = new PlayerState
                {
                    Seat = i,
                    Wick = Rules.DefaultWick,
                    Depth = 1,
                    UpgradeCost = Rules.UpgradeCostBase(1),
                    DisplayName = "Seat" + i
                };
            }
        }

        public int AliveCount()
        {
            int n = 0;
            for (int i = 0; i < Seats.Length; i++)
            {
                if (Seats[i] != null && Seats[i].Alive) n++;
            }
            return n;
        }

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

        public PoolEntry GetPool(UnitId id)
        {
            for (int i = 0; i < Pool.Count; i++)
            {
                if (Pool[i].Id.Equals(id)) return Pool[i];
            }
            return null;
        }

        public void SortPool()
        {
            Pool.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        public List<int> LivingSeatsInJoinOrder()
        {
            var list = new List<int>(8);
            for (int i = 0; i < Seats.Length; i++)
            {
                if (Seats[i] != null && Seats[i].Alive) list.Add(i);
            }
            return list;
        }
    }
}
