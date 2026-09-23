using System;

namespace ExpandedHordes
{
    // Pure policy shared with the small executable checks; no game state or saved data.
    internal static class HordeRules
    {
        internal static int Budget(int baseTotal, float quantityMultiplier)
        {
            if (!(quantityMultiplier > 0f) || float.IsInfinity(quantityMultiplier)) return 0;
            return (int)Math.Min(int.MaxValue, Math.Max(0d,
                Math.Round(baseTotal * (double)quantityMultiplier, MidpointRounding.ToEven)));
        }

        internal static float RunSpeedMultiplier(int percentage, bool spawning, bool daytime,
            int spawnSource, bool running, bool runAnimation) =>
            spawning && !daytime && spawnSource == 2 && running && runAnimation
                ? percentage / 100f : 1f;

        internal static int Remaining(int total, int spawned) =>
            (int)Math.Max(0L, (long)total - Math.Max(0, spawned));

        internal static int PioneerBase(int living, float quantityMultiplier) =>
            (int)Math.Min(int.MaxValue, Math.Ceiling(living / (double)quantityMultiplier));

        internal static int Region(float x, float z, float gridWidth, float biomeWidth)
        {
            double gx = Math.Round(x / gridWidth, MidpointRounding.ToEven) * gridWidth;
            double gz = Math.Round(z / gridWidth, MidpointRounding.ToEven) * gridWidth;
            return Math.Max(1, 1 + (int)Math.Floor((Math.Sqrt(gx * gx + gz * gz) - 12) / biomeWidth));
        }

        // Disjoint chances, with a 0..9999 roll giving 0.01 percentage-point
        // resolution. A disabled or capped choice keeps the original native pick.
        internal static int Category(int roll, int region, int largeBegin, int bossBegin,
            float largePercent, float bossPercent)
        {
            if (roll < 0 || roll >= 10000) return 0;
            int boss = region >= bossBegin ? BasisPoints(bossPercent) : 0;
            int large = region >= largeBegin ? BasisPoints(largePercent) : 0;
            return roll < boss ? 2 : roll < boss + large ? 1 : 0;
        }

        private static int BasisPoints(float percent) => float.IsNaN(percent) || float.IsInfinity(percent) ? 0 :
            (int)Math.Round(Math.Max(0d, Math.Min(40d, percent)) * 100d, MidpointRounding.AwayFromZero);

        internal static bool AllowsExtra(int category, bool populationKnown, int livingLarge, int livingBoss,
            int largeLimit, int bossLimit)
        {
            if (category != 1 && category != 2) return false;
            int limit = category == 2 ? bossLimit : largeLimit;
            int count = category == 2 ? livingBoss : livingLarge;
            return limit == 0 || (limit > 0 && populationKnown && count >= 0 && count < limit);
        }
    }
}
