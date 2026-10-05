using System;

namespace ExpandedHordes
{
    internal struct RefocusSample
    {
        internal int IdleTicks;
        internal float X, Z;
        internal bool HasPosition;
    }

    // Policy only; the runtime supplies live membership and makes native calls.
    internal static class RefocusRules
    {
        internal const float Interval = 5f;
        internal const int GraceTicks = 9;
        internal const int CallLimit = 16;

        internal static bool TickDue(float scaledTime, ref float nextTick)
        {
            if (scaledTime < nextTick) return false;
            nextTick = scaledTime + Interval;
            return true;
        }

        internal static bool TickAllowed(bool night, int livingCount, bool playerAlive,
            bool quitting, bool sorting, bool adminIgnore) =>
            night && livingCount > 0 && playerAlive && !quitting && !sorting && !adminIgnore;

        internal static void Observe(ref RefocusSample sample, bool active, float hp,
            bool ragdolled, bool hasFocus, float x, float z)
        {
            float dx = x - sample.X, dz = z - sample.Z;
            bool stationary = sample.HasPosition && dx * dx + dz * dz < 1f;
            sample.IdleTicks = active && hp > 0f && !ragdolled && !hasFocus && stationary
                ? Math.Min(GraceTicks, sample.IdleTicks + 1) : 0;
            sample.X = x;
            sample.Z = z;
            sample.HasPosition = true;
        }

        internal static bool TryRefocus(ref int idleTicks, ref int calls)
        {
            if (idleTicks < GraceTicks || calls >= CallLimit) return false;
            calls++;
            idleTicks = 0; // Even a vetoed broadcast gets another grace period.
            return true;
        }

        internal static int NextIndex(int index, int count) => (index + 1) % count;
    }
}
