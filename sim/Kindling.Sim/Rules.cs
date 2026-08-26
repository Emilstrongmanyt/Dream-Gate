namespace Kindling.Sim
{
    public static class Rules
    {
        public const int LobbySize = 8;
        public const int DefaultWick = 30;
        public const int BoardMax = 7;
        public const int HandMax = 10;
        public const int BuyCost = 3;
        public const int SellReward = 1;
        public const int RerollCost = 1;
        public const int EmbersCeiling = 20;
        public const int MinDepth = 1;
        public const int MaxDepth = 6;
        public const int RoundCap = 20;
        public const int LifetimeSummonCap = 32;
        public const int DeathWaveCap = 64;
        public const int MaxStall = 7;
        public const int CaptainOfferCount = 3;
        public const int GlimpseOfferCount = 3;
        public const int DummyGhostCount = 3;
        public const int CaptainPickSeconds = 20;
        public const int CombatPlaybackCapSeconds = 12;
        public const int CombatAutoContinueSeconds = 2;

        /// <summary>Round 1 is 15s; round 5 and later are 60s. Linear in between.</summary>
        public static int RecruitSeconds(int round)
        {
            if (round < 1) round = 1;
            if (round >= 5) return 60;
            return 15 + ((round - 1) * 45) / 4;
        }

        public static readonly int[] StallSizeByDepth = { 0, 3, 3, 4, 4, 5, 6 };
        public static readonly int[] CopyLimitByDepth = { 0, 16, 15, 13, 11, 9, 7 };
        public static readonly int[] UpgradeBaseCost = { 0, 5, 7, 8, 9, 11 };

        public static int StallSize(int depth, int stallSizeDelta)
        {
            if (depth < 1) depth = 1;
            if (depth > MaxDepth) depth = MaxDepth;
            int size = StallSizeByDepth[depth] + stallSizeDelta;
            if (size < 1) size = 1;
            if (size > MaxStall) size = MaxStall;
            return size;
        }

        public static int CopyLimit(int depth)
        {
            if (depth < 1) depth = 1;
            if (depth > MaxDepth) depth = MaxDepth;
            return CopyLimitByDepth[depth];
        }

        public static int UpgradeCostBase(int currentDepth)
        {
            if (currentDepth < 1) currentDepth = 1;
            if (currentDepth >= MaxDepth) return 0;
            return UpgradeBaseCost[currentDepth];
        }

        public static int ClampEmbers(int embers)
        {
            if (embers < 0) return 0;
            if (embers > EmbersCeiling) return EmbersCeiling;
            return embers;
        }
    }
}
