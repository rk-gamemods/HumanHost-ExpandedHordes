using System;

namespace ExpandedHordes
{
    internal static class ArrivalRules
    {
        internal const float NativeWaitSeconds = 0.1f;
        internal static float WaitSeconds(int percentage) => NativeWaitSeconds * 100 / Math.Max(100, Math.Min(400, percentage));
        internal static int Directions(int directions) => Math.Max(1, Math.Min(4, directions));
        internal static float SectorAngle(int sector, int directions) => sector * 360f / Directions(directions);

        // Positive angles follow Unity's rotation about the vertical axis.
        // Only the center's horizontal offset changes; callers retain height and radius.
        internal static void Rotate(float x, float z, int sector, int directions, out float rotatedX, out float rotatedZ)
        {
            double radians = SectorAngle(sector, directions) * Math.PI / 180;
            double sin = Math.Sin(radians), cos = Math.Cos(radians);
            rotatedX = (float)(x * cos + z * sin);
            rotatedZ = (float)(z * cos - x * sin);
        }

        internal static bool PlacementFailed(float y, float offsetX, float offsetZ) =>
            y == -10000f || offsetX * offsetX + offsetZ * offsetZ < 1600f;
    }

    internal sealed class ArrivalSectors
    {
        private readonly int directions;
        private readonly int[] failures = new int[4];
        private readonly float[] coolingUntil = new float[4];
        private int next;

        internal ArrivalSectors(int directions) { this.directions = ArrivalRules.Directions(directions); }

        internal int Select(float now)
        {
            // At most four fixed comparisons, no search loop or per-request allocation.
            int sector = next;
            if (Ready(sector, now)) return sector;
            sector = (sector + 1) % directions;
            if (Ready(sector, now)) return sector;
            sector = (sector + 1) % directions;
            if (Ready(sector, now)) return sector;
            sector = (sector + 1) % directions;
            return Ready(sector, now) ? sector : 0;
        }

        private bool Ready(int sector, float now) => now >= coolingUntil[sector];

        internal void Record(int sector, bool success, float now)
        {
            if (success)
            {
                failures[sector] = 0;
                next = (sector + 1) % directions;
            }
            else if (++failures[sector] >= 3)
            {
                failures[sector] = 0;
                coolingUntil[sector] = now + 10f;
            }
        }
    }
}
