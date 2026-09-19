using System;

namespace Core.Rewards
{
    public static class CorruptionTierCalculator
    {
        public static int GetTier(double corruptionValue)
        {
            var clampedFloor = Math.Max(0, corruptionValue);
            if (clampedFloor <= 10)
            {
                return 0;
            }
            if (clampedFloor <= 20)
            {
                return 1;
            }
            if (clampedFloor <= 30)
            {
                return 2;
            }
            if (clampedFloor <= 40)
            {
                return 3;
            }
            return 4;
        }
    }
}