using System.IO;
using System;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ExpandedHordes
{
    [BepInPlugin(Guid, ModIdentity.Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = ModIdentity.Guid;
        public const string Version = ModIdentity.Version;
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            ModSettings.Bind(Config);
            DebugHordeTrigger.Initialize(Config);
            FeatureRuntime.Install(Feature.Population, typeof(AiSetup), typeof(HordeAllowance), typeof(SpawnBudget));
            FeatureRuntime.Install(Feature.Catalog, typeof(HordeSetup));
            if (FeatureRuntime.Enabled(Feature.Catalog))
            {
                FeatureRuntime.Install(Feature.Composition, typeof(HordeComposition));
                FeatureRuntime.Install(Feature.Resistance, typeof(DamageResistance));
            }
            FeatureRuntime.Install(Feature.Attraction, typeof(HordeStart));
            FeatureRuntime.Install(Feature.Movement, typeof(HordeRunSpeed));
            FeatureRuntime.Install(Feature.Corpses, typeof(CorpseRetention));
            try { PerformanceMonitor.Start(Path.GetDirectoryName(Info.Location)); }
            catch (Exception ex) { Log.LogError("Diagnostics initialization failed: " + ex.GetType().Name); PerformanceMonitor.Stop(); }
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Log.LogInfo($"Expanded Hordes {Version} loaded | game {Application.version} | base budget {ModSettings.Total.Value}, living {ModSettings.Living.Value}, shared AI {ModSettings.Allowance.Value}, run speed {ModSettings.RunSpeedPercent.Value}%; resistance regular/large/boss {ModSettings.RegularResistance.Value}/{ModSettings.LargeResistance.Value}/{ModSettings.BossResistance.Value}%; corpse target {ModSettings.CorpseLimit.Value}. Restart after configuration changes.");
        }

        private void Update() { PerformanceMonitor.Update(); DebugHordeTrigger.Update(); }
        private void OnGUI() => PerformanceMonitor.Draw();
        private void OnApplicationPause(bool paused) { if (paused) DebugHordeTrigger.Cancel(); PerformanceMonitor.Boundary(paused ? WindowEnd.Pause : WindowEnd.Resume); }
        private void OnApplicationQuit() { DebugHordeTrigger.Stop(); PerformanceMonitor.Stop(); }
        private void OnSceneUnloaded(Scene scene) { DebugHordeTrigger.Cancel(); PerformanceMonitor.Boundary(WindowEnd.SceneUnload); }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            DebugHordeTrigger.Stop();
            PlacementLog.Flush();
            PerformanceMonitor.Stop();
            try { PopulationOverrides.Restore(); }
            catch (Exception ex) { Log.LogError("Could not restore owned population values: " + ex); }
            finally { FeatureRuntime.Stop(); CreatureCatalog.Clear(); }
        }
    }
}
