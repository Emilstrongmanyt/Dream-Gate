using System;
using System.Collections.Generic;
using Kindling.Sim.Bots;
using Kindling.Sim.Catalog;
using Kindling.Sim.Combat;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Kindling.Sim.Rng;
using Kindling.Sim.Seasons;
using Kindling.Sim.Validation;

namespace Kindling.Sim.Match
{
    public sealed class MatchLoop
    {
        public MatchState State;
        public Catalog.Catalog Cat;
        public List<string> RoundLog = new List<string>();
        public int HumanSeat;
        public CombatResult LastHumanCombat;
        public readonly List<CombatResult> LastRoundCombats = new List<CombatResult>();
        public bool TutorialWickFloor;

        public static MatchLoop Create(Catalog.Catalog cat, Guid matchId, uint salt, int humanSeats = 0)
        {
            var loop = new MatchLoop
            {
                Cat = cat,
                State = new MatchState
                {
                    MatchId = matchId,
                    Salt = salt,
                    Rng = MatchRng.Create(matchId, salt),
                    Season = new SeasonNone(),
                    CatalogVersion = cat.ContentVersion,
                    Phase = Phase.CaptainPick,
                    Round = 0
                }
            };
            MatchState m = loop.State;
            loop.HumanSeat = humanSeats > 0 ? 0 : -1;
            for (int i = 0; i < Rules.LobbySize; i++)
            {
                m.Seats[i].IsBot = i >= humanSeats;
                m.Seats[i].DisplayName = m.Seats[i].IsBot ? ("Bot" + i) : (i == 0 ? "You" : ("Seat" + i));
                m.Seats[i].Wick = Rules.DefaultWick;
                m.Seats[i].Depth = 1;
                m.Seats[i].UpgradeCost = Rules.UpgradeCostBase(1);
            }
            Pool.Init(m, cat);
            m.Season.OnMatchStart(m);
            loop.DealCaptainOffers();
            return loop;
        }

        public static MatchLoop CreateHeadless(Catalog.Catalog cat, ulong fixtureSeed)
        {
            Guid id = new Guid("01234567-89ab-cdef-0123-456789abcdef");
            var loop = Create(cat, id, (uint)(fixtureSeed & 0xffffffffu), 0);
            loop.State.Rng = new MatchRng(fixtureSeed);
            return loop;
        }

        public void DealCaptainOffers()
        {
            MatchState m = State;
            var ids = new List<CaptainId>();
            for (int i = 0; i < Cat.Captains.Count; i++)
                ids.Add(Cat.Captains[i].Id);
            for (int s = 0; s < m.Seats.Length; s++)
            {
                var bag = new List<CaptainId>(ids.Count);
                for (int i = 0; i < ids.Count; i++) bag.Add(ids[i]);
                m.Rng.Shuffle(MatchRng.Stream.CaptainOffer, bag);
                int n = bag.Count < Rules.CaptainOfferCount ? bag.Count : Rules.CaptainOfferCount;
                var offers = new CaptainId[n];
                for (int i = 0; i < n; i++) offers[i] = bag[i];
                m.Seats[s].CaptainOffers = offers;
            }
        }

        /// <summary>Practice: the human picks from the full Captain roster.</summary>
        public void OfferFullRoster()
        {
            if (HumanSeat < 0 || HumanSeat >= State.Seats.Length || Cat.Captains.Count == 0) return;
            var ids = new CaptainId[Cat.Captains.Count];
            for (int i = 0; i < Cat.Captains.Count; i++)
                ids[i] = Cat.Captains[i].Id;
            State.Seats[HumanSeat].CaptainOffers = ids;
        }

        public void RunToEnd()
        {
            AutoCaptainPicks();
            while (State.AliveCount() > 1 && State.Round < Rules.RoundCap)
            {
                RunRound();
            }
            if (State.AliveCount() > 1)
                AssignRoundCapPlaces();
            AssignWinnerIfNeeded();
            State.Phase = Phase.MatchOver;
            State.MatchOver = true;
        }

        public PlayerState Human => HumanSeat >= 0 && HumanSeat < State.Seats.Length
            ? State.Seats[HumanSeat]
            : null;

        public SimResult Try(RecruitAction a)
        {
            return RecruitValidator.TryApply(State, a, Cat);
        }

        /// <summary>Interactive: auto-pick remaining captains, then open Recruit 1.</summary>
        public void StartFromCaptainPick()
        {
            AutoCaptainPicks();
            BeginRecruitPhase();
        }

        /// <summary>Bots pick immediately. Human offers stay until CaptainPick or StartFromCaptainPick.</summary>
        public void AutoPickBotCaptains()
        {
            State.Phase = Phase.CaptainPick;
            for (int i = 0; i < State.Seats.Length; i++)
            {
                PlayerState p = State.Seats[i];
                if (!p.IsBot || !p.Captain.IsEmpty) continue;
                RecruitValidator.TryApply(State, new RecruitAction
                {
                    Op = RecruitOp.CaptainPick,
                    Seat = i,
                    OfferIndex = 0
                }, Cat);
            }
        }

        public void BeginRecruitPhase()
        {
            if (State.MatchOver) return;
            if (State.AliveCount() <= 1)
            {
                AssignWinnerIfNeeded();
                State.Phase = Phase.MatchOver;
                State.MatchOver = true;
                return;
            }
            if (State.Round >= Rules.RoundCap)
            {
                AssignRoundCapPlaces();
                State.Phase = Phase.MatchOver;
                State.MatchOver = true;
                return;
            }

            State.Round++;
            State.Phase = Phase.Recruit;
            PairResult pr = Pairings.Pair(State.LivingSeatsInJoinOrder(), State.Round);
            State.Pairings = pr.Pairs.ToArray();
            State.GhostSeat = pr.GhostSeat;
            LastHumanCombat = null;
            LastRoundCombats.Clear();

            for (int i = 0; i < State.Seats.Length; i++)
            {
                PlayerState p = State.Seats[i];
                if (!p.Alive) continue;
                p.Ready = false;
                Grant.RecruitStart(State, p, Cat);
            }
        }

        /// <summary>Human Ready: bots recruit, all RecruitEnd, combat, placement, next recruit or match over.</summary>
        public void ResolveRecruitAndCombat()
        {
            if (State.Phase != Phase.Recruit) return;

            for (int i = 0; i < State.Seats.Length; i++)
            {
                PlayerState p = State.Seats[i];
                if (!p.Alive) continue;
                if (p.IsBot)
                    HeuristicBot.PlayRecruit(State, i, Cat);
                if (p.HasFlag(PlayerFlags.GlimpseOpen))
                    Glimpse.DrainQueue(State, p, Cat, autoPick: true);
                p.Ready = true;
                p.SnapshotLock();
            }

            for (int i = 0; i < State.Seats.Length; i++)
            {
                PlayerState p = State.Seats[i];
                if (!p.Alive) continue;
                Grant.RecruitEnd(State, p, Cat);
            }

            State.Phase = Phase.Combat;
            RunCombatRound();
            CaptureHumanCombat();
            State.Phase = Phase.Placement;
            PlaceNewlyDead();
            LogRound();

            if (State.AliveCount() <= 1 || State.Round >= Rules.RoundCap)
            {
                if (State.AliveCount() > 1)
                    AssignRoundCapPlaces();
                AssignWinnerIfNeeded();
                State.Phase = Phase.MatchOver;
                State.MatchOver = true;
            }
        }

        public void ContinueToNextRecruit()
        {
            if (State.MatchOver) return;
            if (State.Phase != Phase.Placement && State.Phase != Phase.Combat) return;
            BeginRecruitPhase();
        }

        void CaptureHumanCombat()
        {
            LastHumanCombat = null;
            if (HumanSeat < 0) return;
            for (int i = 0; i < LastRoundCombats.Count; i++)
            {
                CombatResult cr = LastRoundCombats[i];
                if (cr == null) continue;
                if (cr.WinnerSeat == HumanSeat) { LastHumanCombat = cr; return; }
                for (int e = 0; e < cr.Events.Count; e++)
                {
                    if (cr.Events[e].SrcSeat == HumanSeat || cr.Events[e].DstSeat == HumanSeat)
                    {
                        LastHumanCombat = cr;
                        return;
                    }
                }
            }
        }

        public void AutoCaptainPicks()
        {
            State.Phase = Phase.CaptainPick;
            for (int i = 0; i < State.Seats.Length; i++)
            {
                PlayerState p = State.Seats[i];
                if (!p.Captain.IsEmpty) continue;
                var a = new RecruitAction
                {
                    Op = RecruitOp.CaptainPick,
                    Seat = i,
                    OfferIndex = 0
                };
                RecruitValidator.TryApply(State, a, Cat);
            }
            State.Phase = Phase.Recruit;
        }

        public void RunRound()
        {
            State.Round++;
            State.Phase = Phase.Recruit;
            PairResult pr = Pairings.Pair(State.LivingSeatsInJoinOrder(), State.Round);
            State.Pairings = pr.Pairs.ToArray();
            State.GhostSeat = pr.GhostSeat;

            for (int i = 0; i < State.Seats.Length; i++)
            {
                PlayerState p = State.Seats[i];
                if (!p.Alive) continue;
                Grant.RecruitStart(State, p, Cat);
            }

            for (int i = 0; i < State.Seats.Length; i++)
            {
                PlayerState p = State.Seats[i];
                if (!p.Alive) continue;
                if (p.IsBot)
                    HeuristicBot.PlayRecruit(State, i, Cat);
                if (!p.Ready && !p.HasFlag(PlayerFlags.GlimpseOpen))
                    p.Ready = true;
                if (p.HasFlag(PlayerFlags.GlimpseOpen))
                {
                    Glimpse.DrainQueue(State, p, Cat, autoPick: true);
                    p.Ready = true;
                }
            }

            for (int i = 0; i < State.Seats.Length; i++)
            {
                PlayerState p = State.Seats[i];
                if (!p.Alive) continue;
                Grant.RecruitEnd(State, p, Cat);
            }

            State.Phase = Phase.Combat;
            RunCombatRound();
            State.Phase = Phase.Placement;
            PlaceNewlyDead();
            LogRound();
        }

        public void RunCombatRound()
        {
            LastRoundCombats.Clear();
            for (int i = 0; i < State.Pairings.Length; i++)
            {
                Pairing pair = State.Pairings[i];
                PlayerState a = State.Seats[pair.SeatA];
                PlayerState b = State.Seats[pair.SeatB];
                CombatResult cr = CombatSim.Run(State, a, b, State.Rng, Cat);
                ApplyCombatResult(a, b, cr);
                LastRoundCombats.Add(cr);
            }
            if (State.GhostSeat.HasValue)
            {
                int gs = State.GhostSeat.Value;
                PlayerState living = State.Seats[gs];
                if (living.Alive)
                {
                    PlayerState ghost = BuildGhost();
                    CombatResult cr = CombatSim.Run(State, living, ghost, State.Rng, Cat);
                    ApplyGhostResult(living, cr);
                    LastRoundCombats.Add(cr);
                }
            }
        }

        PlayerState BuildGhost()
        {
            PlayerState src = FindGhostSource();
            var ghost = new PlayerState
            {
                Seat = -1,
                Wick = int.MaxValue,
                Depth = src != null ? (src.DepthAtDeath > 0 ? src.DepthAtDeath : src.Depth) : 1,
                GhostRingDepth = src != null ? (src.DepthAtDeath > 0 ? src.DepthAtDeath : src.Depth) : 1,
                DisplayName = "Ash Echo"
            };
            if (src != null && src.LastLockedBoard != null && src.LastLockedBoard.Count > 0)
            {
                for (int i = 0; i < src.LastLockedBoard.Count; i++)
                    ghost.Board.Add(src.LastLockedBoard[i].Clone());
            }
            else
            {
                UnitId dummy = new UnitId("tok_dummy");
                for (int i = 0; i < Rules.DummyGhostCount; i++)
                    ghost.Board.Add(Units.Create(Cat, State.Rng, dummy));
            }
            return ghost;
        }

        PlayerState FindGhostSource()
        {
            int bestRound = -1;
            int bestPlace = -1;
            int bestSeat = int.MaxValue;
            PlayerState best = null;
            for (int i = 0; i < State.Seats.Length; i++)
            {
                PlayerState p = State.Seats[i];
                if (p.Alive || !p.Place.HasValue) continue;
                int rnd = 0;
                for (int e = 0; e < State.EliminationOrder.Count; e++)
                {
                    if (State.EliminationOrder[e] == i) { rnd = e; break; }
                }
                int place = p.Place.Value;
                bool better = false;
                if (best == null) better = true;
                else if (rnd > bestRound) better = true;
                else if (rnd == bestRound && place > bestPlace) better = true;
                else if (rnd == bestRound && place == bestPlace && i < bestSeat) better = true;
                if (better)
                {
                    best = p;
                    bestRound = rnd;
                    bestPlace = place;
                    bestSeat = i;
                }
            }
            return best;
        }

        void ApplyCombatResult(PlayerState a, PlayerState b, CombatResult cr)
        {
            if (cr.Draw) return;
            PlayerState winner = cr.WinnerSeat == a.Seat ? a : b;
            PlayerState loser = cr.WinnerSeat == a.Seat ? b : a;
            loser.Wick -= cr.Damage;
            loser.RingDamageTaken += cr.Damage;
            winner.RingDamageDealt += cr.Damage;
        }

        void ApplyGhostResult(PlayerState living, CombatResult cr)
        {
            if (cr.Draw) return;
            if (cr.WinnerSeat != living.Seat)
            {
                living.Wick -= cr.Damage;
                living.RingDamageTaken += cr.Damage;
            }
            else
            {
                living.RingDamageDealt += cr.Damage;
            }
        }

        public void PlaceNewlyDead()
        {
            if (TutorialWickFloor && HumanSeat >= 0 && HumanSeat < State.Seats.Length)
            {
                PlayerState h = State.Seats[HumanSeat];
                if (h.Wick <= 0 && !h.Place.HasValue)
                    h.Wick = 1;
            }
            var newly = new List<PlayerState>();
            for (int i = 0; i < State.Seats.Length; i++)
            {
                PlayerState p = State.Seats[i];
                if (!p.Place.HasValue && p.Wick <= 0)
                {
                    p.DepthAtDeath = p.Depth;
                    newly.Add(p);
                }
            }
            if (newly.Count == 0)
            {
                if (State.AliveCount() == 1)
                    AssignWinnerIfNeeded();
                return;
            }
            newly.Sort((x, y) =>
            {
                int c = x.Wick.CompareTo(y.Wick);
                if (c != 0) return c;
                c = y.RingDamageTaken.CompareTo(x.RingDamageTaken);
                if (c != 0) return c;
                c = x.RingDamageDealt.CompareTo(y.RingDamageDealt);
                if (c != 0) return c;
                int rx = State.Rng.Range(MatchRng.Stream.TieBreak, 0, int.MaxValue);
                int ry = State.Rng.Range(MatchRng.Stream.TieBreak, 0, int.MaxValue);
                return rx.CompareTo(ry);
            });
            int livingAfter = 0;
            for (int i = 0; i < State.Seats.Length; i++)
            {
                if (State.Seats[i].Wick > 0) livingAfter++;
            }
            for (int i = 0; i < newly.Count; i++)
            {
                newly[i].Place = livingAfter + (newly.Count - i);
                State.EliminationOrder.Add(newly[i].Seat);
            }
            AssignWinnerIfNeeded();
        }

        void AssignWinnerIfNeeded()
        {
            int living = 0;
            int seat = -1;
            for (int i = 0; i < State.Seats.Length; i++)
            {
                if (State.Seats[i].Alive && !State.Seats[i].Place.HasValue)
                {
                    living++;
                    seat = i;
                }
            }
            if (living == 1 && seat >= 0)
                State.Seats[seat].Place = 1;
        }

        public void AssignRoundCapPlaces()
        {
            var living = new List<PlayerState>();
            for (int i = 0; i < State.Seats.Length; i++)
            {
                if (State.Seats[i].Alive && !State.Seats[i].Place.HasValue)
                    living.Add(State.Seats[i]);
            }
            living.Sort((x, y) =>
            {
                int c = y.Wick.CompareTo(x.Wick);
                if (c != 0) return c;
                c = y.RingDamageDealt.CompareTo(x.RingDamageDealt);
                if (c != 0) return c;
                c = y.LastLockedBoardSum.CompareTo(x.LastLockedBoardSum);
                if (c != 0) return c;
                int rx = State.Rng.Range(MatchRng.Stream.TieBreak, 0, int.MaxValue);
                int ry = State.Rng.Range(MatchRng.Stream.TieBreak, 0, int.MaxValue);
                return rx.CompareTo(ry);
            });
            for (int i = 0; i < living.Count; i++)
                living[i].Place = i + 1;
        }

        void LogRound()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("R").Append(State.Round).Append(':');
            for (int i = 0; i < State.Seats.Length; i++)
            {
                PlayerState p = State.Seats[i];
                sb.Append(' ').Append(p.DisplayName).Append('=');
                sb.Append(p.Wick);
                if (p.Place.HasValue) sb.Append('#').Append(p.Place.Value);
            }
            RoundLog.Add(sb.ToString());
        }
    }
}
