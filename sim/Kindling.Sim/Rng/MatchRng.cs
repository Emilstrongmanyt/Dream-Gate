using System;
using System.Collections.Generic;

namespace Kindling.Sim.Rng
{
    public struct Pcg32State
    {
        public uint S0;
        public uint S1;

        public ulong Pack()
        {
            return ((ulong)S1 << 32) | S0;
        }

        public static Pcg32State From(ulong state)
        {
            Pcg32State s;
            s.S0 = (uint)state;
            s.S1 = (uint)(state >> 32);
            return s;
        }
    }

    public sealed class MatchRng
    {
        public enum Stream : byte
        {
            Stall = 1,
            Combat = 2,
            Pair = 3,
            Glimpse = 4,
            CaptainOffer = 5,
            TieBreak = 6,
            Bot = 7,
            Recruit = 8
        }

        public const int StreamCount = 8;
        public const ulong Multiplier = 6364136223846793005UL;

        public Pcg32State[] States;
        public ulong NextInstanceId;

        public MatchRng()
        {
            States = new Pcg32State[StreamCount + 1];
            NextInstanceId = 1;
        }

        public MatchRng(ulong fixtureSeed) : this()
        {
            for (byte s = 1; s <= StreamCount; s++)
                SeedStream((Stream)s, fixtureSeed);
            NextInstanceId = 1;
        }

        public static MatchRng Create(Guid matchId, uint salt)
        {
            var rng = new MatchRng();
            for (byte s = 1; s <= StreamCount; s++)
            {
                ulong h = Fnv1a64.HashMatchSeed(matchId, salt, s);
                rng.SeedStream((Stream)s, h);
            }
            rng.NextInstanceId = 1;
            return rng;
        }

        public void SeedStream(Stream stream, ulong seed)
        {
            int i = (int)stream;
            ulong inc = IncrementFor(stream);
            ulong state = 0;
            state = Step(state, inc);
            state += seed;
            state = Step(state, inc);
            States[i] = Pcg32State.From(state);
        }

        public static ulong IncrementFor(Stream stream)
        {
            return (((ulong)(byte)stream) << 1) | 1UL;
        }

        public uint NextU32(Stream stream)
        {
            int i = (int)stream;
            ulong inc = IncrementFor(stream);
            ulong old = States[i].Pack();
            ulong next = Step(old, inc);
            States[i] = Pcg32State.From(next);
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        public int Range(Stream stream, int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
                return minInclusive;
            uint bound = (uint)(maxExclusive - minInclusive);
            uint threshold = (uint)((0x100000000UL - bound) % bound);
            uint r;
            do
            {
                r = NextU32(stream);
            } while (r < threshold);
            return minInclusive + (int)(r % bound);
        }

        public bool Bit(Stream stream)
        {
            return (NextU32(stream) & 1u) != 0;
        }

        public void Shuffle<T>(Stream stream, IList<T> list)
        {
            if (list == null) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(stream, 0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        public ulong NextId()
        {
            ulong id = NextInstanceId;
            NextInstanceId = id + 1;
            return id;
        }

        public MatchRng Clone()
        {
            var c = new MatchRng();
            c.NextInstanceId = NextInstanceId;
            for (int i = 0; i < States.Length; i++)
                c.States[i] = States[i];
            return c;
        }

        static ulong Step(ulong state, ulong inc)
        {
            return state * Multiplier + inc;
        }
    }

    public static class Fnv1a64
    {
        public const ulong Offset = 14695981039346656037UL;
        public const ulong Prime = 1099511628211UL;

        public static ulong Hash(byte[] data)
        {
            ulong h = Offset;
            if (data == null) return h;
            for (int i = 0; i < data.Length; i++)
            {
                h ^= data[i];
                h *= Prime;
            }
            return h;
        }

        public static ulong HashMatchSeed(Guid matchId, uint salt, byte stream)
        {
            byte[] bytes = new byte[16 + 4 + 1 + 1 + 1];
            CanonicalGuid(matchId, bytes, 0);
            bytes[16] = (byte)salt;
            bytes[17] = (byte)(salt >> 8);
            bytes[18] = (byte)(salt >> 16);
            bytes[19] = (byte)(salt >> 24);
            bytes[20] = stream;
            bytes[21] = 0;
            bytes[22] = 0;
            return Hash(bytes);
        }

        public static void CanonicalGuid(Guid id, byte[] dest, int offset)
        {
            string hex = id.ToString("N");
            for (int i = 0; i < 16; i++)
            {
                dest[offset + i] = (byte)((Nibble(hex[i * 2]) << 4) | Nibble(hex[i * 2 + 1]));
            }
        }

        static int Nibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return 0;
        }
    }
}
