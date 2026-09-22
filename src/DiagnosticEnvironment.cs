using System;
using System.Linq;
using System.Text;
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
            var b = new StringBuilder(8192);
            b.AppendLine("environment session=" + session);
            b.AppendLine("game=" + Safe(Application.version) + " unity=" + Safe(Application.unityVersion) + " mod=" + ModIdentity.Version);
            b.AppendLine("bepinex=" + typeof(BepInEx.BaseUnityPlugin).Assembly.GetName().Version);
            b.AppendLine("build_identity=Terrain assembly MVID " + typeof(NPC_Horde_Mgr).Assembly.ManifestModule.ModuleVersionId);
            b.AppendLine("cpu=" + Safe(SystemInfo.processorType) + " logical_processors=" + SystemInfo.processorCount + " reported_ram_mib=" + SystemInfo.systemMemorySize);
            b.AppendLine("gpu=" + Safe(SystemInfo.graphicsDeviceName) + " reported_gpu_capacity_mib=" + SystemInfo.graphicsMemorySize + " api=" + SystemInfo.graphicsDeviceType);
            b.AppendLine("os=" + Safe(SystemInfo.operatingSystem) + " resolution=" + Screen.width + "x" + Screen.height + " quality=" + QualitySettings.GetQualityLevel() + " vsync=" + QualitySettings.vSyncCount + " frame_cap=" + Application.targetFrameRate);
            b.AppendLine("base_budget=" + ModSettings.Total.Value + " living=" + ModSettings.Living.Value + " ai_allowance=" + ModSettings.Allowance.Value + " corpse_target=" + ModSettings.CorpseLimit.Value);
            b.AppendLine("debug=" + ModSettings.DebugMode.Value + " profiling=" + ModSettings.Profiling.Value + " hud=" + ModSettings.DebugHud.Value + " detailed=" + ModSettings.DetailedTimings.Value);
            b.AppendLine("run_speed_percent=" + ModSettings.RunSpeedPercent.Value + " resistance_regular/large/boss=" + ModSettings.RegularResistance.Value + "/" + ModSettings.LargeResistance.Value + "/" + ModSettings.BossResistance.Value);
            b.AppendLine("large_begin/percent=" + ModSettings.LargeBegin.Value + "/" + ModSettings.LargePercent.Value + " boss_begin/percent=" + ModSettings.BossBegin.Value + "/" + ModSettings.BossPercent.Value);
            b.AppendLine("attraction_height/radius=" + ModSettings.AttractionHeight.Value + "/" + ModSettings.AttractionRadius.Value + " hud_x/y/scale=" + ModSettings.HudX.Value + "/" + ModSettings.HudY.Value + "/" + ModSettings.HudScale.Value);
            b.AppendLine("hud/marker/rescan_keys=" + ModSettings.HudKey.Value + "/" + ModSettings.MarkerKey.Value + "/" + ModSettings.RescanKey.Value);
            if (G_Save._config != null) b.AppendLine("native_horde_quantity_factor=" + G_Save._config._Horde_Z_NumF + " native_corpse_limit=" + G_Save._config._MaxCorpseCount);
            foreach (Feature feature in Enum.GetValues(typeof(Feature)))
                if (!FeatureRuntime.Enabled(feature)) b.AppendLine("disabled_feature=" + feature);
            b.AppendLine(GameTelemetry.Availability);
            b.AppendLine("coverage=BepInEx plugins only; native/Workshop registry unavailable; loader error inventory unavailable; no overlap does not establish compatibility");
            foreach (var p in Chainloader.PluginInfos.Values.OrderBy(p => p.Metadata.GUID))
                b.AppendLine("plugin=" + Safe(p.Metadata.GUID) + " name=" + Safe(p.Metadata.Name) + " version=" + p.Metadata.Version);
            foreach (var method in Harmony.GetAllPatchedMethods())
            {
                var info = Harmony.GetPatchInfo(method);
                if (info == null || !info.Owners.Any(o => o.StartsWith(ModIdentity.Guid, StringComparison.Ordinal))) continue;
                string target = method.DeclaringType?.FullName + "." + method.Name;
                Append(b, target, "prefix", info.Prefixes.ToArray()); Append(b, target, "postfix", info.Postfixes.ToArray());
                Append(b, target, "transpiler", info.Transpilers.ToArray()); Append(b, target, "finalizer", info.Finalizers.ToArray());
            }
            // Cap metadata even in heavily modded environments; disclose truncation.
            if (b.Length > 16384) { b.Length = 16384; b.AppendLine("\nmetadata_truncated=true"); }
            return b.ToString();
        }
        private static void Append(StringBuilder b, string target, string kind, Patch[] patches)
        {
            foreach (var p in patches)
            {
                if (p.owner.StartsWith(ModIdentity.Guid, StringComparison.Ordinal)) continue;
                string mapping = Chainloader.PluginInfos.ContainsKey(p.owner) ? p.owner : "unresolved";
                b.AppendLine("overlap target=" + Safe(target) + " kind=" + kind + " owner=" + Safe(p.owner) + " plugin=" + Safe(mapping) +
                    " assembly=" + Safe(p.PatchMethod.DeclaringType?.Assembly.GetName().Name) + " type=" + Safe(p.PatchMethod.DeclaringType?.FullName) +
                    " priority=" + p.priority + " before=" + Safe(string.Join("|", p.before ?? Array.Empty<string>())) + " after=" + Safe(string.Join("|", p.after ?? Array.Empty<string>())));
            }
        }
        private static string Safe(string value)
        {
            if (value == null) return "unavailable";
            // Metadata is a narrow allowlist, never third-party config/log contents.
            if (value.Contains("\\") || value.Contains("/")) return "[path-like metadata omitted]";
            return value.Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
