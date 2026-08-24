using System;
using System.Collections.Generic;
using System.IO;
using Kindling.Sim.Model;

namespace Kindling.Sim.Catalog
{
    public sealed class Catalog
    {
        public string ContentVersion = "0.1.0";
        public readonly List<UnitDef> Units = new List<UnitDef>();
        public readonly List<UnitDef> Tokens = new List<UnitDef>();
        public readonly List<CaptainDef> Captains = new List<CaptainDef>();
        public SeasonDef Season = new SeasonDef();

        readonly Dictionary<string, UnitDef> _units = new Dictionary<string, UnitDef>(StringComparer.Ordinal);
        readonly Dictionary<string, CaptainDef> _captains = new Dictionary<string, CaptainDef>(StringComparer.Ordinal);

        public UnitDef GetUnit(UnitId id)
        {
            if (id.IsEmpty) return null;
            _units.TryGetValue(id.Value, out UnitDef def);
            return def;
        }

        public UnitDef GetUnit(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _units.TryGetValue(id, out UnitDef def);
            return def;
        }

        public CaptainDef GetCaptain(CaptainId id)
        {
            if (id.IsEmpty) return null;
            _captains.TryGetValue(id.Value, out CaptainDef def);
            return def;
        }

        public CaptainDef GetCaptain(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _captains.TryGetValue(id, out CaptainDef def);
            return def;
        }

        public IEnumerable<UnitDef> ShopUnits()
        {
            for (int i = 0; i < Units.Count; i++)
            {
                if (!Units[i].Token && !Units[i].Disabled) yield return Units[i];
            }
        }

        public void AddUnit(UnitDef def)
        {
            if (def == null || def.Id.IsEmpty) return;
            if (_units.ContainsKey(def.Id.Value))
                throw new InvalidOperationException("duplicate unit id " + def.Id.Value);
            _units[def.Id.Value] = def;
            if (def.Token) Tokens.Add(def);
            else Units.Add(def);
        }

        public void AddCaptain(CaptainDef def)
        {
            if (def == null || def.Id.IsEmpty) return;
            if (_captains.ContainsKey(def.Id.Value))
                throw new InvalidOperationException("duplicate captain id " + def.Id.Value);
            _captains[def.Id.Value] = def;
            Captains.Add(def);
        }

        public void Sort()
        {
            Units.Sort((a, b) => a.Id.CompareTo(b.Id));
            Tokens.Sort((a, b) => a.Id.CompareTo(b.Id));
            Captains.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        public static Catalog LoadFromDirectory(string contentRoot)
        {
            if (string.IsNullOrEmpty(contentRoot) || !Directory.Exists(contentRoot))
                throw new DirectoryNotFoundException("content root not found: " + contentRoot);

            var cat = new Catalog();
            LoadUnitsDir(cat, Path.Combine(contentRoot, "units"), token: false);
            LoadUnitsDir(cat, Path.Combine(contentRoot, "tokens"), token: true);
            LoadCaptainsDir(cat, Path.Combine(contentRoot, "captains"));
            string seasonPath = Path.Combine(contentRoot, "seasons", "none.yaml");
            if (File.Exists(seasonPath))
            {
                YamlNode n = TinyYaml.Parse(File.ReadAllText(seasonPath));
                cat.Season = new SeasonDef
                {
                    Id = n.GetString("id", "none"),
                    Name = n.GetString("name", "None")
                };
            }
            cat.Sort();
            return cat;
        }

        public static string FindContentRoot(string startDir)
        {
            string dir = startDir;
            for (int i = 0; i < 12 && !string.IsNullOrEmpty(dir); i++)
            {
                string c = Path.Combine(dir, "content");
                if (Directory.Exists(Path.Combine(c, "units")) && Directory.Exists(Path.Combine(c, "captains")))
                    return c;
                string nested = Path.Combine(dir, "content", "units");
                if (Directory.Exists(nested))
                    return Path.Combine(dir, "content");
                try { dir = Directory.GetParent(dir)?.FullName; }
                catch { break; }
            }
            return null;
        }

        static void LoadUnitsDir(Catalog cat, string dir, bool token)
        {
            if (!Directory.Exists(dir)) return;
            string[] files = Directory.GetFiles(dir, "*.yaml");
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = 0; i < files.Length; i++)
            {
                YamlNode n = TinyYaml.Parse(File.ReadAllText(files[i]));
                UnitDef def = ParseUnit(n);
                def.Token = token || def.Token;
                cat.AddUnit(def);
            }
        }

        static void LoadCaptainsDir(Catalog cat, string dir)
        {
            if (!Directory.Exists(dir)) return;
            string[] files = Directory.GetFiles(dir, "*.yaml");
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = 0; i < files.Length; i++)
            {
                YamlNode n = TinyYaml.Parse(File.ReadAllText(files[i]));
                cat.AddCaptain(ParseCaptain(n));
            }
        }

        public static UnitDef ParseUnit(YamlNode n)
        {
            var def = new UnitDef
            {
                Id = new UnitId(n.GetString("id")),
                Name = n.GetString("name"),
                Chorus = ParseChorus(n.GetString("chorus", "neutral")),
                Depth = n.GetInt("depth", 1),
                Atk = n.GetInt("atk"),
                Hp = n.GetInt("hp"),
                Token = n.GetBool("token"),
                EchoOnSell = n.GetBool("echoOnSell"),
                AfterglowKeepsKeywords = n.GetBool("afterglowKeepsKeywords"),
                LatchTransferEffects = true,
                OnLatchedMulN = 1,
                OnLatchedMulD = 1
            };
            if (n.TryGetInt("copyLimit", out int cl)) def.CopyLimitOverride = cl;
            if (n.TryGetInt("tokenDamageDepth", out int td)) def.TokenDamageDepth = td;
            List<string> kws = n.StringList("keywords");
            for (int i = 0; i < kws.Count; i++)
                def.Keywords |= ParseKeyword(kws[i]);
            string latchHost = n.GetString("latchHost");
            if (!string.IsNullOrEmpty(latchHost))
                def.LatchHost = ParseLatchHost(latchHost);
            YamlNode onLatched = n.Get("onLatched");
            if (onLatched != null && onLatched.Type == YamlNode.Kind.Mapping)
            {
                def.OnLatchedMulN = onLatched.GetInt("statMulN", 1);
                def.OnLatchedMulD = onLatched.GetInt("statMulD", 1);
                if (def.OnLatchedMulD == 0) def.OnLatchedMulD = 1;
            }
            YamlNode effects = n.Get("effects");
            if (effects != null && effects.Type == YamlNode.Kind.List)
            {
                for (int i = 0; i < effects.Items.Count; i++)
                    def.Effects.Add(ParseEffect(effects.Items[i]));
            }
            YamlNode aw = n.Get("awakenedEffects");
            if (aw != null && aw.Type == YamlNode.Kind.List)
            {
                def.AwakenedEffects = new List<EffectDef>();
                for (int i = 0; i < aw.Items.Count; i++)
                    def.AwakenedEffects.Add(ParseEffect(aw.Items[i]));
            }
            return def;
        }

        public static CaptainDef ParseCaptain(YamlNode n)
        {
            var def = new CaptainDef
            {
                Id = new CaptainId(n.GetString("id")),
                Name = n.GetString("name"),
                Wick = n.GetInt("wick", Rules.DefaultWick)
            };
            List<string> pass = n.StringList("passives");
            for (int i = 0; i < pass.Count; i++)
            {
                CaptainPassive p = ParsePassive(pass[i]);
                if (p != CaptainPassive.None) def.Passives.Add(p);
            }
            YamlNode edict = n.Get("edict");
            if (edict != null && edict.Type == YamlNode.Kind.Mapping)
            {
                def.HasEdict = true;
                def.EdictCost = edict.GetInt("cost");
                def.EdictNeedsTarget = edict.GetString("target") == "Board" || edict.GetBool("needsTarget");
                YamlNode actions = edict.Get("actions");
                if (actions != null && actions.Type == YamlNode.Kind.List)
                {
                    for (int i = 0; i < actions.Items.Count; i++)
                        def.EdictActions.Add(ParseAction(actions.Items[i]));
                }
            }
            return def;
        }

        public static EffectDef ParseEffect(YamlNode n)
        {
            var e = new EffectDef
            {
                Trigger = ParseTrigger(n.GetString("trigger")),
                Priority = n.GetInt("priority"),
                Once = ParseOnce(n.GetString("once", "Never")),
                EchoTimes = n.GetInt("echoTimes", 1)
            };
            if (e.EchoTimes < 1) e.EchoTimes = 1;
            string persist = n.GetString("persist");
            if (!string.IsNullOrEmpty(persist))
            {
                e.PersistSpecified = true;
                e.Persist = persist == "Player" ? Persist.Player : Persist.CombatCopy;
            }
            else
            {
                e.Persist = DefaultPersist(e.Trigger);
            }
            YamlNode when = n.Get("when");
            if (when != null) e.When = ParseCondition(when);
            YamlNode filter = n.Get("filter");
            if (filter != null) e.Filter = ParseFilter(filter);
            YamlNode actions = n.Get("actions");
            if (actions != null && actions.Type == YamlNode.Kind.List)
            {
                for (int i = 0; i < actions.Items.Count; i++)
                    e.Actions.Add(ParseAction(actions.Items[i]));
            }
            return e;
        }

        public static ActionDef ParseAction(YamlNode n)
        {
            var a = new ActionDef
            {
                Type = ParseActionType(n.GetString("type")),
                Unit = n.GetString("unit"),
                Keyword = n.GetString("keyword"),
                Flag = n.GetString("flag"),
                EchoUnit = n.GetString("echoUnit"),
                Chorus = n.GetString("chorus"),
                Consume = n.GetBool("consume"),
                ConsumePool = n.GetBool("consumePool"),
                ShopLegalOnly = n.GetBool("shopLegalOnly"),
                BaseCatalog = n.GetBool("baseCatalog"),
                HasEcho = n.GetBool("hasEcho"),
                FireArrival = n.GetBool("fireArrival")
            };
            if (n.TryGetInt("atk", out int atk)) { a.Atk = atk; a.AtkSpecified = true; }
            if (n.TryGetInt("hp", out int hp)) { a.Hp = hp; a.HpSpecified = true; }
            if (n.TryGetInt("amount", out int amount)) { a.Amount = amount; a.AmountSpecified = true; }
            if (n.TryGetInt("count", out int count)) { a.Count = count; a.CountSpecified = true; }
            else a.Count = 1;
            if (n.TryGetInt("depth", out int depth)) { a.Depth = depth; a.DepthSpecified = true; }
            if (n.TryGetInt("depthMax", out int depthMax)) a.DepthMax = depthMax;
            if (n.TryGetInt("max", out int max)) a.Max = max;
            string dur = n.GetString("duration");
            if (!string.IsNullOrEmpty(dur)) a.Duration = ParseDuration(dur);
            string pos = n.GetString("position");
            if (!string.IsNullOrEmpty(pos)) a.Position = ParsePosition(pos);
            string dm = n.GetString("depthMode");
            if (!string.IsNullOrEmpty(dm)) a.DepthMode = ParseDepthMode(dm);
            string counter = n.GetString("counter");
            if (!string.IsNullOrEmpty(counter)) a.Counter = ParseCounter(counter);
            string lh = n.GetString("latchHost");
            if (!string.IsNullOrEmpty(lh)) a.LatchHost = ParseLatchHost(lh);
            YamlNode filter = n.Get("filter");
            if (filter != null) a.Filter = ParseFilter(filter);
            return a;
        }

        static ConditionDef ParseCondition(YamlNode n)
        {
            var c = new ConditionDef
            {
                Op = ParseConditionOp(n.GetString("op", "Always")),
                N = n.GetInt("n"),
                Flag = n.GetString("flag"),
                Chorus = n.GetString("chorus")
            };
            return c;
        }

        static FilterDef ParseFilter(YamlNode n)
        {
            var f = new FilterDef
            {
                Select = ParseSelect(n.GetString("select", "Self")),
                Chorus = n.GetString("chorus"),
                Keyword = n.GetString("keyword"),
                ExcludeSelf = n.GetBool("excludeSelf")
            };
            if (n.TryGetInt("n", out int nn))
            {
                f.N = nn;
                f.NSpecified = true;
            }
            return f;
        }

        public static Persist DefaultPersist(Trigger t)
        {
            switch (t)
            {
                case Trigger.Kindle:
                case Trigger.OnAttack:
                case Trigger.OnKill:
                case Trigger.OnDamaged:
                case Trigger.OnDamageDealt:
                case Trigger.Aura:
                case Trigger.Echo:
                    return Persist.CombatCopy;
                default:
                    return Persist.Player;
            }
        }

        public static Chorus ParseChorus(string s)
        {
            if (s == null) return Chorus.Neutral;
            switch (s.Trim().ToLowerInvariant())
            {
                case "cinderkin": return Chorus.Cinderkin;
                case "gearwights": return Chorus.Gearwights;
                case "ashbound": return Chorus.Ashbound;
                case "gutterlings": return Chorus.Gutterlings;
                default: return Chorus.Neutral;
            }
        }

        public static Keyword ParseKeyword(string s)
        {
            if (s == null) return Keyword.None;
            switch (s.Trim())
            {
                case "Ward": return Keyword.Ward;
                case "Aegis": return Keyword.Aegis;
                case "Afterglow": return Keyword.Afterglow;
                case "Venom": return Keyword.Venom;
                case "Latch": return Keyword.Latch;
                default: return Keyword.None;
            }
        }

        public static PlayerFlags ParsePlayerFlag(string s)
        {
            if (s == null) return PlayerFlags.None;
            switch (s.Trim())
            {
                case "NextRerollFree": return PlayerFlags.NextRerollFree;
                case "TycoonFreeReroll": return PlayerFlags.TycoonFreeReroll;
                case "VesperFreeReroll": return PlayerFlags.VesperFreeReroll;
                case "AwakenPending": return PlayerFlags.AwakenPending;
                case "GlimpseOpen": return PlayerFlags.GlimpseOpen;
                case "UpgradedThisRecruit": return PlayerFlags.UpgradedThisRecruit;
                default: return PlayerFlags.None;
            }
        }

        static LatchHost ParseLatchHost(string s)
        {
            if (s != null && s.Trim().Equals("Any", StringComparison.OrdinalIgnoreCase))
                return LatchHost.Any;
            return LatchHost.Gearwights;
        }

        static Trigger ParseTrigger(string s)
        {
            if (Enum.TryParse(s, true, out Trigger t)) return t;
            throw new InvalidOperationException("unknown trigger " + s);
        }

        static Once ParseOnce(string s)
        {
            if (Enum.TryParse(s, true, out Once o)) return o;
            return Once.Never;
        }

        static ActionType ParseActionType(string s)
        {
            if (Enum.TryParse(s, true, out ActionType t)) return t;
            throw new InvalidOperationException("unknown action type " + s);
        }

        static Duration ParseDuration(string s)
        {
            if (Enum.TryParse(s, true, out Duration d)) return d;
            return Duration.Permanent;
        }

        static SummonPosition ParsePosition(string s)
        {
            if (Enum.TryParse(s, true, out SummonPosition p)) return p;
            return SummonPosition.Rightmost;
        }

        static DepthMode ParseDepthMode(string s)
        {
            if (Enum.TryParse(s, true, out DepthMode d)) return d;
            return DepthMode.Fixed;
        }

        static CounterKind ParseCounter(string s)
        {
            if (Enum.TryParse(s, true, out CounterKind c)) return c;
            return CounterKind.RerollsThisRecruit;
        }

        static ConditionOp ParseConditionOp(string s)
        {
            if (Enum.TryParse(s, true, out ConditionOp o)) return o;
            return ConditionOp.Always;
        }

        static TargetSelect ParseSelect(string s)
        {
            if (Enum.TryParse(s, true, out TargetSelect t)) return t;
            return TargetSelect.Self;
        }

        static CaptainPassive ParsePassive(string s)
        {
            if (Enum.TryParse(s, true, out CaptainPassive p)) return p;
            return CaptainPassive.None;
        }
    }
}
