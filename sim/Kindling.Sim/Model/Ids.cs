using System;

namespace Kindling.Sim.Model
{
    public readonly struct UnitId : IEquatable<UnitId>, IComparable<UnitId>
    {
        public readonly string Value;

        public UnitId(string value)
        {
            Value = value ?? "";
        }

        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public int CompareTo(UnitId other)
        {
            return string.CompareOrdinal(Value, other.Value);
        }

        public bool Equals(UnitId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is UnitId id && Equals(id);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? "";
        }

        public static bool operator ==(UnitId a, UnitId b) => a.Equals(b);
        public static bool operator !=(UnitId a, UnitId b) => !a.Equals(b);
    }

    public readonly struct CaptainId : IEquatable<CaptainId>, IComparable<CaptainId>
    {
        public readonly string Value;

        public CaptainId(string value)
        {
            Value = value ?? "";
        }

        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public int CompareTo(CaptainId other)
        {
            return string.CompareOrdinal(Value, other.Value);
        }

        public bool Equals(CaptainId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CaptainId id && Equals(id);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? "";
        }

        public static bool operator ==(CaptainId a, CaptainId b) => a.Equals(b);
        public static bool operator !=(CaptainId a, CaptainId b) => !a.Equals(b);
    }

    public sealed class EdictState
    {
        public bool UsedThisRecruit;
        public bool Repeatable;
    }

    public sealed class Modifier
    {
        public ModTag Tag;
        public int Atk;
        public int Hp;
        public Keyword Keywords;
        public bool FromAura;
    }

    public sealed class LatchAttachment
    {
        public UnitId CatalogId;
        public int Atk;
        public int Hp;
        public Keyword Keywords;
    }

    public sealed class PoolEntry
    {
        public UnitId Id;
        public int Remaining;
    }

    public sealed class GlimpseOffer
    {
        public int Depth;
        public UnitId[] Choices;
    }

    public sealed class Pairing
    {
        public int PairIndex;
        public int SeatA;
        public int SeatB;
        public bool Ghost;
    }

    public sealed class SimResult
    {
        public bool Ok;
        public string Code;

        public static SimResult Success()
        {
            return new SimResult { Ok = true, Code = null };
        }

        public static SimResult Fail(string code)
        {
            return new SimResult { Ok = false, Code = code };
        }
    }

    public sealed class RecruitAction
    {
        public RecruitOp Op;
        public int Seat;
        public int Seq;
        public int StallIndex;
        public DestLoc Dest;
        public int DestIndex;
        public DestLoc Loc;
        public int Index;
        public bool Held;
        public int[] BoardPerm;
        public int HandIndex;
        public DestLoc From;
        public int FromIndex;
        public int HostIndex;
        public int OfferIndex = -1;
        public int TargetIndex = -1;
        public string CaptainId;
    }

    public sealed class CombatEvent
    {
        public int Step;
        public CombatOp Op;
        public int SrcSeat;
        public int DstSeat;
        public ulong SrcInstance;
        public ulong DstInstance;
        public int SrcSlot;
        public int DstSlot;
        public int Amount;
        public int Atk;
        public int HpAfter;
        public string CatalogId;
        public string Note;
    }

    public sealed class CombatPiece
    {
        public ulong InstanceId;
        public UnitId CatalogId;
        public int Atk;
        public int Hp;
        public int MaxHp;
        public Keyword Keywords;
        public bool Awakened;
        public int Seat;

        public UnitInstance ToUnit()
        {
            return new UnitInstance
            {
                InstanceId = InstanceId,
                CatalogId = CatalogId,
                Atk = Atk,
                Hp = Hp,
                MaxHp = MaxHp > 0 ? MaxHp : Hp,
                Keywords = Keywords,
                Awakened = Awakened,
                CombatSeat = Seat,
                AttackCharges = 1
            };
        }
    }

    public sealed class CombatResult
    {
        public int WinnerSeat = -1;
        public bool Draw;
        public int Damage;
        public int SeatA;
        public int SeatB;
        public string NameA = "";
        public string NameB = "";
        public int DepthA;
        public int DepthB;
        public int WickA;
        public int WickB;
        public System.Collections.Generic.List<CombatPiece> BoardA =
            new System.Collections.Generic.List<CombatPiece>();
        public System.Collections.Generic.List<CombatPiece> BoardB =
            new System.Collections.Generic.List<CombatPiece>();
        public System.Collections.Generic.List<CombatEvent> Events =
            new System.Collections.Generic.List<CombatEvent>();
        public int RemainingA;
        public int RemainingB;
    }

    public sealed class GraveRecord
    {
        public UnitInstance Snapshot;
        public int Seat;
        public int Order;
        public bool HasEcho;
    }
}
