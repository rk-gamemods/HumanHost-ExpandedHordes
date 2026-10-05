using System;

namespace ExpandedHordes
{
    // Policy only; the runtime supplies live membership and makes native calls.
    internal static class RefocusRules
    {
        internal const float Interval = 5f;
        internal const int GraceTicks = 2;
        internal const int CallLimit = 16;

        internal static bool TickDue(float scaledTime, ref float nextTick)
        {
            if (scaledTime < nextTick) return false;
            nextTick = scaledTime + Interval;
            return true;
        }

        internal static bool TickAllowed(bool night, int livingCount, bool playerAlive,
            bool quitting, bool sorting) => night && livingCount > 0 && playerAlive && !quitting && !sorting;

        internal static int IdleTicks(int previous, bool active, float hp, bool ragdolled, bool hasFocus) =>
            active && hp > 0f && !ragdolled && !hasFocus ? Math.Min(GraceTicks, previous + 1) : 0;

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
