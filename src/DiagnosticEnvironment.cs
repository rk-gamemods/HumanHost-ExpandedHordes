using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace ExpandedHordes
{
    internal static class DiagnosticEnvironment
    {
        // Called at initialization, post-load, and explicit rescan only. Copy metadata
        // here; the writer is never permitted to query live Unity or plugin objects.
        internal static string Capture(string session)
        {
            var b = new TelemetryMetadata(Environment.UserName, Environment.MachineName);
            string Safe(string value) => b.Safe(value);
            b.AppendLine("environment session=" + session);
            b.AppendLine("game=" + Safe(Application.version) + " unity=" + Safe(Application.unityVersion) + " mod=" + ModIdentity.Version);
            b.AppendLine("bepinex=" + typeof(BepInEx.BaseUnityPlugin).Assembly.GetName().Version);
            b.AppendLine("build_identity=Terrain assembly MVID " + typeof(NPC_Horde_Mgr).Assembly.ManifestModule.ModuleVersionId);
            b.AppendLine("cpu=" + Safe(SystemInfo.processorType) + " logical_processors=" + SystemInfo.processorCount + " reported_ram_mib=" + SystemInfo.systemMemorySize);
            b.AppendLine("gpu=" + Safe(SystemInfo.graphicsDeviceName) + " reported_gpu_capacity_mib=" + SystemInfo.graphicsMemorySize + " api=" + SystemInfo.graphicsDeviceType);
            b.AppendLine("os=" + Safe(SystemInfo.operatingSystem) + " resolution=" + Screen.width + "x" + Screen.height + " quality=" + QualitySettings.GetQualityLevel() + " vsync=" + QualitySettings.vSyncCount + " frame_cap=" + Application.targetFrameRate);
            b.AppendLine("base_budget=" + ModSettings.Total.Value + " living=" + ModSettings.Living.Value + " ai_allowance=" + ModSettings.Allowance.Value + " corpse_target=" + ModSettings.CorpseLimit.Value);
            b.AppendLine("debug=" + ModSettings.DebugMode.Value + " profiling=" + ModSettings.Profiling.Value + " hud=" + ModSettings.DebugMode.Value + " detailed=" + ModSettings.DetailedTimings.Value);
            b.AppendLine("report_file_mib=" + ModSettings.ReportFileMiB.Value + " retained_files_per_stream=2; threshold_may_be_exceeded_by_one_batch");
            b.AppendLine("run_speed_percent=" + ModSettings.RunSpeedPercent.Value + " resistance_regular/large/boss=" + ModSettings.RegularResistance.Value + "/" + ModSettings.LargeResistance.Value + "/" + ModSettings.BossResistance.Value);
            b.AppendLine("large_begin/percent=" + ModSettings.LargeBegin.Value + "/" + ModSettings.LargePercent.Value + " boss_begin/percent=" + ModSettings.BossBegin.Value + "/" + ModSettings.BossPercent.Value);
            b.AppendLine("attraction_height/radius=" + ModSettings.AttractionHeight.Value + "/" + ModSettings.AttractionRadius.Value + " hud_x/y/scale=" + ModSettings.HudX.Value + "/" + ModSettings.HudY.Value + "/" + ModSettings.HudScale.Value);
            if (G_Save._config != null) b.AppendLine("native_horde_quantity_factor=" + G_Save._config._Horde_Z_NumF + " native_corpse_limit=" + G_Save._config._MaxCorpseCount);
            foreach (Feature feature in Enum.GetValues(typeof(Feature)))
                if (!FeatureRuntime.Enabled(feature)) b.AppendLine("disabled_feature=" + feature);
            b.AppendLine(GameTelemetry.Availability);
            b.AppendLine("patch_target_inventory_complete=" + FeatureRuntime.TargetInventoryComplete);
            b.AppendLine("coverage=BepInEx plugins and chainloader dependency errors; native/Workshop loaded-content registry unavailable; browser installation records do not prove loaded content; no overlap does not establish compatibility");
            HotkeyInventory.Current.Append(b);
            b.AppendLine("loader_dependency_error_count=" + Chainloader.DependencyErrors.Count);
            foreach (string error in Chainloader.DependencyErrors)
            {
                if (b.Full) break;
                b.AppendLine("loader_dependency_error=" + Safe(error));
            }
            foreach (var p in Chainloader.PluginInfos.Values)
            {
                if (b.Full) break;
                b.AppendLine("plugin=" + Safe(p.Metadata.GUID) + " name=" + Safe(p.Metadata.Name) + " version=" + p.Metadata.Version);
            }
            foreach (var method in FeatureRuntime.OwnedTargets)
            {
                if (b.Full) break;
                var info = Harmony.GetPatchInfo(method);
                if (info == null || !info.Owners.Any(FeatureRuntime.Owns)) continue;
                string target = method.DeclaringType?.FullName + "." + method.Name;
                Append(b, target, "prefix", info.Prefixes); Append(b, target, "postfix", info.Postfixes);
                Append(b, target, "transpiler", info.Transpilers); Append(b, target, "finalizer", info.Finalizers);
            }
            return b.ToString();
        }
        private static void Append(TelemetryMetadata b, string target, string kind, IEnumerable<Patch> patches)
        {
            foreach (var p in patches)
            {
                if (b.Full) break;
                if (FeatureRuntime.Owns(p.owner)) continue;
                string mapping = p.owner != null && Chainloader.PluginInfos.ContainsKey(p.owner) ? p.owner : "unresolved";
                b.AppendLine("overlap target=" + b.Safe(target) + " kind=" + kind + " owner=" + b.Safe(p.owner) + " plugin=" + b.Safe(mapping) +
                    " assembly=" + b.Safe(p.PatchMethod.DeclaringType?.Assembly.GetName().Name) + " type=" + b.Safe(p.PatchMethod.DeclaringType?.FullName) +
                    " priority=" + p.priority + " before=" + b.Ordering(p.before) + " after=" + b.Ordering(p.after));
            }
        }
    }
}
