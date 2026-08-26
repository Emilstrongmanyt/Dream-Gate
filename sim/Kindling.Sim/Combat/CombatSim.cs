using System.Collections.Generic;
using Kindling.Sim.Captains;
using Kindling.Sim.Catalog;
using Kindling.Sim.Effects;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Kindling.Sim.Rng;

namespace Kindling.Sim.Combat
{
    public sealed class CombatRuntime
    {
        public MatchState Match;
        public Catalog.Catalog Cat;
        public MatchRng Rng;
        public PlayerState A;
        public PlayerState B;
        public PlayerState OrigA;
        public PlayerState OrigB;
        public PlayerState FirstP;
        public PlayerState SecondP;
        public List<UnitInstance> First;
        public List<UnitInstance> Second;
        public int LifetimeSummons;
        public int DeathWaves;
        public bool InDrain;
        public int DrainReentryAttempts;
        public List<GraveRecord> Graveyard = new List<GraveRecord>();
        public CombatResult Result = new CombatResult();
        public int Step;
        public bool Truncated;

        public PlayerState OrigOf(PlayerState p)
        {
            if (p == null) return null;
            if (p.Seat == OrigA.Seat) return OrigA;
            if (p.Seat == OrigB.Seat) return OrigB;
            return p;
        }

        public PlayerState OpponentOf(PlayerState p)
        {
            if (p == null) return null;
            return p.Seat == A.Seat ? B : A;
        }

        public void Log(CombatOp op, int srcSeat, int dstSeat, ulong srcInst, ulong dstInst, int srcSlot, int dstSlot, int amount, int atk, int hpAfter, string catalogId, string note)
        {
            Result.Events.Add(new CombatEvent
            {
                Step = Step++,
                Op = op,
                SrcSeat = srcSeat,
                DstSeat = dstSeat,
                SrcInstance = srcInst,
                DstInstance = dstInst,
                SrcSlot = srcSlot,
                DstSlot = dstSlot,
                Amount = amount,
                Atk = atk,
                HpAfter = hpAfter,
                CatalogId = catalogId,
                Note = note
            });
        }

        public bool TrySummon(PlayerState owner, ActionDef act)
        {
            if (owner.Board.Count >= Rules.BoardMax)
            {
                Log(CombatOp.BoardFull, owner.Seat, owner.Seat, 0, 0, -1, -1, 0, 0, 0, act.Unit, "BoardFull");
                return false;
            }
            if (LifetimeSummons >= Rules.LifetimeSummonCap)
            {
                Log(CombatOp.Truncated, owner.Seat, owner.Seat, 0, 0, -1, -1, 0, 0, 0, act.Unit, "Truncated");
                Truncated = true;
                return false;
            }
            UnitInstance neu = Units.Create(Cat, Rng, new UnitId(act.Unit));
            neu.CombatSeat = owner.Seat;
            if (act.AtkSpecified) neu.Atk = act.Atk;
            if (act.HpSpecified) { neu.Hp = act.Hp; neu.MaxHp = act.Hp; }
            owner.Board.Add(neu);
            LifetimeSummons++;
            UnitDef d = Cat.GetUnit(neu.CatalogId);
            if (d != null && d.Token && Match != null) Match.TokenSpawned++;
            Log(CombatOp.Summon, owner.Seat, owner.Seat, neu.InstanceId, 0, owner.Board.Count - 1, -1, 0, neu.Atk, neu.Hp, neu.CatalogId.Value, null);
            if (act.FireArrival)
                EffectHooks.FireCombat(this, Trigger.Arrival, neu, owner);
            return true;
        }

        public void SummonFromGraveyard(PlayerState owner, ActionDef act, UnitInstance self)
        {
            var cand = new List<GraveRecord>();
            for (int i = 0; i < Graveyard.Count; i++)
            {
                GraveRecord g = Graveyard[i];
                if (g.Seat != owner.Seat) continue;
                if (self != null && g.Snapshot.InstanceId == self.InstanceId) continue;
                bool hasEcho = g.HasEcho;
                if (act.HasEcho && !hasEcho) continue;
                cand.Add(g);
            }
            int want = act.CountSpecified ? act.Count : 1;
            for (int n = 0; n < want; n++)
            {
                if (cand.Count == 0) break;
                int r = Rng.Range(MatchRng.Stream.Combat, 0, cand.Count);
                GraveRecord g = cand[r];
                cand.RemoveAt(r);
                var summonAct = new ActionDef
                {
                    Type = ActionType.Summon,
                    Unit = g.Snapshot.CatalogId.Value,
                    Count = 1,
                    Atk = act.AtkSpecified ? act.Atk : 1,
                    Hp = act.HpSpecified ? act.Hp : 1,
                    AtkSpecified = true,
                    HpSpecified = true
                };
                TrySummon(owner, summonAct);
            }
        }

        public void ApplyEffectDamage(UnitInstance source, UnitInstance target, int amount)
        {
            if (amount <= 0 || target == null) return;
            CombatSim.ApplyDamage(this, source, target, amount);
        }
    }

    public static class CombatSim
    {
        public static CombatResult Run(PlayerState a, PlayerState b, MatchRng rng, Catalog.Catalog cat)
        {
            return Run(null, a, b, rng, cat);
        }

        public static CombatResult Run(MatchState match, PlayerState origA, PlayerState origB, MatchRng rng, Catalog.Catalog cat)
        {
            var rt = new CombatRuntime
            {
                Match = match,
                Cat = cat,
                Rng = rng,
                OrigA = origA,
                OrigB = origB,
                A = origA.CloneForCombat(),
                B = origB.CloneForCombat()
            };
            if (match != null) match.InCombat = true;
            PlayerState pa = rt.A;
            PlayerState pb = rt.B;
            Dense(pa.Board);
            Dense(pb.Board);

            if (pa.Board.Count > pb.Board.Count)
            {
                rt.FirstP = pa; rt.SecondP = pb;
            }
            else if (pb.Board.Count > pa.Board.Count)
            {
                rt.FirstP = pb; rt.SecondP = pa;
            }
            else
            {
                if (rng.Bit(MatchRng.Stream.Combat))
                {
                    rt.FirstP = pa; rt.SecondP = pb;
                }
                else
                {
                    rt.FirstP = pb; rt.SecondP = pa;
                }
            }
            rt.First = rt.FirstP.Board;
            rt.Second = rt.SecondP.Board;
            CaptureOpening(rt.Result, origA, origB, rt.A, rt.B);

            AuraRefresh(rt);
            rt.Log(CombatOp.KindleStart, rt.FirstP.Seat, rt.SecondP.Seat, 0, 0, 0, 0, 0, 0, 0, null, "first");
            KindleSide(rt, rt.FirstP);
            DrainDeaths(rt);
            AuraRefresh(rt);
            KindleSide(rt, rt.SecondP);
            DrainDeaths(rt);
            AuraRefresh(rt);

            PlayerState attackerP = rt.FirstP;
            PlayerState defenderP = rt.SecondP;
            int guard = 0;
            while (pa.Board.Count > 0 && pb.Board.Count > 0 && guard++ < 256)
            {
                UnitInstance atk = LeftmostEligible(attackerP.Board);
                if (atk == null)
                {
                    atk = LeftmostEligible(defenderP.Board);
                    if (atk == null) break;
                    PlayerState tmp = attackerP;
                    attackerP = defenderP;
                    defenderP = tmp;
                }
                List<UnitInstance> valid = WardsOrAll(defenderP.Board);
                if (valid.Count == 0) break;
                int ti = rng.Range(MatchRng.Stream.Combat, 0, valid.Count);
                UnitInstance target = valid[ti];
                ResolveAttack(rt, atk, target, attackerP, defenderP);
                atk.AttacksThisCombat++;
                DrainDeaths(rt);
                AuraRefresh(rt);
                PlayerState sw = attackerP;
                attackerP = defenderP;
                defenderP = sw;
            }

            CombatResult res = rt.Result;
            res.RemainingA = pa.Board.Count;
            res.RemainingB = pb.Board.Count;
            if (pa.Board.Count == 0 && pb.Board.Count == 0)
            {
                res.Draw = true;
                res.WinnerSeat = -1;
                res.Damage = 0;
            }
            else if (pa.Board.Count == 0)
            {
                res.WinnerSeat = origB.Seat;
                res.Damage = RingDamage(pb, cat);
            }
            else if (pb.Board.Count == 0)
            {
                res.WinnerSeat = origA.Seat;
                res.Damage = RingDamage(pa, cat);
            }
            else
            {
                res.Draw = true;
                res.WinnerSeat = -1;
                res.Damage = 0;
            }
            rt.Log(CombatOp.CombatEnd, origA.Seat, origB.Seat, 0, 0, 0, 0, res.Damage, 0, 0, null, res.Draw ? "Draw" : "Win");
            if (match != null)
            {
                match.InCombat = false;
                match.DrainReentryAttempts += rt.DrainReentryAttempts;
            }
            return res;
        }

        static void Dense(List<UnitInstance> board)
        {
            for (int i = board.Count - 1; i >= 0; i--)
            {
                if (board[i] == null) board.RemoveAt(i);
            }
        }

        static UnitInstance LeftmostEligible(List<UnitInstance> side)
        {
            for (int i = 0; i < side.Count; i++)
            {
                UnitInstance u = side[i];
                if (u.EffectiveAtk > 0 && u.AttacksThisCombat < u.AttackCharges && u.Hp > 0 && !u.DeathProcessed)
                    return u;
            }
            return null;
        }

        static List<UnitInstance> WardsOrAll(List<UnitInstance> side)
        {
            var wards = new List<UnitInstance>();
            var all = new List<UnitInstance>();
            for (int i = 0; i < side.Count; i++)
            {
                UnitInstance u = side[i];
                if (u.Hp <= 0 || u.DeathProcessed) continue;
                all.Add(u);
                if (u.Has(Keyword.Ward)) wards.Add(u);
            }
            return wards.Count > 0 ? wards : all;
        }

        static int RingDamage(PlayerState winner, Catalog.Catalog cat)
        {
            int d = winner.Depth;
            if (winner.GhostRingDepth > 0) d = winner.GhostRingDepth;
            for (int i = 0; i < winner.Board.Count; i++)
            {
                UnitDef def = cat.GetUnit(winner.Board[i].CatalogId);
                d += Units.RingDepth(def);
            }
            return d;
        }

        static void KindleSide(CombatRuntime rt, PlayerState p)
        {
            CaptainPassives.OnKindle(p, rt.Cat, rt);
            var snap = new List<UnitInstance>(p.Board.Count);
            for (int i = 0; i < p.Board.Count; i++) snap.Add(p.Board[i]);
            for (int i = 0; i < snap.Count; i++)
            {
                UnitInstance u = snap[i];
                if (!StillOn(p.Board, u)) continue;
                UnitDef def = rt.Cat.GetUnit(u.CatalogId);
                if (!u.HasKindle(def)) continue;
                rt.Log(CombatOp.Kindle, p.Seat, p.Seat, u.InstanceId, 0, IndexOf(p.Board, u), -1, 0, u.EffectiveAtk, u.Hp, u.CatalogId.Value, null);
                EffectHooks.FireCombat(rt, Trigger.Kindle, u, p);
            }
        }

        static void ResolveAttack(CombatRuntime rt, UnitInstance atk, UnitInstance target, PlayerState attackerP, PlayerState defenderP)
        {
            rt.Log(CombatOp.Attack, attackerP.Seat, defenderP.Seat, atk.InstanceId, target.InstanceId,
                IndexOf(attackerP.Board, atk), IndexOf(defenderP.Board, target), atk.EffectiveAtk, atk.EffectiveAtk, target.Hp, atk.CatalogId.Value, null);
            EffectHooks.FireCombat(rt, Trigger.OnAttack, atk, attackerP);
            ApplyDamage(rt, atk, target, atk.EffectiveAtk);
            ApplyDamage(rt, target, atk, target.EffectiveAtk);
        }

        public static void ApplyDamage(CombatRuntime rt, UnitInstance source, UnitInstance target, int amount)
        {
            if (amount <= 0 || target == null || target.DeathProcessed) return;
            if (target.Has(Keyword.Aegis))
            {
                target.RemoveKeyword(Keyword.Aegis);
                rt.Log(CombatOp.AegisBreak, source != null ? source.CombatSeat : -1, target.CombatSeat,
                    source != null ? source.InstanceId : 0, target.InstanceId, -1, -1, 0, 0, target.Hp, target.CatalogId.Value, null);
                return;
            }
            bool wasAlive = target.Hp > 0;
            target.Hp -= amount;
            PlayerState srcP = OwnerOf(rt, source);
            PlayerState dstP = OwnerOf(rt, target);
            rt.Log(CombatOp.Damage, srcP != null ? srcP.Seat : -1, dstP != null ? dstP.Seat : -1,
                source != null ? source.InstanceId : 0, target.InstanceId, -1, -1, amount, source != null ? source.EffectiveAtk : 0, target.Hp, target.CatalogId.Value, null);
            if (source != null && source.Has(Keyword.Venom) && amount > 0)
            {
                target.Hp = 0;
                rt.Log(CombatOp.Venom, srcP.Seat, dstP.Seat, source.InstanceId, target.InstanceId, -1, -1, 0, 0, 0, source.CatalogId.Value, "VenomKill");
                if (srcP != null)
                    EffectHooks.FireCombat(rt, Trigger.OnVenomKill, source, srcP);
            }
            if (srcP != null && source != null)
                EffectHooks.FireCombat(rt, Trigger.OnDamageDealt, source, srcP);
            if (dstP != null)
                EffectHooks.FireCombat(rt, Trigger.OnDamaged, target, dstP);
            if (wasAlive && target.Hp <= 0 && srcP != null && source != null)
                EffectHooks.FireCombat(rt, Trigger.OnKill, source, srcP);
        }

        static PlayerState OwnerOf(CombatRuntime rt, UnitInstance u)
        {
            if (u == null) return null;
            if (u.CombatSeat == rt.A.Seat) return rt.A;
            if (u.CombatSeat == rt.B.Seat) return rt.B;
            for (int i = 0; i < rt.A.Board.Count; i++)
                if (ReferenceEquals(rt.A.Board[i], u)) return rt.A;
            for (int i = 0; i < rt.B.Board.Count; i++)
                if (ReferenceEquals(rt.B.Board[i], u)) return rt.B;
            return null;
        }

        static bool StillOn(List<UnitInstance> board, UnitInstance u)
        {
            for (int i = 0; i < board.Count; i++)
            {
                if (ReferenceEquals(board[i], u) || board[i].InstanceId == u.InstanceId)
                    return board[i].Hp > 0 && !board[i].DeathProcessed;
            }
            return false;
        }

        static int IndexOf(List<UnitInstance> board, UnitInstance u)
        {
            for (int i = 0; i < board.Count; i++)
            {
                if (ReferenceEquals(board[i], u) || (u != null && board[i].InstanceId == u.InstanceId))
                    return i;
            }
            return -1;
        }

        public static void DrainDeaths(CombatRuntime rt)
        {
            if (rt.InDrain)
            {
                rt.DrainReentryAttempts++;
                if (rt.Match != null) rt.Match.DrainReentryAttempts++;
                return;
            }
            rt.InDrain = true;
            try
            {
                while (true)
                {
                    var dying = CollectDying(rt);
                    if (dying.Count == 0) return;
                    rt.DeathWaves++;
                    if (rt.DeathWaves > Rules.DeathWaveCap)
                    {
                        rt.Log(CombatOp.Truncated, -1, -1, 0, 0, 0, 0, 0, 0, 0, null, "DeathWaves");
                        MarkProcessed(rt.A.Board);
                        MarkProcessed(rt.B.Board);
                        Compact(rt.A.Board);
                        Compact(rt.B.Board);
                        return;
                    }
                    var afterglow = new List<AfterglowRec>();
                    for (int i = 0; i < dying.Count; i++)
                    {
                        DyingRec d = dying[i];
                        d.Unit.DeathProcessed = true;
                        UnitDef def = rt.Cat.GetUnit(d.Unit.CatalogId);
                        rt.Graveyard.Add(new GraveRecord
                        {
                            Snapshot = d.Unit.Clone(),
                            Seat = d.Owner.Seat,
                            Order = rt.Graveyard.Count,
                            HasEcho = d.Unit.HasEcho(def)
                        });
                        if (d.Unit.Has(Keyword.Afterglow) && !d.Unit.AfterglowConsumed)
                        {
                            afterglow.Add(new AfterglowRec
                            {
                                Owner = d.Owner,
                                Slot = d.Slot,
                                Unit = d.Unit
                            });
                        }
                        rt.Log(CombatOp.Death, d.Owner.Seat, d.Owner.Seat, d.Unit.InstanceId, 0, d.Slot, -1, 0, d.Unit.Atk, 0, d.Unit.CatalogId.Value, null);
                        RemoveByRef(d.Owner.Board, d.Unit);
                    }
                    Compact(rt.A.Board);
                    Compact(rt.B.Board);

                    for (int i = 0; i < dying.Count; i++)
                    {
                        DyingRec d = dying[i];
                        UnitDef def = rt.Cat.GetUnit(d.Unit.CatalogId);
                        if (!d.Unit.HasEcho(def)) continue;
                        rt.Log(CombatOp.Echo, d.Owner.Seat, d.Owner.Seat, d.Unit.InstanceId, 0, -1, -1, 0, 0, 0, d.Unit.CatalogId.Value, null);
                        EffectHooks.FireCombat(rt, Trigger.Echo, d.Unit, d.Owner);
                    }

                    for (int i = 0; i < afterglow.Count; i++)
                    {
                        AfterglowRec rec = afterglow[i];
                        if (rec.Owner.Board.Count >= Rules.BoardMax)
                        {
                            rt.Log(CombatOp.BoardFull, rec.Owner.Seat, rec.Owner.Seat, rec.Unit.InstanceId, 0, rec.Slot, -1, 0, 0, 0, rec.Unit.CatalogId.Value, "Afterglow");
                            continue;
                        }
                        UnitInstance neu = Units.Create(rt.Cat, rt.Rng, rec.Unit.CatalogId);
                        neu.Atk = 1;
                        neu.Hp = 1;
                        neu.MaxHp = 1;
                        neu.Keywords = Keyword.None;
                        UnitDef def = rt.Cat.GetUnit(rec.Unit.CatalogId);
                        if (def != null && def.AfterglowKeepsKeywords)
                            neu.Keywords = rec.Unit.Keywords & ~Keyword.Afterglow;
                        neu.AfterglowConsumed = true;
                        neu.AttacksThisCombat = 0;
                        neu.AttackCharges = 1;
                        neu.Cinders = 0;
                        neu.Latches.Clear();
                        neu.CombatSeat = rec.Owner.Seat;
                        int ins = rec.Slot;
                        if (ins > rec.Owner.Board.Count) ins = rec.Owner.Board.Count;
                        if (ins < 0) ins = 0;
                        rec.Owner.Board.Insert(ins, neu);
                        rt.Log(CombatOp.Afterglow, rec.Owner.Seat, rec.Owner.Seat, neu.InstanceId, rec.Unit.InstanceId, ins, rec.Slot, 0, 1, 1, neu.CatalogId.Value, null);
                    }
                }
            }
            finally
            {
                rt.InDrain = false;
            }
        }

        struct DyingRec
        {
            public UnitInstance Unit;
            public PlayerState Owner;
            public int Slot;
            public bool IsFirst;
        }

        struct AfterglowRec
        {
            public PlayerState Owner;
            public int Slot;
            public UnitInstance Unit;
        }

        static List<DyingRec> CollectDying(CombatRuntime rt)
        {
            var list = new List<DyingRec>();
            CollectSide(list, rt.FirstP, true);
            CollectSide(list, rt.SecondP, false);
            list.Sort((a, b) =>
            {
                int c = (a.IsFirst ? 0 : 1).CompareTo(b.IsFirst ? 0 : 1);
                if (c != 0) return c;
                return a.Slot.CompareTo(b.Slot);
            });
            return list;
        }

        static void CollectSide(List<DyingRec> list, PlayerState p, bool isFirst)
        {
            for (int i = 0; i < p.Board.Count; i++)
            {
                UnitInstance u = p.Board[i];
                if (u.Hp <= 0 && !u.DeathProcessed)
                {
                    list.Add(new DyingRec { Unit = u, Owner = p, Slot = i, IsFirst = isFirst });
                }
            }
        }

        static void Compact(List<UnitInstance> board)
        {
            for (int i = board.Count - 1; i >= 0; i--)
            {
                if (board[i] == null || (board[i].Hp <= 0 && board[i].DeathProcessed))
                    board.RemoveAt(i);
            }
        }

        static void RemoveByRef(List<UnitInstance> board, UnitInstance u)
        {
            for (int i = 0; i < board.Count; i++)
            {
                if (ReferenceEquals(board[i], u) || board[i].InstanceId == u.InstanceId)
                {
                    board.RemoveAt(i);
                    return;
                }
            }
        }

        static void MarkProcessed(List<UnitInstance> board)
        {
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Hp <= 0) board[i].DeathProcessed = true;
            }
        }

        static void CaptureOpening(CombatResult res, PlayerState origA, PlayerState origB, PlayerState a, PlayerState b)
        {
            res.SeatA = origA.Seat;
            res.SeatB = origB.Seat;
            res.NameA = origA.DisplayName ?? "";
            res.NameB = origB.DisplayName ?? "";
            res.DepthA = origA.Depth;
            res.DepthB = origB.GhostRingDepth > 0 ? origB.GhostRingDepth : origB.Depth;
            res.WickA = origA.Wick;
            res.WickB = origB.Wick >= 100000 ? 0 : origB.Wick;
            res.BoardA = SnapBoard(a.Board, origA.Seat);
            res.BoardB = SnapBoard(b.Board, origB.Seat);
        }

        static System.Collections.Generic.List<CombatPiece> SnapBoard(System.Collections.Generic.List<UnitInstance> board, int seat)
        {
            var list = new System.Collections.Generic.List<CombatPiece>(board.Count);
            for (int i = 0; i < board.Count; i++)
            {
                UnitInstance u = board[i];
                list.Add(new CombatPiece
                {
                    InstanceId = u.InstanceId,
                    CatalogId = u.CatalogId,
                    Atk = u.EffectiveAtk,
                    Hp = u.Hp,
                    MaxHp = u.MaxHp,
                    Keywords = u.Keywords,
                    Awakened = u.Awakened,
                    Seat = seat
                });
            }
            return list;
        }

        static void AuraRefresh(CombatRuntime rt)
        {
            StripSide(rt.A.Board);
            StripSide(rt.B.Board);
            ApplyAuras(rt, rt.A);
            ApplyAuras(rt, rt.B);
            rt.Log(CombatOp.AuraRefresh, rt.A.Seat, rt.B.Seat, 0, 0, 0, 0, 0, 0, 0, null, null);
        }

        static void StripSide(List<UnitInstance> board)
        {
            for (int i = 0; i < board.Count; i++)
            {
                UnitInstance u = board[i];
                u.Atk -= u.AuraAtk;
                u.Hp -= u.AuraHp;
                u.MaxHp -= u.AuraHp;
                u.AuraAtk = 0;
                u.AuraHp = 0;
                u.EchoTimesBonus = 0;
                if (u.Atk < 0) u.Atk = 0;
                if (u.Hp > 0 && u.MaxHp < u.Hp) { }
                if (u.Hp <= 0) { }
                else if (u.Hp < 1) u.Hp = 1;
            }
        }

        static void ApplyAuras(CombatRuntime rt, PlayerState p)
        {
            var snap = new List<UnitInstance>(p.Board.Count);
            for (int i = 0; i < p.Board.Count; i++) snap.Add(p.Board[i]);
            for (int i = 0; i < snap.Count; i++)
            {
                UnitInstance u = snap[i];
                if (!StillOn(p.Board, u) && IndexOf(p.Board, u) < 0) continue;
                UnitDef def = rt.Cat.GetUnit(u.CatalogId);
                var fx = u.AllEffects(def);
                for (int e = 0; e < fx.Count; e++)
                {
                    if (fx[e].Trigger != Trigger.Aura) continue;
                    for (int a = 0; a < fx[e].Actions.Count; a++)
                    {
                        ActionDef act = fx[e].Actions[a];
                        if (act.Type == ActionType.SetEchoTimesBonus)
                        {
                            int amt = act.Amount > 0 ? act.Amount : 1;
                            for (int k = 0; k < p.Board.Count; k++)
                                p.Board[k].EchoTimesBonus += amt;
                        }
                        else if (act.Type == ActionType.BuffStats)
                        {
                            FilterDef f = act.Filter ?? fx[e].Filter;
                            if (f != null && f.Select == TargetSelect.Adjacent)
                            {
                                int idx = IndexOf(p.Board, u);
                                if (idx < 0) continue;
                                if (idx - 1 >= 0) Units.ApplyAura(p.Board[idx - 1], act.Atk, act.Hp);
                                if (idx + 1 < p.Board.Count) Units.ApplyAura(p.Board[idx + 1], act.Atk, act.Hp);
                            }
                            else
                            {
                                Units.ApplyAura(u, act.Atk, act.Hp);
                            }
                        }
                    }
                }
            }
        }
    }
}
