using Kindling.Sim.Captains;
using Kindling.Sim.Catalog;
using Kindling.Sim.Effects;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;

namespace Kindling.Sim.Validation
{
    public static class RecruitValidator
    {
        public static SimResult TryApply(MatchState m, RecruitAction a, Catalog.Catalog cat)
        {
            if (m == null || a == null) return SimResult.Fail("BAD_ACTION");
            if (a.Seat < 0 || a.Seat >= m.Seats.Length) return SimResult.Fail("BAD_SEAT");
            PlayerState p = m.Seats[a.Seat];
            if (p == null) return SimResult.Fail("BAD_SEAT");
            if (m.Phase == Phase.MatchOver) return SimResult.Fail("MATCH_OVER");
            if (a.Op != RecruitOp.CaptainPick && !p.Alive) return SimResult.Fail("DEAD");
            m.Season?.ValidateAction(p, a);

            SimResult r;
            switch (a.Op)
            {
                case RecruitOp.CaptainPick: r = CaptainPick(m, p, a, cat); break;
                case RecruitOp.Buy: r = Buy(m, p, a, cat); break;
                case RecruitOp.Sell: r = Sell(m, p, a, cat); break;
                case RecruitOp.Reroll: r = Reroll(m, p, a, cat); break;
                case RecruitOp.Hold: r = Hold(p, a); break;
                case RecruitOp.Upgrade: r = Upgrade(m, p, a, cat); break;
                case RecruitOp.Play: r = Play(m, p, a, cat); break;
                case RecruitOp.Reorder: r = Reorder(p, a); break;
                case RecruitOp.Latch: r = Latch(m, p, a, cat); break;
                case RecruitOp.Edict: r = Edict(m, p, a, cat); break;
                case RecruitOp.GlimpsePick: r = GlimpsePick(m, p, a, cat); break;
                case RecruitOp.Ready: r = Ready(p); break;
                default: r = SimResult.Fail("UNKNOWN_OP"); break;
            }
            if (r.Ok)
            {
                m.Seq++;
                p.PlayerSeq++;
            }
            return r;
        }

        static SimResult RequireRecruit(MatchState m)
        {
            if (m.Phase != Phase.Recruit) return SimResult.Fail("WRONG_PHASE");
            return SimResult.Success();
        }

        static SimResult CaptainPick(MatchState m, PlayerState p, RecruitAction a, Catalog.Catalog cat)
        {
            if (m.Phase != Phase.CaptainPick) return SimResult.Fail("WRONG_PHASE");
            if (!p.Captain.IsEmpty) return SimResult.Fail("ALREADY_PICKED");
            string id = null;
            if (a.OfferIndex >= 0 && p.CaptainOffers != null && a.OfferIndex < p.CaptainOffers.Length)
                id = p.CaptainOffers[a.OfferIndex].Value;
            else if (!string.IsNullOrEmpty(a.CaptainId))
                id = a.CaptainId;
            CaptainDef def = cat.GetCaptain(id);
            if (def == null) return SimResult.Fail("BAD_CAPTAIN");
            bool inOffer = OfferContains(p, def.Id.Value);
            if (!inOffer && a.OfferIndex >= 0) return SimResult.Fail("BAD_CAPTAIN");
            if (CaptainTaken(m, def.Id, p.Seat)) return SimResult.Fail("CAPTAIN_TAKEN");
            p.Captain = def.Id;
            CaptainPassives.OnCaptainPicked(p, cat);
            return SimResult.Success();
        }

        public static bool CaptainTaken(MatchState m, CaptainId id, int exceptSeat)
        {
            if (m == null || id.IsEmpty) return false;
            for (int i = 0; i < m.Seats.Length; i++)
            {
                if (i == exceptSeat) continue;
                if (m.Seats[i].Captain == id) return true;
            }
            return false;
        }

        static bool OfferContains(PlayerState p, string id)
        {
            if (p == null || p.CaptainOffers == null || string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < p.CaptainOffers.Length; i++)
            {
                if (string.Equals(p.CaptainOffers[i].Value, id, System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        static SimResult Buy(MatchState m, PlayerState p, RecruitAction a, Catalog.Catalog cat)
        {
            SimResult phase = RequireRecruit(m);
            if (!phase.Ok) return phase;
            if (p.Embers < Rules.BuyCost) return SimResult.Fail("NOT_ENOUGH_EMBERS");
            if (a.StallIndex < 0 || a.StallIndex >= p.Stall.Count) return SimResult.Fail("BAD_INDEX");
            UnitInstance u = p.Stall[a.StallIndex];
            if (u == null) return SimResult.Fail("EMPTY_SLOT");
            if (p.Hand.Count >= Rules.HandMax) return SimResult.Fail("HAND_FULL");
            int destIndex = a.DestIndex;
            if (destIndex < 0 || destIndex > p.Hand.Count) destIndex = p.Hand.Count;
            p.Hand.Insert(destIndex, u);
            p.Stall[a.StallIndex] = null;
            p.Embers -= Rules.BuyCost;
            p.BoughtThisRecruit++;
            m.BoughtUnit = u;
            CaptainPassives.OnBuy(p, cat, u);
            EffectHooks.Fire(m, cat, Trigger.OnBuy, u, p, u);
            m.BoughtUnit = null;
            Awaken.TryAwaken(m, p, cat);
            return SimResult.Success();
        }

        static SimResult Sell(MatchState m, PlayerState p, RecruitAction a, Catalog.Catalog cat)
        {
            SimResult phase = RequireRecruit(m);
            if (!phase.Ok) return phase;
            UnitInstance u = TakeFrom(p, a.Loc, a.Index);
            if (u == null) return SimResult.Fail("BAD_INDEX");
            UnitDef def = cat.GetUnit(u.CatalogId);
            if (def != null && !def.Token)
                Pool.Return(m, u.CatalogId, 1);
            else if (def != null && def.Token && m != null)
                m.TokenDestroyed++;
            p.Embers = Rules.ClampEmbers(p.Embers + Rules.SellReward);
            EffectHooks.Fire(m, cat, Trigger.OnSell, u, p, u);
            Awaken.TryAwaken(m, p, cat);
            return SimResult.Success();
        }

        static SimResult Reroll(MatchState m, PlayerState p, RecruitAction a, Catalog.Catalog cat)
        {
            SimResult phase = RequireRecruit(m);
            if (!phase.Ok) return phase;
            int cost = Grant.RerollCostNow(p);
            if (p.Embers < cost) return SimResult.Fail("NOT_ENOUGH_EMBERS");
            p.Embers -= cost;
            Grant.ConsumeFreeRerollFlags(p);
            Stall.Reroll(m, p, cat, m.Rng);
            p.RerollsThisRecruit++;
            EffectHooks.Fire(m, cat, Trigger.OnReroll, null, p, null);
            return SimResult.Success();
        }

        static SimResult Hold(PlayerState p, RecruitAction a)
        {
            p.Hold = a.Held;
            return SimResult.Success();
        }

        static SimResult Upgrade(MatchState m, PlayerState p, RecruitAction a, Catalog.Catalog cat)
        {
            SimResult phase = RequireRecruit(m);
            if (!phase.Ok) return phase;
            if (p.Depth >= Rules.MaxDepth) return SimResult.Fail("MAX_DEPTH");
            if (p.Embers < p.UpgradeCost) return SimResult.Fail("NOT_ENOUGH_EMBERS");
            p.Embers -= p.UpgradeCost;
            p.Depth++;
            p.SetFlag(PlayerFlags.UpgradedThisRecruit);
            p.UpgradeCost = Rules.UpgradeCostBase(p.Depth);
            CaptainPassives.OnUpgrade(p, cat);
            EffectHooks.Fire(m, cat, Trigger.OnUpgrade, null, p, null);
            return SimResult.Success();
        }

        static SimResult Play(MatchState m, PlayerState p, RecruitAction a, Catalog.Catalog cat)
        {
            SimResult phase = RequireRecruit(m);
            if (!phase.Ok) return phase;
            if (a.HandIndex < 0 || a.HandIndex >= p.Hand.Count) return SimResult.Fail("BAD_INDEX");
            UnitInstance u = p.Hand[a.HandIndex];
            UnitDef def = cat.GetUnit(u.CatalogId);
            if (def != null && def.Spell)
            {
                p.Hand.RemoveAt(a.HandIndex);
                EffectHooks.Fire(m, cat, Trigger.Arrival, u, p, null);
                return SimResult.Success();
            }
            if (p.Board.Count >= Rules.BoardMax) return SimResult.Fail("BOARD_FULL");
            p.Hand.RemoveAt(a.HandIndex);
            int dest = a.DestIndex;
            if (dest < 0 || dest > p.Board.Count) dest = p.Board.Count;
            p.Board.Insert(dest, u);
            EffectHooks.Fire(m, cat, Trigger.Arrival, u, p, null);
            Awaken.TryAwaken(m, p, cat);
            return SimResult.Success();
        }

        static SimResult Reorder(PlayerState p, RecruitAction a)
        {
            if (a.BoardPerm == null) return SimResult.Fail("BAD_PERM");
            if (a.BoardPerm.Length != p.Board.Count) return SimResult.Fail("BAD_PERM");
            var seen = new bool[p.Board.Count];
            var next = new UnitInstance[p.Board.Count];
            for (int i = 0; i < a.BoardPerm.Length; i++)
            {
                int idx = a.BoardPerm[i];
                if (idx < 0 || idx >= p.Board.Count || seen[idx]) return SimResult.Fail("BAD_PERM");
                seen[idx] = true;
                next[i] = p.Board[idx];
            }
            p.Board.Clear();
            for (int i = 0; i < next.Length; i++) p.Board.Add(next[i]);
            return SimResult.Success();
        }

        static SimResult Latch(MatchState m, PlayerState p, RecruitAction a, Catalog.Catalog cat)
        {
            SimResult phase = RequireRecruit(m);
            if (!phase.Ok) return phase;
            return LatchOps.TryLatch(m, p, cat, a.From, a.FromIndex, a.HostIndex);
        }

        static SimResult Edict(MatchState m, PlayerState p, RecruitAction a, Catalog.Catalog cat)
        {
            SimResult phase = RequireRecruit(m);
            if (!phase.Ok) return phase;
            CaptainDef def = cat.GetCaptain(p.Captain);
            if (def == null || !def.HasEdict) return SimResult.Fail("NO_EDICT");
            if (p.Edict.UsedThisRecruit && !p.Edict.Repeatable) return SimResult.Fail("EDICT_USED");
            if (p.Embers < def.EdictCost) return SimResult.Fail("NOT_ENOUGH_EMBERS");
            if (p.Captain.Value == "cap_jun" && p.Wick <= 1) return SimResult.Fail("EDICT_ILLEGAL");
            UnitInstance target = null;
            if (def.EdictNeedsTarget)
            {
                if (a.TargetIndex < 0 || a.TargetIndex >= p.Board.Count) return SimResult.Fail("BAD_TARGET");
                target = p.Board[a.TargetIndex];
            }
            p.Embers -= def.EdictCost;
            p.Edict.UsedThisRecruit = true;
            var ctx = new FireContext
            {
                Match = m,
                Cat = cat,
                Owner = p,
                OwnerOrig = p,
                Source = target,
                Host = target,
                InCombat = false,
                Persist = Persist.Player,
                Trigger = Trigger.Arrival
            };
            for (int i = 0; i < def.EdictActions.Count; i++)
            {
                ActionDef act = def.EdictActions[i];
                if (act.Type == ActionType.RerollStall)
                {
                    Stall.Reroll(m, p, cat, m.Rng);
                    p.RerollsThisRecruit++;
                    continue;
                }
                if (act.Type == ActionType.SetHold)
                {
                    p.Hold = true;
                    continue;
                }
                if (act.Type == ActionType.DamageWick)
                {
                    if (p.Wick - (act.Amount > 0 ? act.Amount : 1) < 1)
                        return SimResult.Fail("EDICT_ILLEGAL");
                }
                var targets = new System.Collections.Generic.List<UnitInstance>();
                if (target != null) targets.Add(target);
                ExecuteEdict(ctx, act, p, target, targets);
            }
            return SimResult.Success();
        }

        static void ExecuteEdict(FireContext ctx, ActionDef act, PlayerState p, UnitInstance target, System.Collections.Generic.List<UnitInstance> targets)
        {
            switch (act.Type)
            {
                case ActionType.AddToHand:
                    if (p.Hand.Count >= Rules.HandMax)
                    {
                        ctx.Match.AddLog("HandFull");
                        p.AddLog("HandFull");
                        break;
                    }
                    p.Hand.Add(Units.Create(ctx.Cat, ctx.Match.Rng, new UnitId(act.Unit)));
                    break;
                case ActionType.Glimpse:
                    Glimpse.Enqueue(ctx.Match, p, ctx.Cat, null, act.DepthMode == 0 ? DepthMode.Current : act.DepthMode, p.Depth);
                    break;
                case ActionType.BuffStats:
                    if (target != null) Units.BuffPermanent(target, act.Atk, act.Hp);
                    break;
                case ActionType.GrantKeyword:
                    if (target != null)
                    {
                        if (act.Keyword == "Echo")
                        {
                            target.ExtraEffects.Add(new EffectDef
                            {
                                Trigger = Trigger.Echo,
                                Persist = Persist.CombatCopy,
                                Actions = new System.Collections.Generic.List<ActionDef>()
                            });
                        }
                        else
                        {
                            Keyword kw = Catalog.Catalog.ParseKeyword(act.Keyword);
                            if (kw != Keyword.None) target.AddKeyword(kw);
                        }
                    }
                    break;
                case ActionType.GiveEchoSummon:
                    if (target != null)
                    {
                        target.ExtraEffects.Add(new EffectDef
                        {
                            Trigger = Trigger.Echo,
                            Persist = Persist.CombatCopy,
                            Actions = new System.Collections.Generic.List<ActionDef>
                            {
                                new ActionDef { Type = ActionType.Summon, Unit = act.EchoUnit, Count = 1 }
                            }
                        });
                    }
                    break;
                case ActionType.DamageWick:
                    {
                        int amt = act.Amount > 0 ? act.Amount : 1;
                        p.Wick -= amt;
                        break;
                    }
                default:
                    EffectHooks.Fire(ctx.Match, ctx.Cat, Trigger.StartOfRecruit, target, p, target);
                    break;
            }
        }

        static SimResult GlimpsePick(MatchState m, PlayerState p, RecruitAction a, Catalog.Catalog cat)
        {
            return Glimpse.Pick(m, p, cat, a.OfferIndex);
        }

        static SimResult Ready(PlayerState p)
        {
            if (p.HasFlag(PlayerFlags.GlimpseOpen)) return SimResult.Fail("GLIMPSE_PENDING");
            p.Ready = true;
            return SimResult.Success();
        }

        static UnitInstance TakeFrom(PlayerState p, DestLoc loc, int index)
        {
            if (loc == DestLoc.Board)
            {
                if (index < 0 || index >= p.Board.Count) return null;
                UnitInstance u = p.Board[index];
                p.Board.RemoveAt(index);
                return u;
            }
            if (loc == DestLoc.Hand)
            {
                if (index < 0 || index >= p.Hand.Count) return null;
                UnitInstance u = p.Hand[index];
                p.Hand.RemoveAt(index);
                return u;
            }
            return null;
        }
    }
}
