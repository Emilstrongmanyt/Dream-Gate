using System.Collections.Generic;
using Kindling.Sim.Model;

namespace Kindling.Sim.Catalog
{
    public static class RulesText
    {
        public static string Face(UnitDef def, UnitInstance live)
        {
            if (def == null) return "";
            if (def.Spell) return "Spell · play to cast";
            var parts = new List<string>();
            bool aw = live != null && live.Awakened;
            Keyword kw = live != null ? live.Keywords : def.Keywords;
            if (aw) parts.Add("Awakened");
            AddKw(parts, kw, Keyword.Ward, "Ward");
            AddKw(parts, kw, Keyword.Aegis, "Aegis");
            AddKw(parts, kw, Keyword.Afterglow, "Afterglow");
            AddKw(parts, kw, Keyword.Venom, "Venom");
            AddKw(parts, kw, Keyword.Latch, "Latch");
            AddTriggers(parts, def);
            if (parts.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(parts[i]);
            }
            return sb.ToString();
        }

        public static string Body(UnitDef def)
        {
            if (def == null) return "";
            if (!string.IsNullOrEmpty(def.Text)) return def.Text;
            if (def.Effects == null || def.Effects.Count == 0)
            {
                if (def.Spell) return "Play from hand to cast.";
                return "";
            }
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < def.Effects.Count; i++)
            {
                string line = EffectLine(def.Effects[i]);
                if (line.Length == 0) continue;
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(line);
            }
            if (def.OnLatchedMulN != 1 || def.OnLatchedMulD != 1)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append("Latch: ×").Append(def.OnLatchedMulN).Append("/").Append(def.OnLatchedMulD);
            }
            return sb.ToString();
        }

        static void AddKw(List<string> parts, Keyword kw, Keyword flag, string name)
        {
            if ((kw & flag) != 0) parts.Add(name);
        }

        static void AddTriggers(List<string> parts, UnitDef def)
        {
            if (def.Effects == null) return;
            for (int i = 0; i < def.Effects.Count; i++)
            {
                string n = TriggerName(def.Effects[i].Trigger);
                if (n.Length == 0) continue;
                bool hit = false;
                for (int s = 0; s < parts.Count; s++)
                    if (parts[s] == n) { hit = true; break; }
                if (!hit) parts.Add(n);
            }
        }

        static string TriggerName(Trigger t)
        {
            switch (t)
            {
                case Trigger.Arrival: return "Arrival";
                case Trigger.Echo: return "Echo";
                case Trigger.Kindle: return "Kindle";
                case Trigger.Aura: return "Aura";
                case Trigger.OnBuy: return "On buy";
                case Trigger.OnSell: return "On sell";
                case Trigger.OnReroll: return "On reroll";
                case Trigger.OnLatch: return "On latch";
                case Trigger.StartOfRecruit: return "Recruit start";
                case Trigger.EndOfRecruit: return "Recruit end";
                case Trigger.OnVenomKill: return "On Venom";
                default: return "";
            }
        }

        static string EffectLine(EffectDef fx)
        {
            if (fx == null || fx.Actions == null || fx.Actions.Count == 0) return "";
            string trig = TriggerName(fx.Trigger);
            if (trig.Length == 0) trig = fx.Trigger.ToString();
            var bits = new List<string>();
            for (int i = 0; i < fx.Actions.Count; i++)
            {
                string a = ActionLine(fx.Actions[i], fx.Filter);
                if (a.Length > 0) bits.Add(a);
            }
            if (bits.Count == 0) return trig;
            var sb = new System.Text.StringBuilder();
            sb.Append(trig).Append(": ");
            for (int i = 0; i < bits.Count; i++)
            {
                if (i > 0) sb.Append("; ");
                sb.Append(bits[i]);
            }
            return sb.ToString();
        }

        static string ActionLine(ActionDef act, FilterDef inherited)
        {
            if (act == null) return "";
            FilterDef f = act.Filter ?? inherited;
            string who = FilterWord(f);
            switch (act.Type)
            {
                case ActionType.BuffStats:
                    return Signed(act.Atk) + "/" + Signed(act.Hp) + (who.Length > 0 ? " " + who : "");
                case ActionType.BuffStatsScaled:
                    return "scale stats" + (who.Length > 0 ? " " + who : "");
                case ActionType.Summon:
                    return "summon " + IdName(act.Unit);
                case ActionType.SummonFill:
                    return "fill with " + IdName(act.Unit);
                case ActionType.SummonFromGraveyard:
                    return "return from graveyard";
                case ActionType.DealDamage:
                    return "deal " + (act.Amount > 0 ? act.Amount : 1);
                case ActionType.GrantKeyword:
                    return "grant " + (string.IsNullOrEmpty(act.Keyword) ? "keyword" : act.Keyword);
                case ActionType.GainEmbers:
                    return "+" + (act.Amount > 0 ? act.Amount : 1) + " Embers";
                case ActionType.PendingEmbers:
                    return "bank " + (act.Amount > 0 ? act.Amount : 1) + " Embers";
                case ActionType.PendingEmbersFromCounter:
                    return "bank Embers from counter";
                case ActionType.GiveCinder:
                    return "+Cinder" + (who.Length > 0 ? " " + who : "");
                case ActionType.SetEchoTimesBonus:
                    return "Echoes fire +" + (act.Amount > 0 ? act.Amount : 1);
                case ActionType.AddToHand:
                    return "add " + IdName(act.Unit) + " to hand";
                case ActionType.AddToHandFromPool:
                    return "copy from the stall pool";
                case ActionType.CopyOwnedToHand:
                    return "copy owned to hand";
                case ActionType.CopyArrival:
                    return "copy Arrival";
                case ActionType.AttachLatch:
                    return "Latch " + IdName(act.Unit);
                case ActionType.GiveEchoSummon:
                    return "give Echo (" + IdName(act.EchoUnit) + ")";
                case ActionType.Glimpse:
                    return "Glimpse";
                case ActionType.RerollStall:
                    return "Reroll stall";
                case ActionType.SetHold:
                    return "Hold stall";
                case ActionType.DamageWick:
                    return "−" + (act.Amount > 0 ? act.Amount : 1) + " Wick";
                case ActionType.SetStallSizeDelta:
                    return "stall size +" + act.Amount;
                default:
                    return "";
            }
        }

        static string FilterWord(FilterDef f)
        {
            if (f == null) return "";
            switch (f.Select)
            {
                case TargetSelect.Friendly: return "friendly";
                case TargetSelect.Enemy: return "enemy";
                case TargetSelect.Adjacent: return "adjacent";
                case TargetSelect.Other: return "another";
                case TargetSelect.All: return "all";
                case TargetSelect.RandomN: return "random";
                case TargetSelect.Leftmost: return "leftmost";
                case TargetSelect.Rightmost: return "rightmost";
                case TargetSelect.Host: return "host";
                default: return "";
            }
        }

        static string Signed(int n)
        {
            if (n > 0) return "+" + n;
            return n.ToString();
        }

        static string IdName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "a Kindled";
            int us = id.LastIndexOf('_');
            if (us >= 0 && us + 1 < id.Length) id = id.Substring(us + 1);
            if (id.Length == 0) return "a Kindled";
            return char.ToUpperInvariant(id[0]) + id.Substring(1);
        }
    }
}
