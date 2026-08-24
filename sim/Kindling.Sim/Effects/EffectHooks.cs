using System.Collections.Generic;
using Kindling.Sim.Catalog;
using Kindling.Sim.Combat;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Kindling.Sim.Rng;

namespace Kindling.Sim.Effects
{
    public sealed class FireContext
    {
        public MatchState Match;
        public Catalog.Catalog Cat;
        public PlayerState Owner;
        public PlayerState OwnerOrig;
        public PlayerState Opponent;
        public UnitInstance Source;
        public UnitInstance Host;
        public UnitInstance Bought;
        public bool InCombat;
        public CombatRuntime Combat;
        public Persist Persist;
        public Trigger Trigger;
    }

    public static class EffectHooks
    {
        public static void Fire(MatchState m, Catalog.Catalog cat, Trigger t, UnitInstance source, PlayerState owner, UnitInstance other)
        {
            Fire(new FireContext
            {
                Match = m,
                Cat = cat,
                Owner = owner,
                OwnerOrig = owner,
                Source = source,
                Host = m != null ? m.LatchHost : null,
                Bought = m != null ? m.BoughtUnit : other,
                InCombat = m != null && m.InCombat,
                Trigger = t
            }, t, source, owner);
        }

        public static void FireCombat(CombatRuntime rt, Trigger t, UnitInstance source, PlayerState owner)
        {
            Fire(new FireContext
            {
                Match = rt.Match,
                Cat = rt.Cat,
                Owner = owner,
                OwnerOrig = rt.OrigOf(owner),
                Opponent = rt.OpponentOf(owner),
                Source = source,
                InCombat = true,
                Combat = rt,
                Trigger = t
            }, t, source, owner);
        }

        static void Fire(FireContext ctx, Trigger t, UnitInstance source, PlayerState owner)
        {
            if (owner == null || ctx.Cat == null) return;
            var listeners = Collect(ctx, t, source, owner);
            listeners.Sort(CompareListener);
            int echoBonus = 0;
            if (t == Trigger.Echo)
                echoBonus = SumEchoBonus(owner);
            for (int i = 0; i < listeners.Count; i++)
            {
                Listener L = listeners[i];
                if (L.Effect.Once != Once.Never)
                {
                    string key = OnceKey(L, t);
                    if (L.Unit != null && L.Unit.OnceExhausted(key)) continue;
                    if (L.Unit != null) L.Unit.ExhaustOnce(key);
                }
                if (!WhenOk(L.Effect.When, ctx, L.Unit, owner, source))
                    continue;
                FilterDef filter = L.Effect.Filter;
                int times = t == Trigger.Echo ? 1 + echoBonus : (L.Effect.EchoTimes < 1 ? 1 : L.Effect.EchoTimes);
                for (int rep = 0; rep < times; rep++)
                {
                    for (int a = 0; a < L.Effect.Actions.Count; a++)
                    {
                        ActionDef act = L.Effect.Actions[a];
                        FilterDef f = act.Filter ?? filter;
                        List<UnitInstance> targets = ResolveFilter(ctx, f, act.Type, L.Unit, owner, source);
                        ctx.Persist = L.Effect.Persist;
                        Execute(ctx, act, L.Unit, owner, source, targets);
                    }
                }
            }
        }

        struct Listener
        {
            public UnitInstance Unit;
            public EffectDef Effect;
            public int BoardIndex;
            public int HandIndex;
            public int Seq;
            public bool Captain;
        }

        static int CompareListener(Listener a, Listener b)
        {
            int pa = -a.Effect.Priority;
            int pb = -b.Effect.Priority;
            if (pa != pb) return pa.CompareTo(pb);
            int ia = a.Captain ? 1000 : (a.BoardIndex >= 0 ? a.BoardIndex : 100 + a.HandIndex);
            int ib = b.Captain ? 1000 : (b.BoardIndex >= 0 ? b.BoardIndex : 100 + b.HandIndex);
            if (ia != ib) return ia.CompareTo(ib);
            return a.Seq.CompareTo(b.Seq);
        }

        static List<Listener> Collect(FireContext ctx, Trigger t, UnitInstance source, PlayerState owner)
        {
            var list = new List<Listener>();
            int seq = 0;
            bool sideWide = t == Trigger.OnBuy || t == Trigger.OnSell || t == Trigger.OnReroll
                || t == Trigger.OnLatch || t == Trigger.OnUpgrade || t == Trigger.OnAwaken
                || t == Trigger.StartOfRecruit || t == Trigger.EndOfRecruit
                || t == Trigger.OnVenomKill || t == Trigger.OnKill || t == Trigger.Aura;
            if (sideWide)
            {
                CollectList(ctx.Cat, list, owner.Board, t, ref seq, board: true);
                if (!ctx.InCombat)
                    CollectList(ctx.Cat, list, owner.Hand, t, ref seq, board: false);
            }
            if (source != null && (t == Trigger.Echo || t == Trigger.OnKill || t == Trigger.OnVenomKill || t == Trigger.Kindle || t == Trigger.OnAttack || t == Trigger.OnDamaged || t == Trigger.OnDamageDealt || t == Trigger.Arrival))
            {
                bool present = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Unit != null && list[i].Unit.InstanceId == source.InstanceId)
                    {
                        present = true;
                        break;
                    }
                }
                if (!present)
                {
                    UnitDef def = ctx.Cat != null ? ctx.Cat.GetUnit(source.CatalogId) : null;
                    var fx = source.AllEffects(def);
                    for (int e = 0; e < fx.Count; e++)
                    {
                        if (fx[e].Trigger != t) continue;
                        list.Add(new Listener
                        {
                            Unit = source,
                            Effect = fx[e],
                            BoardIndex = 0,
                            HandIndex = -1,
                            Seq = seq++
                        });
                    }
                }
            }
            return list;
        }

        static void CollectList(Catalog.Catalog cat, List<Listener> list, List<UnitInstance> units, Trigger t, ref int seq, bool board)
        {
            if (units == null) return;
            for (int i = 0; i < units.Count; i++)
            {
                UnitInstance u = units[i];
                if (u.DeathProcessed && t != Trigger.Echo) continue;
                UnitDef def = cat != null ? cat.GetUnit(u.CatalogId) : null;
                var fx = u.AllEffects(def);
                for (int e = 0; e < fx.Count; e++)
                {
                    if (fx[e].Trigger == t)
                    {
                        list.Add(new Listener
                        {
                            Unit = u,
                            Effect = fx[e],
                            BoardIndex = board ? i : -1,
                            HandIndex = board ? -1 : i,
                            Seq = seq++
                        });
                    }
                }
            }
        }

        public static List<EffectDef> EffectsOf(UnitInstance u, Catalog.Catalog cat)
        {
            UnitDef def = cat != null ? cat.GetUnit(u.CatalogId) : null;
            return u.AllEffects(def);
        }

        static bool WhenOk(ConditionDef c, FireContext ctx, UnitInstance self, PlayerState owner, UnitInstance source)
        {
            if (c == null) return true;
            switch (c.Op)
            {
                case ConditionOp.Always: return true;
                case ConditionOp.EmbersGte: return owner.Embers >= c.N;
                case ConditionOp.DepthGte: return owner.Depth >= c.N;
                case ConditionOp.BoughtThisRecruitGte: return owner.BoughtThisRecruit >= c.N;
                case ConditionOp.RerollsThisRecruitGte: return owner.RerollsThisRecruit >= c.N;
                case ConditionOp.HasFlag:
                    return owner.HasFlag(Catalog.Catalog.ParsePlayerFlag(c.Flag));
                case ConditionOp.ChorusIs:
                    return ChorusOf(self, ctx.Cat) == Catalog.Catalog.ParseChorus(c.Chorus);
                case ConditionOp.IsAwakened: return self != null && self.Awakened;
                case ConditionOp.WickGte: return owner.Wick >= c.N;
                case ConditionOp.BoardCountGte: return owner.Board.Count >= c.N;
                case ConditionOp.HandNotFull: return owner.Hand.Count < Rules.HandMax;
                case ConditionOp.SourceIsChorus:
                    return ChorusOf(source, ctx.Cat) == Catalog.Catalog.ParseChorus(c.Chorus);
                default: return true;
            }
        }

        static Chorus ChorusOf(UnitInstance u, Catalog.Catalog cat)
        {
            if (u == null || cat == null) return Chorus.Neutral;
            UnitDef d = cat.GetUnit(u.CatalogId);
            return d != null ? d.Chorus : Chorus.Neutral;
        }

        static string OnceKey(Listener L, Trigger t)
        {
            string id = L.Unit != null ? L.Unit.InstanceId.ToString() : "cap";
            return id + "|" + L.Effect.Once + "|" + t;
        }

        static int SumEchoBonus(PlayerState owner)
        {
            int s = 0;
            for (int i = 0; i < owner.Board.Count; i++)
                s += owner.Board[i].EchoTimesBonus;
            return s;
        }

        static List<UnitInstance> ResolveFilter(FireContext ctx, FilterDef f, ActionType actionType, UnitInstance self, PlayerState owner, UnitInstance source)
        {
            var result = new List<UnitInstance>();
            if (f == null)
            {
                if (self != null) result.Add(self);
                return result;
            }
            List<UnitInstance> pool;
            switch (f.Select)
            {
                case TargetSelect.Self:
                    if (self != null) result.Add(self);
                    return result;
                case TargetSelect.Host:
                    if (ctx.Host != null) result.Add(ctx.Host);
                    else if (ctx.Combat != null && self != null) result.Add(self);
                    return result;
                case TargetSelect.BoughtUnit:
                    if (ctx.Bought != null) result.Add(ctx.Bought);
                    return result;
                case TargetSelect.Source:
                    if (source != null) result.Add(source);
                    return result;
                case TargetSelect.Leftmost:
                    if (owner.Board.Count > 0) result.Add(owner.Board[0]);
                    return FilterExtras(result, f, self, ctx.Cat);
                case TargetSelect.Rightmost:
                    if (owner.Board.Count > 0) result.Add(owner.Board[owner.Board.Count - 1]);
                    return FilterExtras(result, f, self, ctx.Cat);
                case TargetSelect.Adjacent:
                    AddAdjacent(result, owner, self);
                    return FilterExtras(result, f, self, ctx.Cat);
                case TargetSelect.Other:
                    pool = CopyLiving(owner.Board);
                    RemoveSelf(pool, self);
                    break;
                case TargetSelect.Enemy:
                    pool = ctx.Opponent != null ? CopyLiving(ctx.Opponent.Board) : new List<UnitInstance>();
                    break;
                case TargetSelect.All:
                    pool = CopyLiving(owner.Board);
                    if (ctx.Opponent != null) AddAll(pool, ctx.Opponent.Board);
                    break;
                case TargetSelect.RandomN:
                    if (actionType == ActionType.DealDamage && ctx.Opponent != null)
                        pool = CopyLiving(ctx.Opponent.Board);
                    else
                        pool = CopyLiving(owner.Board);
                    break;
                default:
                    pool = CopyLiving(owner.Board);
                    break;
            }
            pool = FilterExtras(pool, f, self, ctx.Cat);
            if (f.Select == TargetSelect.RandomN || (f.NSpecified && f.Select != TargetSelect.Friendly && f.Select != TargetSelect.All && f.Select != TargetSelect.Other && f.Select != TargetSelect.Enemy))
            {
                int n = f.NSpecified ? f.N : 1;
                return PickRandom(ctx, pool, n);
            }
            if (f.Select == TargetSelect.RandomN)
                return PickRandom(ctx, pool, f.NSpecified ? f.N : 1);
            if (f.Select == TargetSelect.Enemy && actionType == ActionType.DealDamage && !f.NSpecified)
                return PickRandom(ctx, pool, 1);
            if ((f.Select == TargetSelect.Other || f.Select == TargetSelect.Friendly) && actionType == ActionType.GrantKeyword && !f.NSpecified && pool.Count > 1 && ctx.Trigger == Trigger.Kindle)
                return PickRandom(ctx, pool, 1);
            if (f.Select == TargetSelect.Other && actionType == ActionType.BuffStats && pool.Count > 0 && !f.NSpecified && ctx.Trigger == Trigger.Arrival)
                return PickRandom(ctx, pool, 1);
            return pool;
        }

        static List<UnitInstance> FilterExtras(List<UnitInstance> pool, FilterDef f, UnitInstance self, Catalog.Catalog cat)
        {
            var outp = new List<UnitInstance>();
            for (int i = 0; i < pool.Count; i++)
            {
                UnitInstance u = pool[i];
                if (u == null || u.DeathProcessed || u.Hp <= 0) continue;
                if (f.ExcludeSelf && self != null && (ReferenceEquals(u, self) || u.InstanceId == self.InstanceId))
                    continue;
                if (!string.IsNullOrEmpty(f.Chorus) && ChorusOf(u, cat) != Catalog.Catalog.ParseChorus(f.Chorus))
                    continue;
                if (!string.IsNullOrEmpty(f.Keyword))
                {
                    if (f.Keyword == "Echo")
                    {
                        UnitDef d = cat != null ? cat.GetUnit(u.CatalogId) : null;
                        if (!u.HasEcho(d)) continue;
                    }
                    else
                    {
                        Keyword k = Catalog.Catalog.ParseKeyword(f.Keyword);
                        if (k != Keyword.None && !u.Has(k)) continue;
                    }
                }
                outp.Add(u);
            }
            return outp;
        }

        static List<UnitInstance> PickRandom(FireContext ctx, List<UnitInstance> pool, int n)
        {
            var result = new List<UnitInstance>();
            if (pool.Count == 0 || n <= 0) return result;
            MatchRng rng = ctx.Match != null ? ctx.Match.Rng : (ctx.Combat != null ? ctx.Combat.Rng : null);
            if (rng == null)
            {
                result.Add(pool[0]);
                return result;
            }
            MatchRng.Stream stream = ctx.InCombat ? MatchRng.Stream.Combat : MatchRng.Stream.Recruit;
            var work = new List<UnitInstance>(pool.Count);
            for (int i = 0; i < pool.Count; i++) work.Add(pool[i]);
            int take = n < work.Count ? n : work.Count;
            for (int i = 0; i < take; i++)
            {
                int r = rng.Range(stream, 0, work.Count);
                result.Add(work[r]);
                work.RemoveAt(r);
            }
            return result;
        }

        static List<UnitInstance> CopyLiving(List<UnitInstance> src)
        {
            var l = new List<UnitInstance>();
            if (src == null) return l;
            for (int i = 0; i < src.Count; i++)
            {
                if (src[i] != null && !src[i].DeathProcessed) l.Add(src[i]);
            }
            return l;
        }

        static void AddAll(List<UnitInstance> dst, List<UnitInstance> src)
        {
            for (int i = 0; i < src.Count; i++)
                if (src[i] != null && !src[i].DeathProcessed) dst.Add(src[i]);
        }

        static void RemoveSelf(List<UnitInstance> pool, UnitInstance self)
        {
            if (self == null) return;
            for (int i = pool.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(pool[i], self) || pool[i].InstanceId == self.InstanceId)
                    pool.RemoveAt(i);
            }
        }

        static void AddAdjacent(List<UnitInstance> result, PlayerState owner, UnitInstance self)
        {
            int idx = -1;
            for (int i = 0; i < owner.Board.Count; i++)
            {
                if (ReferenceEquals(owner.Board[i], self) || (self != null && owner.Board[i].InstanceId == self.InstanceId))
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0) return;
            if (idx - 1 >= 0) result.Add(owner.Board[idx - 1]);
            if (idx + 1 < owner.Board.Count) result.Add(owner.Board[idx + 1]);
        }

        static void Execute(FireContext ctx, ActionDef act, UnitInstance self, PlayerState owner, UnitInstance source, List<UnitInstance> targets)
        {
            PlayerState persistOwner = (ctx.Persist == Persist.Player && ctx.OwnerOrig != null) ? ctx.OwnerOrig : owner;
            switch (act.Type)
            {
                case ActionType.NoOp:
                    break;
                case ActionType.BuffStats:
                    foreach (UnitInstance t in targets)
                        Buff(ctx, t, act.Atk, act.Hp, act.Duration);
                    if (targets.Count == 0 && self != null)
                        Buff(ctx, self, act.Atk, act.Hp, act.Duration);
                    break;
                case ActionType.BuffStatsScaled:
                    {
                        int n = CounterValue(persistOwner, act.Counter);
                        Buff(ctx, self ?? (targets.Count > 0 ? targets[0] : null), act.Atk * n, act.Hp * n, act.Duration);
                        break;
                    }
                case ActionType.GiveCinder:
                    {
                        var ts = targets.Count > 0 ? targets : (self != null ? new List<UnitInstance> { self } : targets);
                        for (int i = 0; i < ts.Count; i++)
                        {
                            UnitInstance t = ts[i];
                            ApplyPersistUnit(ctx, t, u => Units.GiveCinder(u, act.Amount > 0 ? act.Amount : 1));
                        }
                        break;
                    }
                case ActionType.GrantKeyword:
                    {
                        Keyword kw = act.Keyword == "Echo"
                            ? Keyword.None
                            : Catalog.Catalog.ParseKeyword(act.Keyword);
                        var ts = targets.Count > 0 ? targets : (self != null ? new List<UnitInstance> { self } : targets);
                        for (int i = 0; i < ts.Count; i++)
                        {
                            UnitInstance t = ts[i];
                            if (act.Keyword == "Echo")
                            {
                                ApplyPersistUnit(ctx, t, u =>
                                {
                                    u.ExtraEffects.Add(new EffectDef
                                    {
                                        Trigger = Trigger.Echo,
                                        Persist = Persist.CombatCopy,
                                        Actions = new List<ActionDef>()
                                    });
                                });
                            }
                            else if (kw != Keyword.None)
                            {
                                GrantKw(ctx, t, kw, act.Duration);
                            }
                        }
                        break;
                    }
                case ActionType.RemoveKeyword:
                    {
                        Keyword kw = Catalog.Catalog.ParseKeyword(act.Keyword);
                        for (int i = 0; i < targets.Count; i++)
                            targets[i].RemoveKeyword(kw);
                        break;
                    }
                case ActionType.DealDamage:
                    if (ctx.Combat != null)
                    {
                        for (int i = 0; i < targets.Count; i++)
                            ctx.Combat.ApplyEffectDamage(self, targets[i], act.Amount);
                    }
                    break;
                case ActionType.GainEmbers:
                    persistOwner.Embers = Rules.ClampEmbers(persistOwner.Embers + act.Amount);
                    break;
                case ActionType.PendingEmbers:
                    persistOwner.PendingEmbers += act.Amount > 0 ? act.Amount : 1;
                    break;
                case ActionType.PendingEmbersFromCounter:
                    {
                        int n = CounterValue(persistOwner, act.Counter);
                        if (act.Max > 0 && n > act.Max) n = act.Max;
                        persistOwner.PendingEmbers += n;
                        break;
                    }
                case ActionType.SetFlag:
                    persistOwner.SetFlag(Catalog.Catalog.ParsePlayerFlag(act.Flag));
                    break;
                case ActionType.ClearFlag:
                    persistOwner.ClearFlag(Catalog.Catalog.ParsePlayerFlag(act.Flag));
                    break;
                case ActionType.SetHold:
                    persistOwner.Hold = true;
                    break;
                case ActionType.RerollStall:
                    if (ctx.Match != null)
                        Stall.Reroll(ctx.Match, persistOwner, ctx.Cat, ctx.Match.Rng);
                    break;
                case ActionType.AddToHand:
                    AddToHand(ctx, persistOwner, new UnitId(act.Unit));
                    break;
                case ActionType.AddToHandFromPool:
                    AddToHandFromPool(ctx, persistOwner, act);
                    break;
                case ActionType.CopyOwnedToHand:
                    CopyOwnedToHand(ctx, persistOwner, act);
                    break;
                case ActionType.Glimpse:
                    {
                        int d = act.DepthMode == DepthMode.Current ? persistOwner.Depth : (act.DepthSpecified ? act.Depth : persistOwner.Depth);
                        Glimpse.Enqueue(ctx.Match, persistOwner, ctx.Cat, self, act.DepthMode, d);
                        break;
                    }
                case ActionType.Summon:
                    Summon(ctx, owner, act, act.CountSpecified ? act.Count : 1);
                    break;
                case ActionType.SummonFill:
                    SummonFill(ctx, owner, act);
                    break;
                case ActionType.SummonFromGraveyard:
                    SummonFromGraveyard(ctx, owner, act, self);
                    break;
                case ActionType.AttachLatch:
                    {
                        UnitInstance host = targets.Count > 0 ? targets[0] : self;
                        if (host != null && !string.IsNullOrEmpty(act.Unit) && ctx.Match != null)
                            LatchOps.AttachToken(ctx.Match, persistOwner, ctx.Cat, host, new UnitId(act.Unit));
                        break;
                    }
                case ActionType.GiveEchoSummon:
                    if (self != null || (targets.Count > 0))
                    {
                        UnitInstance t = targets.Count > 0 ? targets[0] : self;
                        ApplyPersistUnit(ctx, t, u =>
                        {
                            u.ExtraEffects.Add(new EffectDef
                            {
                                Trigger = Trigger.Echo,
                                Persist = Persist.CombatCopy,
                                Actions = new List<ActionDef>
                                {
                                    new ActionDef { Type = ActionType.Summon, Unit = act.EchoUnit, Count = 1 }
                                }
                            });
                        });
                    }
                    break;
                case ActionType.CopyArrival:
                    CopyArrival(ctx, persistOwner, self, targets);
                    break;
                case ActionType.DamageWick:
                    {
                        int amt = act.Amount > 0 ? act.Amount : 1;
                        if (persistOwner.Wick - amt < 1) break;
                        persistOwner.Wick -= amt;
                        break;
                    }
                case ActionType.SetStallSizeDelta:
                    persistOwner.StallSizeDelta += act.Amount;
                    break;
                case ActionType.SetEchoTimesBonus:
                    if (self != null) self.EchoTimesBonus += act.Amount > 0 ? act.Amount : 1;
                    break;
                case ActionType.ModifyCost:
                    break;
            }
        }

        static int CounterValue(PlayerState p, CounterKind k)
        {
            switch (k)
            {
                case CounterKind.LatchPlaysThisMatch: return p.LatchPlaysThisMatch;
                case CounterKind.BoughtThisRecruit: return p.BoughtThisRecruit;
                default: return p.RerollsThisRecruit;
            }
        }

        static void Buff(FireContext ctx, UnitInstance t, int atk, int hp, Duration d)
        {
            if (t == null) return;
            ApplyPersistUnit(ctx, t, u =>
            {
                if (d == Duration.ThisCombat)
                    Units.BuffCombat(u, atk, hp);
                else
                    Units.BuffPermanent(u, atk, hp);
            });
        }

        static void GrantKw(FireContext ctx, UnitInstance t, Keyword kw, Duration d)
        {
            ApplyPersistUnit(ctx, t, u =>
            {
                u.AddKeyword(kw);
                if (d == Duration.ThisCombat)
                {
                    u.Mods.Add(new Modifier { Tag = ModTag.ThisCombat, Keywords = kw });
                }
            });
        }

        static void ApplyPersistUnit(FireContext ctx, UnitInstance combatOrLive, System.Action<UnitInstance> apply)
        {
            if (combatOrLive == null) return;
            apply(combatOrLive);
            if (ctx.Persist == Persist.Player && ctx.OwnerOrig != null && ctx.InCombat)
            {
                UnitInstance orig = ctx.OwnerOrig.FindOwned(combatOrLive.InstanceId);
                if (orig != null && !ReferenceEquals(orig, combatOrLive))
                    apply(orig);
            }
        }

        static void AddToHand(FireContext ctx, PlayerState p, UnitId id)
        {
            if (ctx.Cat.GetUnit(id) == null) return;
            if (p.Hand.Count >= Rules.HandMax)
            {
                ctx.Match?.AddLog("HandFull");
                p.AddLog("HandFull");
                return;
            }
            p.Hand.Add(Units.Create(ctx.Cat, ctx.Match != null ? ctx.Match.Rng : ctx.Combat?.Rng, id));
        }

        static void AddToHandFromPool(FireContext ctx, PlayerState p, ActionDef act)
        {
            if (p.Hand.Count >= Rules.HandMax)
            {
                ctx.Match?.AddLog("HandFull");
                p.AddLog("HandFull");
                return;
            }
            Chorus ch = Catalog.Catalog.ParseChorus(act.Chorus);
            int depthMax = act.DepthMax > 0 ? act.DepthMax : 6;
            var cand = new List<PoolEntry>();
            MatchState m = ctx.Match;
            if (m == null) return;
            for (int i = 0; i < m.Pool.Count; i++)
            {
                PoolEntry e = m.Pool[i];
                UnitDef def = ctx.Cat.GetUnit(e.Id);
                if (def == null || def.Token || def.Disabled) continue;
                if (def.Chorus != ch) continue;
                if (def.Depth > depthMax) continue;
                cand.Add(e);
            }
            if (cand.Count == 0) return;
            int r = m.Rng.Range(MatchRng.Stream.Recruit, 0, cand.Count);
            PoolEntry pick = cand[r];
            if (act.Consume)
            {
                if (pick.Remaining > 0) pick.Remaining--;
                else m.AddToHandFromPoolOverflow++;
            }
            p.Hand.Add(Units.Create(ctx.Cat, m.Rng, pick.Id));
        }

        static void CopyOwnedToHand(FireContext ctx, PlayerState p, ActionDef act)
        {
            if (p.Hand.Count >= Rules.HandMax)
            {
                ctx.Match?.AddLog("HandFull");
                p.AddLog("HandFull");
                return;
            }
            var cand = new List<UnitInstance>();
            CollectOwnedShop(p, ctx.Cat, cand, act.ShopLegalOnly);
            if (cand.Count == 0) return;
            MatchRng rng = ctx.Match != null ? ctx.Match.Rng : ctx.Combat?.Rng;
            int r = rng.Range(MatchRng.Stream.Recruit, 0, cand.Count);
            UnitId id = cand[r].CatalogId;
            if (act.ConsumePool && ctx.Match != null)
            {
                if (!Pool.TryConsume(ctx.Match, id))
                    ctx.Match.MirrorGrants++;
            }
            else if (ctx.Match != null)
            {
                ctx.Match.MirrorGrants++;
            }
            p.Hand.Add(Units.Create(ctx.Cat, rng, id));
        }

        static void CollectOwnedShop(PlayerState p, Catalog.Catalog cat, List<UnitInstance> cand, bool shopLegalOnly)
        {
            void add(UnitInstance u)
            {
                UnitDef d = cat.GetUnit(u.CatalogId);
                if (d == null) return;
                if (shopLegalOnly && d.Token) return;
                cand.Add(u);
            }
            for (int i = 0; i < p.Board.Count; i++) add(p.Board[i]);
            for (int i = 0; i < p.Hand.Count; i++) add(p.Hand[i]);
        }

        static void CopyArrival(FireContext ctx, PlayerState p, UnitInstance self, List<UnitInstance> targets)
        {
            UnitInstance target = null;
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (self != null && targets[i].InstanceId == self.InstanceId) continue;
                    target = targets[i];
                    break;
                }
            }
            if (target == null)
            {
                for (int i = 0; i < p.Board.Count; i++)
                {
                    if (self != null && p.Board[i].InstanceId == self.InstanceId) continue;
                    UnitDef d = ctx.Cat.GetUnit(p.Board[i].CatalogId);
                    if (p.Board[i].AllEffects(d).Exists(e => e.Trigger == Trigger.Arrival))
                    {
                        target = p.Board[i];
                        break;
                    }
                }
            }
            if (target == null) return;
            UnitDef def = ctx.Cat.GetUnit(target.CatalogId);
            var fx = target.AllEffects(def);
            for (int i = 0; i < fx.Count; i++)
            {
                if (fx[i].Trigger == Trigger.Arrival)
                {
                    var clone = new FireContext
                    {
                        Match = ctx.Match,
                        Cat = ctx.Cat,
                        Owner = p,
                        OwnerOrig = p,
                        Source = target,
                        InCombat = false,
                        Persist = Persist.Player,
                        Trigger = Trigger.Arrival
                    };
                    for (int a = 0; a < fx[i].Actions.Count; a++)
                    {
                        ActionDef act = fx[i].Actions[a];
                        List<UnitInstance> ts = ResolveFilter(clone, fx[i].Filter ?? act.Filter, act.Type, target, p, target);
                        Execute(clone, act, target, p, target, ts);
                    }
                }
            }
        }

        static void Summon(FireContext ctx, PlayerState owner, ActionDef act, int count)
        {
            if (string.IsNullOrEmpty(act.Unit)) return;
            for (int i = 0; i < count; i++)
                TrySummonOne(ctx, owner, act);
        }

        static void SummonFill(FireContext ctx, PlayerState owner, ActionDef act)
        {
            for (int i = 0; i < Rules.BoardMax + 4; i++)
            {
                if (owner.Board.Count >= Rules.BoardMax) break;
                if (ctx.Combat != null && ctx.Combat.LifetimeSummons >= Rules.LifetimeSummonCap) break;
                if (!TrySummonOne(ctx, owner, act)) break;
            }
        }

        static bool TrySummonOne(FireContext ctx, PlayerState owner, ActionDef act)
        {
            if (ctx.InCombat && ctx.Combat != null)
                return ctx.Combat.TrySummon(owner, act);
            if (owner.Board.Count >= Rules.BoardMax)
            {
                ctx.Match?.AddLog("BoardFull");
                return false;
            }
            UnitInstance neu = Units.Create(ctx.Cat, ctx.Match.Rng, new UnitId(act.Unit));
            if (act.AtkSpecified) { neu.Atk = act.Atk; }
            if (act.HpSpecified) { neu.Hp = act.Hp; neu.MaxHp = act.Hp; }
            owner.Board.Add(neu);
            if (ctx.Match != null)
            {
                UnitDef d = ctx.Cat.GetUnit(neu.CatalogId);
                if (d != null && d.Token) ctx.Match.TokenSpawned++;
            }
            if (act.FireArrival)
                Fire(ctx.Match, ctx.Cat, Trigger.Arrival, neu, owner, null);
            return true;
        }

        static void SummonFromGraveyard(FireContext ctx, PlayerState owner, ActionDef act, UnitInstance self)
        {
            if (ctx.Combat == null) return;
            ctx.Combat.SummonFromGraveyard(owner, act, self);
        }
    }
}
