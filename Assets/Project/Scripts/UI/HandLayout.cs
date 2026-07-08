namespace DreamGate.Battlegrounds.UI
{
    /// <summary>
    /// Maps hand list indices to visual slot positions (center-out fill).
    /// </summary>
    public static class HandLayout
    {
        private static readonly int[] FillOrder = { 2, 3, 1, 4, 0, 5 };

        public static int GetVisualSlotForHandIndex(int handIndex, int handCount)
        {
            if (handIndex < 0 || handIndex >= handCount || handCount > FillOrder.Length)
            {
                return -1;
            }

            return FillOrder[handIndex];
        }

        public static int GetHandIndexForVisualSlot(int visualSlot, int handCount)
        {
            for (var i = 0; i < handCount; i++)
            {
                if (GetVisualSlotForHandIndex(i, handCount) == visualSlot)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}