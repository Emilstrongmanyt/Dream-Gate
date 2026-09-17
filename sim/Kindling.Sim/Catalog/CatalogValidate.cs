using System;
using System.Collections.Generic;
using System.IO;
using Kindling.Sim.Model;

namespace Kindling.Sim.Catalog
{
    public static class CatalogValidate
    {
        static readonly HashSet<string> UnitKeys = NewSet(
            "id", "name", "chorus", "depth", "atk", "hp", "keywords", "effects",
            "awakenedEffects", "token", "echoOnSell", "afterglowKeepsKeywords",
            "latchHost", "spell", "onLatched", "copyLimit", "tokenDamageDepth",
            "text", "disabled");
        static readonly HashSet<string> CaptainKeys = NewSet(
            "id", "name", "wick", "text", "passives", "edict");
        static readonly HashSet<string> EffectKeys = NewSet(
            "trigger", "priority", "once", "persist", "when", "filter", "echoTimes", "actions");
        static readonly HashSet<string> ActionKeys = NewSet(
            "type", "atk", "hp", "duration", "unit", "count", "position", "fireArrival",
            "amount", "keyword", "flag", "depth", "depthMax", "depthMode", "echoUnit",
            "latchHost", "max", "counter", "chorus", "consume", "consumePool",
            "shopLegalOnly", "baseCatalog", "hasEcho", "filter");
        static readonly HashSet<string> WhenKeys = NewSet("op", "n", "flag", "chorus");
        static readonly HashSet<string> FilterKeys = NewSet("select", "n", "chorus", "keyword", "excludeSelf");
        static readonly HashSet<string> OnLatchedKeys = NewSet("statMulN", "statMulD");
        static readonly HashSet<string> EdictKeys = NewSet("cost", "target", "needsTarget", "actions");

        static HashSet<string> NewSet(params string[] keys)
        {
            return new HashSet<string>(keys, StringComparer.Ordinal);
        }

        public static List<string> ValidateDirectory(string contentRoot)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(contentRoot) || !Directory.Exists(contentRoot))
            {
                errors.Add("content root missing");
                return errors;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            ScanUnits(Path.Combine(contentRoot, "units"), ids, errors);
            ScanUnits(Path.Combine(contentRoot, "tokens"), ids, errors);
            ScanUnits(Path.Combine(contentRoot, "spells"), ids, errors);
            ScanCaptains(Path.Combine(contentRoot, "captains"), ids, errors);
            return errors;
        }

        static void ScanUnits(string dir, HashSet<string> ids, List<string> errors)
        {
            if (!Directory.Exists(dir)) return;
            string[] files = Directory.GetFiles(dir, "*.yaml");
            for (int i = 0; i < files.Length; i++)
            {
                string rel = files[i];
                YamlNode n = TinyYaml.Parse(File.ReadAllText(rel));
                CheckKeys(n, UnitKeys, rel, errors);
                string id = n.GetString("id");
                if (string.IsNullOrEmpty(id))
                    errors.Add(rel + ": missing id");
                else if (!ids.Add(id))
                    errors.Add(rel + ": duplicate id " + id);
                YamlNode latched = n.Get("onLatched");
                if (latched != null) CheckKeys(latched, OnLatchedKeys, rel + " onLatched", errors);
                CheckEffectList(n.Get("effects"), rel, errors);
                CheckEffectList(n.Get("awakenedEffects"), rel, errors);
            }
        }

        static void ScanCaptains(string dir, HashSet<string> ids, List<string> errors)
        {
            if (!Directory.Exists(dir)) return;
            string[] files = Directory.GetFiles(dir, "*.yaml");
            for (int i = 0; i < files.Length; i++)
            {
                string rel = files[i];
                YamlNode n = TinyYaml.Parse(File.ReadAllText(rel));
                CheckKeys(n, CaptainKeys, rel, errors);
                string id = n.GetString("id");
                if (string.IsNullOrEmpty(id))
                    errors.Add(rel + ": missing id");
                else if (!ids.Add(id))
                    errors.Add(rel + ": duplicate id " + id);
                YamlNode edict = n.Get("edict");
                if (edict != null)
                {
                    CheckKeys(edict, EdictKeys, rel + " edict", errors);
                    CheckActionList(edict.Get("actions"), rel + " edict", errors);
                }
            }
        }

        static void CheckEffectList(YamlNode list, string rel, List<string> errors)
        {
            if (list == null || list.Type != YamlNode.Kind.List || list.Items == null) return;
            for (int i = 0; i < list.Items.Count; i++)
            {
                YamlNode fx = list.Items[i];
                CheckKeys(fx, EffectKeys, rel + " effect[" + i + "]", errors);
                string trig = fx.GetString("trigger");
                if (!Enum.TryParse(trig, true, out Trigger _))
                    errors.Add(rel + " effect[" + i + "]: bad trigger " + trig);
                YamlNode when = fx.Get("when");
                if (when != null) CheckKeys(when, WhenKeys, rel + " when", errors);
                YamlNode filter = fx.Get("filter");
                if (filter != null) CheckKeys(filter, FilterKeys, rel + " filter", errors);
                CheckActionList(fx.Get("actions"), rel + " effect[" + i + "]", errors);
            }
        }

        static void CheckActionList(YamlNode list, string rel, List<string> errors)
        {
            if (list == null || list.Type != YamlNode.Kind.List || list.Items == null) return;
            for (int i = 0; i < list.Items.Count; i++)
            {
                YamlNode act = list.Items[i];
                CheckKeys(act, ActionKeys, rel + " action[" + i + "]", errors);
                string t = act.GetString("type");
                if (!Enum.TryParse(t, true, out ActionType _))
                    errors.Add(rel + " action[" + i + "]: bad type " + t);
                YamlNode filter = act.Get("filter");
                if (filter != null) CheckKeys(filter, FilterKeys, rel + " action filter", errors);
            }
        }

        static void CheckKeys(YamlNode n, HashSet<string> allowed, string rel, List<string> errors)
        {
            if (n == null || n.Type != YamlNode.Kind.Mapping) return;
            List<string> keys = n.KeyNames();
            for (int i = 0; i < keys.Count; i++)
            {
                if (!allowed.Contains(keys[i]))
                    errors.Add(rel + ": unknown key '" + keys[i] + "'");
            }
        }
    }
}
