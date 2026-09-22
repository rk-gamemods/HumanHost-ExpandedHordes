using HarmonyLib;
using UnityEngine;

namespace ExpandedHordes
{
    internal static class PlacementLog
    {
        internal static void Reset() { }
        internal static void Flush() { }
        internal static void Position(bool failure)
        {
            PerformanceMonitor.Record(TelemetryEvent.Placement);
            if (failure) PerformanceMonitor.Record(TelemetryEvent.PlacementFailed);
        }
        internal static void Context(bool success)
        {
            PerformanceMonitor.Record(TelemetryEvent.Context);
            if (!success) PerformanceMonitor.Record(TelemetryEvent.ContextFailed);
        }
    }
    [HarmonyPatch(typeof(NPC_Horde_Mgr), "GetValidSpawnPosition")]
    internal static class PlacementResult
    {
        private static void Postfix(Vector3 __result, int maxAttempts)
        {
            if (!PerformanceMonitor.OnMain) return;
            // Sentinel failure is authoritative. Distance rejection happens at the
            // caller, so this metric deliberately counts placement-call failures only.
            PlacementLog.Position(__result.y == -10000f);
        }
    }
    [HarmonyPatch(typeof(NPC_Horde_Mgr), "Try_Get_Spawn_Context")]
    internal static class ContextResult
    {
        private static void Postfix(bool __result) => PlacementLog.Context(__result);
    }
}
