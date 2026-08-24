using System.Collections.Generic;
using Kindling.Sim.Match;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class PairingTests
    {
        [Fact]
        public void N7_bye_table_ghosts_0_5_3_1_6_4_2()
        {
            var living = new List<int> { 0, 1, 2, 3, 4, 5, 6 };
            int[] expected = { 0, 5, 3, 1, 6, 4, 2 };
            var counts = new int[7];
            for (int round = 1; round <= 7; round++)
            {
                PairResult r = Pairings.Pair(living, round);
                Assert.True(r.GhostSeat.HasValue);
                Assert.Equal(expected[round - 1], r.GhostSeat.Value);
                counts[r.GhostSeat.Value]++;
                Assert.Equal(3, r.Pairs.Count);
            }
            int min = 99, max = 0;
            for (int i = 0; i < 7; i++)
            {
                Assert.Equal(1, counts[i]);
                if (counts[i] < min) min = counts[i];
                if (counts[i] > max) max = counts[i];
            }
            Assert.True(max - min <= 1);
        }

        [Fact]
        public void N8_round1_pairs()
        {
            var living = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
            PairResult r = Pairings.Pair(living, 1);
            Assert.Null(r.GhostSeat);
            Assert.Equal(4, r.Pairs.Count);
            Assert.Equal(0, r.Pairs[0].SeatA);
            Assert.Equal(7, r.Pairs[0].SeatB);
            Assert.Equal(1, r.Pairs[1].SeatA);
            Assert.Equal(6, r.Pairs[1].SeatB);
            Assert.Equal(2, r.Pairs[2].SeatA);
            Assert.Equal(5, r.Pairs[2].SeatB);
            Assert.Equal(3, r.Pairs[3].SeatA);
            Assert.Equal(4, r.Pairs[3].SeatB);
        }

        [Fact]
        public void RotateRight_example()
        {
            int[] arr = { 1, 2, 3, 4, 5, 6, Pairings.Bye };
            Pairings.RotateRight(arr, 1);
            Assert.Equal(new[] { Pairings.Bye, 1, 2, 3, 4, 5, 6 }, arr);
        }
    }
}
