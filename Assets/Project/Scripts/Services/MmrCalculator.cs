using UnityEngine;

namespace DreamGate.Battlegrounds.Services
{
    public static class MmrCalculator
    {
        private static readonly int[] PlacementDelta =
        {
            0, 40, 20, 10, 0, -10, -20, -30, -40
        };

        public static int CalculateDelta(int placement, int currentMmr)
        {
            var index = Mathf.Clamp(placement, 1, PlacementDelta.Length - 1);
            var baseDelta = PlacementDelta[index];
            var mmrFactor = currentMmr < 1200 ? 1.2f : currentMmr > 1800 ? 0.85f : 1f;
            return Mathf.RoundToInt(baseDelta * mmrFactor);
        }
    }
}