using System.Collections.Generic;
using Kindling.Sim.Model;

namespace Kindling.Sim.Match
{
    public sealed class PairResult
    {
        public List<Pairing> Pairs = new List<Pairing>();
        public int? GhostSeat;
    }

    public static class Pairings
    {
        public const int Bye = -1;

        public static PairResult Pair(List<int> livingSeats, int round)
        {
            var result = new PairResult();
            if (livingSeats == null || livingSeats.Count <= 1)
                return result;

            var work = new List<int>(livingSeats.Count + 1);
            for (int i = 0; i < livingSeats.Count; i++)
                work.Add(livingSeats[i]);
            int n = work.Count;
            if ((n & 1) == 1)
            {
                work.Add(Bye);
                n++;
            }
            int rot = (round - 1) % (n - 1);
            var rest = new int[n - 1];
            for (int i = 1; i < work.Count; i++)
                rest[i - 1] = work[i];
            RotateRight(rest, rot);
            var circle = new int[n];
            circle[0] = work[0];
            for (int i = 0; i < rest.Length; i++)
                circle[i + 1] = rest[i];

            int? ghost = null;
            var pairs = new List<Pairing>();
            for (int i = 0; i < n / 2; i++)
            {
                int a = circle[i];
                int b = circle[n - 1 - i];
                if (a == Bye) { ghost = b; continue; }
                if (b == Bye) { ghost = a; continue; }
                int lo = a < b ? a : b;
                int hi = a < b ? b : a;
                pairs.Add(new Pairing { SeatA = lo, SeatB = hi });
            }
            pairs.Sort((x, y) =>
            {
                int c = x.SeatA.CompareTo(y.SeatA);
                return c != 0 ? c : x.SeatB.CompareTo(y.SeatB);
            });
            for (int i = 0; i < pairs.Count; i++)
                pairs[i].PairIndex = i;
            result.Pairs = pairs;
            result.GhostSeat = ghost;
            return result;
        }

        public static void RotateRight(int[] arr, int k)
        {
            if (arr == null || arr.Length == 0) return;
            int n = arr.Length;
            k %= n;
            if (k < 0) k += n;
            if (k == 0) return;
            var tmp = new int[n];
            for (int i = 0; i < n; i++)
                tmp[(i + k) % n] = arr[i];
            for (int i = 0; i < n; i++)
                arr[i] = tmp[i];
        }

        public static int[] RotateRightCopy(int[] arr, int k)
        {
            var c = (int[])arr.Clone();
            RotateRight(c, k);
            return c;
        }
    }
}
