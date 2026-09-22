using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace ExpandedHordes
{
    internal static class GameTelemetry
    {
        private static Func<NPC_Horde_Mgr> horde;
        private static Func<GPUI_Dead_Body_Mgr> bodies;
        private static Func<NPC_Horde_Mgr, int> aliveCount, nativeBudget, nativeGrowth;
        private static Func<GPUI_Dead_Body_Mgr, int> bodyCount;
        private static Func<NPC_Horde_Mgr, NPC_Horde_Mgr.Horde_Save_Data> save;
        private static Func<NPC_Horde_Mgr, Coroutine> spawning;
        private static Func<NPC_Horde_Mgr, Global_Infos> gameInfo;
        private static Func<Terrain_Loader_Manager> terrain;
        private static Func<Terrain_Loader_Manager, float> gridWidth;
        private static Func<NPC_Horde_Mgr, Dictionary<GameObject, NPC_Horde_Mgr.Horde_NPC_Info>> alive;
        internal static int RestoreDepth, DeathDepth;
        internal static string Availability = "";
        internal static bool CorpseEventsAvailable, LifecycleAvailable;
        internal static bool MembershipAvailable => alive != null;
        internal static void Initialize()
        {
            horde = Static<NPC_Horde_Mgr>(); bodies = Static<GPUI_Dead_Body_Mgr>();
            aliveCount = Count<NPC_Horde_Mgr>("_aliveHordeNPCs"); bodyCount = Count<GPUI_Dead_Body_Mgr>("_ActiveDeadBodies");
            nativeBudget = Field<NPC_Horde_Mgr, int>("_HordeZombieAll"); nativeGrowth = Field<NPC_Horde_Mgr, int>("_ZombiesPerWaveAdd");
            save = Field<NPC_Horde_Mgr, NPC_Horde_Mgr.Horde_Save_Data>("_hordeSaveData");
            spawning = Field<NPC_Horde_Mgr, Coroutine>("_corHordeSpawn");
            gameInfo = Field<NPC_Horde_Mgr, Global_Infos>("_G_Info");
            terrain = Static<Terrain_Loader_Manager>(); gridWidth = Field<Terrain_Loader_Manager, float>("BigTerraWidth");
            alive = Field<NPC_Horde_Mgr, Dictionary<GameObject, NPC_Horde_Mgr.Horde_NPC_Info>>("_aliveHordeNPCs");
            Availability = $"alive_accessor={aliveCount != null}; corpse_accessor={bodyCount != null}; save_accessor={save != null}; game_time_accessor={gameInfo != null}; player_kills=unavailable; categories=catalog_known_fresh_only; region=sampled_horde_spawn_region";
        }
        private static Func<T> Static<T>()
        {
            try { return Expression.Lambda<Func<T>>(Expression.Field(null, AccessTools.Field(typeof(T), "_ins"))).Compile(); }
            catch { return null; }
        }
        private static Func<T, V> Field<T, V>(string name)
        {
            try { var p = Expression.Parameter(typeof(T)); return Expression.Lambda<Func<T, V>>(Expression.Field(p, AccessTools.Field(typeof(T), name)), p).Compile(); }
            catch { return null; }
        }
        private static Func<T, int> Count<T>(string name)
        {
            try { return TelemetryAccessors.DictionaryCount<T>(AccessTools.Field(typeof(T), name)); }
            catch { return null; }
        }
        internal static bool Contains(NPC_Horde_Mgr owner, GameObject entity)
        {
            var members = alive?.Invoke(owner);
            return members != null && !ReferenceEquals(entity, null) && members.ContainsKey(entity);
        }
        internal static int HordeId
        {
            get { var mgr = horde?.Invoke(); int id = mgr && save != null ? save(mgr)?.spawnedWaveCount ?? -1 : -1; return id > 0 ? id : -1; }
        }
        internal static PopulationSample Sample()
        {
            var s = PopulationSample.Unavailable;
            var mgr = horde?.Invoke(); var corpse = bodies?.Invoke();
            if (mgr)
            {
                s.Alive = aliveCount == null ? -1 : aliveCount(mgr);
                var data = save?.Invoke(mgr);
                if (data != null) { s.Horde = data.spawnedWaveCount; s.Emitted = data.spawnedAlreadyCount; }
                var info = gameInfo?.Invoke(mgr);
                if (info) s.GameMinutes = info._totalGameMinutes;
                var loader = terrain?.Invoke();
                if (data != null && data.spawnStartRealPos.y != -10000f && loader && gridWidth != null)
                    s.Region = HordeRules.Region(data.spawnStartRealPos.x, data.spawnStartRealPos.z, gridWidth(loader), loader.BiomesWidthDis);
                s.Spawning = spawning != null && spawning(mgr) != null;
                s.SpawningKnown = spawning != null;
                s.LivingTarget = mgr._MaxAllowActiveZombies;
                if (G_Save._config != null && data != null && nativeBudget != null && nativeGrowth != null)
                    s.Budget = Mathf.RoundToInt(nativeBudget(mgr) * G_Save._config._Horde_Z_NumF) + (data.spawnedWaveCount - 1) * nativeGrowth(mgr);
            }
            if (corpse)
            {
                s.Corpses = bodyCount == null ? -1 : bodyCount(corpse);
                s.CorpseLimit = G_Save._config == null ? -1 : CorpseRetention.EffectiveLimit(G_Save._config._MaxCorpseCount);
            }
            return s;
        }
    }
    [HarmonyPatch(typeof(NPC_Horde_Mgr), "Add_AliveHordeNPC")]
    internal static class ObserveRegistration
    {
        private static void Prefix(NPC_Horde_Mgr __instance, GameObject npcObj, out bool __state)
        { __state = PerformanceMonitor.OnMain && !GameTelemetry.Contains(__instance, npcObj); }
        private static void Postfix(NPC_Horde_Mgr __instance, GameObject npcObj, NPC_Horde_Mgr.Horde_NPC_Info npcInfo, bool __state)
        {
            if (!__state) return;
            var transition = LifecycleObservation.Registration(false,
                GameTelemetry.Contains(__instance, npcObj), GameTelemetry.RestoreDepth > 0);
            if (transition == TelemetryEvent.Count) return;
            PerformanceMonitor.ObserveRegisteredHorde();
            PerformanceMonitor.Record(transition);
            if (GameTelemetry.RestoreDepth == 0)
            {
                var kind = CreatureCatalog.Classify(false, npcInfo);
                if (kind == ZombieKind.Large) PerformanceMonitor.Record(TelemetryEvent.Large);
                if (kind == ZombieKind.Boss) PerformanceMonitor.Record(TelemetryEvent.Boss);
            }
        }
    }
    [HarmonyPatch(typeof(NPC_Horde_Mgr), "Remove_AliveHordeNPC")]
    internal static class ObserveRemoval
    {
        private static void Prefix(NPC_Horde_Mgr __instance, GameObject npcObj, out bool __state)
        { __state = PerformanceMonitor.OnMain && GameTelemetry.Contains(__instance, npcObj); }
        private static void Postfix(NPC_Horde_Mgr __instance, GameObject npcObj, bool __state)
        {
            if (!__state) return;
            var transition = LifecycleObservation.Removal(true,
                GameTelemetry.Contains(__instance, npcObj), GameTelemetry.DeathDepth > 0);
            if (transition != TelemetryEvent.Count) PerformanceMonitor.Record(transition);
        }
    }
    [HarmonyPatch(typeof(NPC_Horde_Mgr), "Restore_Horde_NPCs")]
    internal static class ObserveRestore
    {
        private static void Prefix(out bool __state) { __state = PerformanceMonitor.OnMain; if (__state) GameTelemetry.RestoreDepth++; }
        private static Exception Finalizer(Exception __exception, bool __state) { if (__state) GameTelemetry.RestoreDepth--; return __exception; }
    }
    [HarmonyPatch(typeof(NPC_Spawner_Mgr), "Back_Dead_NPC_To_Pool")]
    internal static class ObserveDeath
    {
        private static void Prefix(out bool __state) { __state = PerformanceMonitor.OnMain; if (__state) GameTelemetry.DeathDepth++; }
        private static Exception Finalizer(Exception __exception, bool __state) { if (__state) GameTelemetry.DeathDepth--; return __exception; }
    }
    // These checked IL seams observe actual dictionary mutations, including load
    // restoration and distance hiding. They do not mean ragdoll creation or loot loss.
    internal static class CorpseObservation
    {
        internal static IEnumerable<CodeInstruction> Insert(IEnumerable<CodeInstruction> instructions, bool remove)
        {
            var field = AccessTools.Field(typeof(GPUI_Dead_Body_Mgr), "_ActiveDeadBodies");
            var method = field.FieldType.GetMethod(remove ? "Remove" : "Add", remove ? new[] { typeof(GameObject) } : field.FieldType.GetGenericArguments());
            var code = instructions.ToList();
            if (code.Count(i => i.Calls(method)) != 1) throw new InvalidOperationException("Unexpected corpse registration/removal IL shape");
            foreach (var instruction in code)
            {
                yield return instruction;
                if (!instruction.Calls(method)) continue;
                if (remove) yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CorpseObservation), remove ? nameof(Removed) : nameof(Added)));
            }
        }
        private static void Added() => PerformanceMonitor.Record(TelemetryEvent.CorpseAdded);
        private static void Removed(bool removed) { if (removed) PerformanceMonitor.Record(TelemetryEvent.CorpseRemoved); }
    }
    [HarmonyPatch(typeof(GPUI_Dead_Body_Mgr), "Spawn_GPUI_Dead_Body")]
    internal static class ObserveCorpseAdded
    { private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => CorpseObservation.Insert(instructions, false); }
    [HarmonyPatch(typeof(GPUI_Dead_Body_Mgr), "Put_Back_Body_To_Pool")]
    internal static class ObserveCorpseRemoved
    { private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => CorpseObservation.Insert(instructions, true); }
}
