using System.Collections.Generic;
using Kindling.Sim.Model;

namespace Kindling.Sim.Catalog
{
    public sealed class ConditionDef
    {
        public ConditionOp Op = ConditionOp.Always;
        public int N;
        public string Flag;
        public string Chorus;
    }

    public sealed class FilterDef
    {
        public TargetSelect Select = TargetSelect.Self;
        public int N = 1;
        public bool NSpecified;
        public string Chorus;
        public string Keyword;
        public bool ExcludeSelf;
    }

    public sealed class ActionDef
    {
        public ActionType Type;
        public int Atk;
        public int Hp;
        public Duration Duration = Duration.Permanent;
        public string Unit;
        public int Count = 1;
        public SummonPosition Position = SummonPosition.Rightmost;
        public bool FireArrival;
        public int Amount;
        public string Keyword;
        public string Flag;
        public int Depth;
        public int DepthMax;
        public DepthMode DepthMode = DepthMode.Fixed;
        public string EchoUnit;
        public LatchHost LatchHost = LatchHost.Gearwights;
        public int Max;
        public CounterKind Counter;
        public string Chorus;
        public bool Consume;
        public bool ConsumePool;
        public bool ShopLegalOnly;
        public bool BaseCatalog;
        public bool HasEcho;
        public FilterDef Filter;
        public bool AtkSpecified;
        public bool HpSpecified;
        public bool AmountSpecified;
        public bool CountSpecified;
        public bool DepthSpecified;
    }

    public sealed class EffectDef
    {
        public Trigger Trigger;
        public int Priority;
        public Once Once = Once.Never;
        public Persist Persist;
        public bool PersistSpecified;
        public ConditionDef When;
        public FilterDef Filter;
        public int EchoTimes = 1;
        public List<ActionDef> Actions = new List<ActionDef>();
        public string OnceKey;
    }

    public sealed class UnitDef
    {
        public UnitId Id;
        public string Name;
        public Chorus Chorus;
        public int Depth = 1;
        public int Atk;
        public int Hp;
        public Keyword Keywords;
        public List<EffectDef> Effects = new List<EffectDef>();
        public List<EffectDef> AwakenedEffects;
        public bool Token;
        public bool EchoOnSell;
        public bool AfterglowKeepsKeywords;
        public LatchHost LatchHost = LatchHost.Gearwights;
        public int OnLatchedMulN = 1;
        public int OnLatchedMulD = 1;
        public bool LatchTransferEffects = true;
        public int CopyLimitOverride;
        public int TokenDamageDepth;
        public bool Disabled;

        public int CopyLimit => CopyLimitOverride > 0 ? CopyLimitOverride : Rules.CopyLimit(Depth);
    }

    public sealed class CaptainDef
    {
        public CaptainId Id;
        public string Name;
        public int Wick = Rules.DefaultWick;
        public List<CaptainPassive> Passives = new List<CaptainPassive>();
        public int EdictCost;
        public bool EdictNeedsTarget;
        public List<ActionDef> EdictActions = new List<ActionDef>();
        public bool HasEdict;
    }

    public sealed class SeasonDef
    {
        public string Id = "none";
        public string Name = "None";
    }
}
