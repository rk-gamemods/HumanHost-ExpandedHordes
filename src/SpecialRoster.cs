using System;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ExpandedHordes
{
    internal static class SpecialRoster
    {
        private static readonly AccessTools.FieldRef<Terrain_Loader_Manager, float> GridWidth =
            AccessTools.FieldRefAccess<Terrain_Loader_Manager, float>("BigTerraWidth");
        internal static void Select(NPC_Horde_Mgr owner, ref int biome, ref int group, ref int prefab)
        {
            var loader = Terrain_Loader_Manager.ins;
            Vector3 real = Player_Input.ins.transform.position - loader.neutralizedPlayerMove;
            int region = HordeRules.Region(real.x, real.z, GridWidth(loader), loader.BiomesWidthDis);
            int category = HordeRules.Category(Random.Range(0, 10000), region, ModSettings.LargeBegin.Value,
                ModSettings.BossBegin.Value, ModSettings.LargePercent.Value, ModSettings.BossPercent.Value);
            if (category == 0) return;
            var pool = category == 2 ? CreatureCatalog.Bosses : CreatureCatalog.Large;
            if (pool.Count == 0) return;
            int limit = category == 2 ? ModSettings.BossLimit.Value : ModSettings.LargeLimit.Value;
            if (limit != 0)
            {
                bool known = HordeSpecialLimits.TryCount(owner, out int large, out int bosses);
                if (!HordeRules.AllowsExtra(category, known, large, bosses,
                    ModSettings.LargeLimit.Value, ModSettings.BossLimit.Value)) return;
            }
            CreatureCatalog.Entry choice = pool[Random.Range(0, pool.Count)];
            biome = choice.Biome;
            group = choice.Group;
            prefab = choice.Prefab;
        }
    }

    [HarmonyPatch(typeof(NPC_Horde_Mgr), "Spawn_Horde_NPC")]
    internal static class HordeComposition
    {
        private static void Prefix(NPC_Horde_Mgr __instance, ref int biomeIndex, ref int groupIndex,
            ref int npcPrefabIndex, bool async, out bool __state)
        {
            __state = false;
            if (!async || G_Save.isQuit || !FeatureRuntime.Enabled(Feature.Composition)) return;
            var started = PerformanceMonitor.Begin(ProfileSection.Composition);
            try
            {
                __state = HordeSpecialLimits.Enter(__instance);
                if (__state) SpecialRoster.Select(__instance, ref biomeIndex, ref groupIndex, ref npcPrefabIndex);
            }
            catch (Exception ex) { FeatureRuntime.Fail(Feature.Composition, ex); }
            finally { PerformanceMonitor.End(started); }
        }

        private static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state) HordeSpecialLimits.Exit();
            return __exception;
        }
    }
}
