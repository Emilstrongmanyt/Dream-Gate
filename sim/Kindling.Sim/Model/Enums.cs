using System;

namespace Kindling.Sim.Model
{
    [Flags]
    public enum Keyword : ushort
    {
        None = 0,
        Ward = 1,
        Aegis = 2,
        Afterglow = 4,
        Venom = 8,
        Latch = 16
    }

    [Flags]
    public enum PlayerFlags : uint
    {
        None = 0,
        NextRerollFree = 1,
        TycoonFreeReroll = 2,
        VesperFreeReroll = 4,
        AwakenPending = 8,
        GlimpseOpen = 16,
        UpgradedThisRecruit = 32
    }

    public enum Trigger
    {
        Arrival,
        Echo,
        Kindle,
        OnAttack,
        OnKill,
        OnDamaged,
        OnDamageDealt,
        OnVenomKill,
        OnBuy,
        OnSell,
        OnReroll,
        OnLatch,
        OnUpgrade,
        OnAwaken,
        StartOfRecruit,
        EndOfRecruit,
        Aura
    }

    public enum Duration
    {
        Permanent,
        ThisRecruit,
        ThisCombat,
        ThisMatch,
        NextRecruit
    }

    public enum TargetSelect
    {
        Self,
        Friendly,
        Enemy,
        All,
        RandomN,
        Adjacent,
        Leftmost,
        Rightmost,
        ChorusIs,
        HasKeyword,
        Host,
        BoughtUnit,
        Other,
        Source
    }

    public enum Once
    {
        Never,
        ThisRecruit,
        ThisCombat,
        ThisMatch,
        PerInstance
    }

    public enum Persist
    {
        Player,
        CombatCopy
    }

    public enum ConditionOp
    {
        Always,
        EmbersGte,
        DepthGte,
        BoughtThisRecruitGte,
        RerollsThisRecruitGte,
        HasFlag,
        ChorusIs,
        IsAwakened,
        WickGte,
        BoardCountGte,
        HandNotFull,
        SourceIsChorus
    }

    public enum ActionType
    {
        BuffStats,
        BuffStatsScaled,
        Summon,
        SummonFill,
        SummonFromGraveyard,
        DealDamage,
        GrantKeyword,
        RemoveKeyword,
        GainEmbers,
        PendingEmbers,
        PendingEmbersFromCounter,
        SetFlag,
        ClearFlag,
        ModifyCost,
        RerollStall,
        SetHold,
        AddToHand,
        AddToHandFromPool,
        CopyOwnedToHand,
        Glimpse,
        GiveCinder,
        CopyArrival,
        AttachLatch,
        GiveEchoSummon,
        DamageWick,
        SetStallSizeDelta,
        SetEchoTimesBonus,
        NoOp
    }

    public enum DepthMode
    {
        Fixed,
        Current,
        TriplePlusOne
    }

    public enum SummonPosition
    {
        Rightmost,
        Leftmost,
        SameSlot
    }

    public enum CounterKind
    {
        LatchPlaysThisMatch,
        RerollsThisRecruit,
        BoughtThisRecruit
    }

    public enum LatchHost
    {
        Humanoid,
        Any
    }

    public enum Chorus
    {
        Neutral = 0,
        Undead = 1,
        Beast = 2,
        Humanoid = 3,
        Dragon = 4,
        Spirit = 5
    }

    public enum CaptainPassive
    {
        None = 0,
        VesperFirstRerollFree,
        DebtGrantPlus1,
        DredgerNextGrantPlus2,
        KettleStallPlus1,
        CandleAwakenPlus2,
        GlassKindleLeftAegis,
        SkivBeastOnBuyPlus1Atk,
        FlintKindleRightPlus1Atk,
        NollOnBuyPendingEmber
    }

    public enum Phase
    {
        CaptainPick,
        Recruit,
        Combat,
        Placement,
        MatchOver
    }

    public enum DestLoc
    {
        Board,
        Hand,
        Stall
    }

    public enum RecruitOp
    {
        Buy,
        Sell,
        Reroll,
        Hold,
        Upgrade,
        Play,
        Reorder,
        Latch,
        Edict,
        CaptainPick,
        Ready,
        GlimpsePick
    }

    public enum CombatOp
    {
        KindleStart,
        Kindle,
        Attack,
        Damage,
        AegisBreak,
        Venom,
        Death,
        Echo,
        Summon,
        Buff,
        AuraRefresh,
        Afterglow,
        CombatEnd,
        BoardFull,
        Truncated,
        GlimpseEmpty,
        HandFull
    }

    public enum ModTag
    {
        Permanent,
        ThisCombat,
        ThisRecruit,
        Aura,
        ThisMatch,
        NextRecruit
    }
}
