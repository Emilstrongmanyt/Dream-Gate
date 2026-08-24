using Kindling.Sim.Catalog;
using Kindling.Sim.Model;
using Kindling.Sim.Rng;
using Kindling.Sim.Validation;

namespace Kindling.Sim.Bots
{
    public static class HeuristicBot
    {
        public static void PlayRecruit(MatchState m, int seat, Catalog.Catalog cat)
        {
            PlayerState p = m.Seats[seat];
            if (!p.Alive) return;
            if (p.HasFlag(PlayerFlags.GlimpseOpen))
            {
                RecruitValidator.TryApply(m, new RecruitAction
                {
                    Op = RecruitOp.GlimpsePick,
                    Seat = seat,
                    OfferIndex = 0
                }, cat);
            }

            TryEdict(m, seat, cat);
            if (m.Round >= 2)
                TryUpgrade(m, seat, cat);

            int guard = 0;
            while (guard++ < 24)
            {
                if (p.HasFlag(PlayerFlags.GlimpseOpen))
                {
                    RecruitValidator.TryApply(m, new RecruitAction
                    {
                        Op = RecruitOp.GlimpsePick,
                        Seat = seat,
                        OfferIndex = 0
                    }, cat);
                }
                if (PlayOneFromHand(m, seat, cat)) continue;
                if (TryLatch(m, seat, cat)) continue;
                if (TryBuy(m, seat, cat)) continue;
                if (TryUpgrade(m, seat, cat)) continue;
                if (TryReroll(m, seat, cat)) continue;
                break;
            }

            RecruitValidator.TryApply(m, new RecruitAction { Op = RecruitOp.Ready, Seat = seat }, cat);
        }

        static bool TryUpgrade(MatchState m, int seat, Catalog.Catalog cat)
        {
            PlayerState p = m.Seats[seat];
            if (p.Depth >= Rules.MaxDepth) return false;
            if (p.Embers < p.UpgradeCost) return false;
            if (p.UpgradeCost > 0 && p.Embers - p.UpgradeCost < 3 && p.Board.Count < 4 && m.Round < 4)
                return false;
            SimResult r = RecruitValidator.TryApply(m, new RecruitAction { Op = RecruitOp.Upgrade, Seat = seat }, cat);
            return r.Ok;
        }

        static bool PlayOneFromHand(MatchState m, int seat, Catalog.Catalog cat)
        {
            PlayerState p = m.Seats[seat];
            if (p.Hand.Count == 0 || p.Board.Count >= Rules.BoardMax) return false;
            SimResult r = RecruitValidator.TryApply(m, new RecruitAction
            {
                Op = RecruitOp.Play,
                Seat = seat,
                HandIndex = 0,
                DestIndex = p.Board.Count
            }, cat);
            return r.Ok;
        }

        static bool TryLatch(MatchState m, int seat, Catalog.Catalog cat)
        {
            PlayerState p = m.Seats[seat];
            for (int i = 0; i < p.Hand.Count; i++)
            {
                if (!p.Hand[i].Has(Keyword.Latch)) continue;
                int host = FindHost(p, cat, -1);
                if (host < 0) continue;
                SimResult r = RecruitValidator.TryApply(m, new RecruitAction
                {
                    Op = RecruitOp.Latch,
                    Seat = seat,
                    From = DestLoc.Hand,
                    FromIndex = i,
                    HostIndex = host
                }, cat);
                if (r.Ok) return true;
            }
            return false;
        }

        static int FindHost(PlayerState p, Catalog.Catalog cat, int except)
        {
            for (int i = 0; i < p.Board.Count; i++)
            {
                if (i == except) continue;
                UnitDef d = cat.GetUnit(p.Board[i].CatalogId);
                if (d != null && d.Chorus == Chorus.Gearwights) return i;
            }
            return -1;
        }

        static bool TryBuy(MatchState m, int seat, Catalog.Catalog cat)
        {
            PlayerState p = m.Seats[seat];
            if (p.Embers < Rules.BuyCost) return false;
            bool boardSpace = p.Board.Count < Rules.BoardMax;
            bool handSpace = p.Hand.Count < Rules.HandMax;
            if (!boardSpace && !handSpace) return false;
            int best = -1;
            int bestScore = int.MinValue;
            for (int i = 0; i < p.Stall.Count; i++)
            {
                UnitInstance u = p.Stall[i];
                if (u == null) continue;
                int s = Score(p, cat, u);
                int jitter = m.Rng.Range(MatchRng.Stream.Bot, 0, 3);
                s += jitter;
                if (s > bestScore)
                {
                    bestScore = s;
                    best = i;
                }
            }
            if (best < 0) return false;
            DestLoc dest = boardSpace ? DestLoc.Board : DestLoc.Hand;
            SimResult r = RecruitValidator.TryApply(m, new RecruitAction
            {
                Op = RecruitOp.Buy,
                Seat = seat,
                StallIndex = best,
                Dest = dest,
                DestIndex = dest == DestLoc.Board ? p.Board.Count : p.Hand.Count
            }, cat);
            return r.Ok;
        }

        static bool TryReroll(MatchState m, int seat, Catalog.Catalog cat)
        {
            PlayerState p = m.Seats[seat];
            int cost = Recruit.Grant.RerollCostNow(p);
            if (p.Embers < cost + Rules.BuyCost && cost > 0) return false;
            if (p.Embers < cost) return false;
            if (FilledStall(p) >= 2 && p.Embers < cost + 6) return false;
            SimResult r = RecruitValidator.TryApply(m, new RecruitAction { Op = RecruitOp.Reroll, Seat = seat }, cat);
            return r.Ok;
        }

        static int FilledStall(PlayerState p)
        {
            int n = 0;
            for (int i = 0; i < p.Stall.Count; i++)
                if (p.Stall[i] != null) n++;
            return n;
        }

        static void TryEdict(MatchState m, int seat, Catalog.Catalog cat)
        {
            PlayerState p = m.Seats[seat];
            CaptainDef def = cat.GetCaptain(p.Captain);
            if (def == null || !def.HasEdict) return;
            if (p.Embers < def.EdictCost) return;
            if (def.Id.Value == "cap_jun" && p.Wick <= 1) return;
            var a = new RecruitAction
            {
                Op = RecruitOp.Edict,
                Seat = seat,
                TargetIndex = p.Board.Count > 0 ? 0 : -1
            };
            RecruitValidator.TryApply(m, a, cat);
        }

        static int Score(PlayerState p, Catalog.Catalog cat, UnitInstance u)
        {
            UnitDef d = cat.GetUnit(u.CatalogId);
            int s = u.Atk * 2 + u.Hp;
            if (d == null) return s;
            s += d.Depth;
            Chorus commit = MajorityChorus(p, cat);
            if (commit != Chorus.Neutral && d.Chorus == commit) s += 4;
            if (u.Has(Keyword.Ward) || u.Has(Keyword.Venom) || u.Has(Keyword.Aegis) || u.Has(Keyword.Afterglow))
                s += 2;
            int copies = 0;
            for (int i = 0; i < p.Board.Count; i++)
                if (p.Board[i].CatalogId.Equals(u.CatalogId)) copies++;
            for (int i = 0; i < p.Hand.Count; i++)
                if (p.Hand[i].CatalogId.Equals(u.CatalogId)) copies++;
            if (copies == 1 || copies == 2) s += 3;
            return s;
        }

        static Chorus MajorityChorus(PlayerState p, Catalog.Catalog cat)
        {
            int[] c = new int[8];
            void add(UnitInstance u)
            {
                UnitDef d = cat.GetUnit(u.CatalogId);
                if (d != null) c[(int)d.Chorus]++;
            }
            for (int i = 0; i < p.Board.Count; i++) add(p.Board[i]);
            int best = 0;
            Chorus ch = Chorus.Neutral;
            for (int i = 1; i < c.Length; i++)
            {
                if (c[i] > best)
                {
                    best = c[i];
                    ch = (Chorus)i;
                }
            }
            return ch;
        }
    }
}
