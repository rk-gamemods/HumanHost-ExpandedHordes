using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExpandedHordes
{
    internal static class HordeArrival
    {
        internal static MethodInfo SpawnMethod, PlacementMethod;
        internal static Type IteratorType;
        internal static bool RateEnabled, DirectionsEnabled;
        internal static int DirectionCount;
        private static AccessTools.FieldRef<WaitForSeconds, float> seconds;
        private static WaitForSeconds replacementWait;
        internal static NativeIterator Advancing;

        internal static void Install()
        {
            int rate = ModSettings.SpawnRatePercent.Value, directions = ModSettings.ArrivalDirections.Value;
            if (rate == 100 && directions == 1) return;
            ArrivalShape shape = default;
            string failure = "";
            try
            {
                SpawnMethod = AccessTools.Method(typeof(NPC_Horde_Mgr), "Spawn_Horde_NPCs", Type.EmptyTypes);
                PlacementMethod = AccessTools.Method(typeof(NPC_Horde_Mgr), "GetValidSpawnPosition",
                    new[] { typeof(Vector3), typeof(float), typeof(float), typeof(int) });
                if (SpawnMethod == null || SpawnMethod.ReturnType != typeof(IEnumerator))
                    throw new MissingMethodException("Spawn_Horde_NPCs(): IEnumerator");
                MethodInfo moveNext = AccessTools.EnumeratorMoveNext(SpawnMethod);
                if (moveNext == null) throw new MissingMethodException("Spawn_Horde_NPCs compiler-generated MoveNext");
                IteratorType = moveNext.DeclaringType;
                // The installed Harmony references mscorlib's ILGenerator in
                // this signature. Invoke its reader without adding that legacy
                // assembly as a compile dependency to our netstandard target.
                var reader = typeof(PatchProcessor).GetMethods().Single(m => m.Name == "GetOriginalInstructions" &&
                    m.GetParameters().Length == 2 && !m.GetParameters()[1].ParameterType.IsByRef);
                var original = (IEnumerable<CodeInstruction>)reader.Invoke(null, new object[] { moveNext, null });
                var instructions = original
                    .Select(i => new ArrivalInstruction(i.opcode, i.operand is MethodBase method ? Describe(method) : i.operand)).ToArray();
                shape = ArrivalShape.Read(instructions,
                    PlacementMethod != null && PlacementMethod.ReturnType == typeof(Vector3) ? Describe(PlacementMethod) : null);
            }
            catch (Exception ex) { failure = " (" + ex.GetType().Name + ": " + ex.Message + ")"; }

            RateEnabled = shape.RateEnabled(rate);
            DirectionsEnabled = shape.DirectionsEnabled(directions);
            if (rate != 100 && !RateEnabled)
                FeatureRuntime.WarnOnce("arrival-rate-shape", "Arrival rate disabled: Spawn_Horde_NPCs.MoveNext did not match one WaitForSeconds(float) fed by ldc.r4 0.1." + failure);
            if (directions != 1 && !DirectionsEnabled)
                FeatureRuntime.WarnOnce("arrival-directions-shape", "Arrival directions disabled: GetValidSpawnPosition signature, the two MoveNext calls with attempts 1000 and 100, or native rejection constants -10000 and 1600 did not match." + failure);
            if (!RateEnabled && !DirectionsEnabled) return;

            DirectionCount = ArrivalRules.Directions(directions);
            if (RateEnabled)
            {
                try
                {
                    seconds = AccessTools.FieldRefAccess<WaitForSeconds, float>("m_Seconds");
                    replacementWait = new WaitForSeconds(ArrivalRules.WaitSeconds(rate));
                }
                catch (Exception)
                {
                    seconds = null;
                    FeatureRuntime.WarnOnce("arrival-wait-field", "Arrival rate unchanged: WaitForSeconds.m_Seconds field ref is unavailable; native waits pass through.");
                }
            }
            FeatureRuntime.Install(Feature.Arrival, typeof(ArrivalIteratorPatch), typeof(ArrivalPlacementPatch));
        }

        private static ArrivalMethod Describe(MethodBase method) => new ArrivalMethod(
            method.DeclaringType.Name, method.Name, method is MethodInfo info ? info.ReturnType.Name : "Void",
            method.IsStatic, method.GetParameters().Select(p => p.ParameterType.Name).ToArray());

        internal sealed class NativeIterator : IEnumerator, IDisposable
        {
            private readonly IEnumerator inner;
            internal readonly NPC_Horde_Mgr Manager;
            internal readonly ArrivalSectors Sectors;
            private Transform player;
            private bool started;
            internal Vector3 PlayerPosition => player.position;

            internal NativeIterator(NPC_Horde_Mgr manager, IEnumerator inner)
            {
                Manager = manager;
                this.inner = inner;
                if (DirectionsEnabled) Sectors = new ArrivalSectors(DirectionCount);
            }

            public bool MoveNext()
            {
                var previous = Advancing;
                Advancing = this;
                try
                {
                    if (!started)
                    {
                        started = true;
                        // Native MoveNext captures this transform once, then reads
                        // its current position on every placement request.
                        if (DirectionsEnabled) player = Player_Input.ins.transform;
                    }
                    return inner.MoveNext();
                }
                finally { Advancing = previous; }
            }

            public object Current
            {
                get
                {
                    object value = inner.Current;
                    if (!FeatureRuntime.Enabled(Feature.Arrival) || !RateEnabled || seconds == null || !(value is WaitForSeconds wait)) return value;
                    try { return seconds(wait) == ArrivalRules.NativeWaitSeconds ? replacementWait : value; }
                    catch (Exception)
                    {
                        seconds = null;
                        FeatureRuntime.WarnOnce("arrival-wait-field", "Arrival rate unchanged: WaitForSeconds.m_Seconds could not be read; native waits pass through.");
                        return value;
                    }
                }
            }

            public void Reset() => inner.Reset();
            public void Dispose() { if (inner is IDisposable disposable) disposable.Dispose(); }
        }
    }

    [HarmonyPatch]
    internal static class ArrivalIteratorPatch
    {
        private static bool Prepare() => HordeArrival.RateEnabled || HordeArrival.DirectionsEnabled;
        private static MethodBase TargetMethod() => HordeArrival.SpawnMethod;
        private static void Postfix(NPC_Horde_Mgr __instance, ref IEnumerator __result)
        {
            if (!FeatureRuntime.Enabled(Feature.Arrival) || __result == null) return;
            if (__result.GetType() != HordeArrival.IteratorType)
            {
                FeatureRuntime.WarnOnce("arrival-replaced-iterator", "Another mod replaced the horde spawn iterator; Arrival is inactive for it.");
                return;
            }
            __result = new HordeArrival.NativeIterator(__instance, __result);
        }
    }

    [HarmonyPatch]
    internal static class ArrivalPlacementPatch
    {
        internal struct PlacementState
        {
            internal HordeArrival.NativeIterator Iterator;
            internal Vector3 Player;
            internal int Sector;
        }
        private static bool Prepare() => HordeArrival.DirectionsEnabled;
        private static MethodBase TargetMethod() => HordeArrival.PlacementMethod;

        private static void Prefix(NPC_Horde_Mgr __instance, ref Vector3 startPos, float minSpawnDis, int maxAttempts,
            out PlacementState __state)
        {
            __state = default;
            var iterator = HordeArrival.Advancing;
            if (!FeatureRuntime.Enabled(Feature.Arrival) || !HordeArrival.DirectionsEnabled || iterator == null ||
                !ReferenceEquals(iterator.Manager, __instance) || maxAttempts != 100 || minSpawnDis != 0f) return;
            try
            {
                Vector3 player = iterator.PlayerPosition;
                int sector = iterator.Sectors.Select(Time.realtimeSinceStartup);
                __state = new PlacementState { Iterator = iterator, Player = player, Sector = sector };
                if (sector != 0)
                {
                    ArrivalRules.Rotate(startPos.x - player.x, startPos.z - player.z, sector, HordeArrival.DirectionCount,
                        out float x, out float z);
                    startPos = new Vector3(player.x + x, startPos.y, player.z + z);
                }
            }
            catch (Exception ex) { FeatureRuntime.Fail(Feature.Arrival, ex); }
        }

        private static void Postfix(Vector3 __result, PlacementState __state)
        {
            if (__state.Iterator == null || !FeatureRuntime.Enabled(Feature.Arrival)) return;
            bool failed = ArrivalRules.PlacementFailed(__result.y, __result.x - __state.Player.x, __result.z - __state.Player.z);
            __state.Iterator.Sectors.Record(__state.Sector, !failed, Time.realtimeSinceStartup);
        }
    }
}
