using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
using ExpandedHordes;
using UnityEngine;

internal static class HostModeChecks
{
    private static T Read<T>(string name) => (T)typeof(PerformanceMonitor).GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    private static void Write(string name, object value) => typeof(PerformanceMonitor).GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    private static void Configure(bool debug, bool profiling, bool detail)
    {
        ModSettings.DebugMode.Value = debug;
        ModSettings.Profiling.Value = profiling; ModSettings.DetailedTimings.Value = detail;
        FeatureRuntime.Installed.Clear(); FeatureRuntime.Failures.Clear(); Plugin.Log.Lines.Clear();
        GameTelemetry.Initializations = GameTelemetry.Samples = 0; GameTelemetry.MembershipAvailable = true;
        DiagnosticEnvironment.Captures = 0; DiagnosticEnvironment.Throw = false;
        FrameTimingManager.Captures = 0; FrameTimingManager.Throw = false;
        GUI.Draws = 0; Input.Pressed.Clear();
        Event.current.type = EventType.Repaint;
        BepInEx.Bootstrap.Chainloader.PluginInfos.Clear();
    }
    internal static void Run(Action<bool, string> check)
    {
        string directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "expanded-hordes-modes-" + Guid.NewGuid().ToString("N")));
        try
        {
            Configure(false, false, true); PerformanceMonitor.Start(directory);
            PerformanceMonitor.Update(); PerformanceMonitor.Draw(); PerformanceMonitor.Record(TelemetryEvent.Fresh);
            check(!PerformanceMonitor.Active && GameTelemetry.Initializations == 0 && FeatureRuntime.Installed.Count == 0 &&
                DiagnosticEnvironment.Captures == 0 && FrameTimingManager.Captures == 0 && !Directory.Exists(directory),
                "All-off host installs no hooks, initializes no game accessors, queries no engine timing and creates no reports");
            for (int i = 0; i < 1000; i++) { PerformanceMonitor.Update(); PerformanceMonitor.Record(TelemetryEvent.Fresh); PerformanceMonitor.Begin(ProfileSection.ZombieUpdate); }
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) { PerformanceMonitor.Update(); PerformanceMonitor.Record(TelemetryEvent.Fresh); PerformanceMonitor.Begin(ProfileSection.ZombieUpdate); }
            check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Warmed disabled production host paths allocate zero bytes");
            foreach (var mode in new[] { (true, false, true), (false, true, false), (true, true, true) })
            {
                Configure(mode.Item1, mode.Item2, mode.Item3);
                string root = Path.Combine(directory, Guid.NewGuid().ToString("N"));
                PerformanceMonitor.Start(root); PerformanceMonitor.Update(); PerformanceMonitor.Draw();
                var collector = Read<TelemetryCollector>("collector");
                bool persistent = mode.Item1 || mode.Item2, detailed = mode.Item2 && mode.Item3;
                check(PerformanceMonitor.Active && GameTelemetry.Initializations == 1 && FeatureRuntime.Installed.Contains(Feature.Diagnostics), "Enabled host initializes shared collector/lifecycle hooks");
                check((Read<TelemetryWriter>("writer") != null) == persistent && DiagnosticEnvironment.Captures == (persistent ? 1 : 0) &&
                    (collector.Batch.ManagedBytes >= 0) == persistent, "Writer, inventory and memory sampling follow persistent mode only");
                check(FrameTimingManager.Captures == (mode.Item2 ? 1 : 0) && FeatureRuntime.Installed.Contains(Feature.Profiling) == detailed,
                    "Engine timings require profiling; detailed hooks require both profiling and detailed option");
                check((GUI.Draws > 0) == mode.Item1, "Debug mode automatically shows HUD; profiling alone draws no HUD");
                if (mode.Item1)
                {
                    DebugHordeTrigger.Hint = "Alt + K: Start Horde Now";
                    Write("nextHud", 0d); PerformanceMonitor.Update();
                    check(Read<HudSnapshot>("hudSnapshot").Hint == DebugHordeTrigger.Hint,
                        "Production monitor refresh forwards the current configurable shortcut hint to the HUD");
                    DebugHordeTrigger.Hint = "Ctrl + Shift + Pause: Start Horde Now";
                }
                int layouts = GUIStyle.Layouts; var cachedSnapshot = Read<HudSnapshot>("hudSnapshot"); var cachedRenderer = Read<TelemetryHudRenderer>("hudRenderer");
                PerformanceMonitor.Draw();
                check(GUIStyle.Layouts == layouts && ReferenceEquals(cachedSnapshot, Read<HudSnapshot>("hudSnapshot")) && ReferenceEquals(cachedRenderer, Read<TelemetryHudRenderer>("hudRenderer")),
                    "Draw reuses the cached snapshot and renderer without reformatting or text layout");
                PerformanceMonitor.Record(TelemetryEvent.Fresh);
                if (!mode.Item2)
                {
                    // Includes the production enable/thread checks and collector
                    // call, but deliberately cannot measure native Harmony dispatch.
                    for (int i = 0; i < 1000; i++) PerformanceMonitor.Record(TelemetryEvent.Death);
                    allocated = GC.GetAllocatedBytesForCurrentThread(); long began = Stopwatch.GetTimestamp();
                    for (int i = 0; i < 10000; i++) PerformanceMonitor.Record(TelemetryEvent.Death);
                    double elapsed = Stopwatch.GetElapsedTime(began).TotalMicroseconds;
                    check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Warmed production host event path allocates zero bytes");
                    Console.WriteLine($"HOST event entry + thread guard + collector: mean={elapsed / 10000:0.###} us/call over 10,000 calls; zero allocated bytes; excludes Harmony/native registration and Unity Mono.");
                }
                var thread = new Thread(() => PerformanceMonitor.Record(TelemetryEvent.Fresh)); thread.Start(); thread.Join();
                check(collector.Totals[0] == 1 && PerformanceMonitor.WrongThreadEvents == 1, "Host rejects off-thread events without changing totals");
                long observed = collector.Batch.Observed[0]; var sample = PerformanceMonitor.Begin(ProfileSection.ZombieUpdate); PerformanceMonitor.End(sample);
                check(collector.Batch.Observed[0] == observed + (detailed ? 1 : 0), "Light modes perform no detail sampling work");
                Input.Pressed.UnionWith(new[] { KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11 });
                PerformanceMonitor.Update(); PerformanceMonitor.Draw();
                check(Input.Pressed.Count == 4, "Diagnostics never consumes the former F8-F11 commands");
                int draws = GUI.Draws;
                Event.current.type = EventType.Layout; PerformanceMonitor.Draw();
                check(GUI.Draws == draws, "HUD is passive and draws only during repaint");
                Event.current.type = EventType.Repaint;
                long lost = collector.DroppedBatches;
                PerformanceMonitor.Boundary(WindowEnd.Pause); bool pauseWritten = collector.DroppedBatches == lost;
                lost = collector.DroppedBatches;
                PerformanceMonitor.Boundary(WindowEnd.Resume); bool resumeWritten = collector.DroppedBatches == lost;
                lost = collector.DroppedBatches;
                PerformanceMonitor.Stop(); PerformanceMonitor.Stop();
                bool shutdownWritten = collector.DroppedBatches == lost;
                check(!PerformanceMonitor.Active && Directory.Exists(Path.Combine(root, "diagnostics")) == persistent, "Host stop is idempotent and every enabled mode writes reports");
                if (persistent)
                {
                    string log = File.ReadAllText(Path.Combine(root, "diagnostics", "diagnostics.log"));
                    check((!pauseWritten || log.Contains("reason=Pause")) && (!resumeWritten || log.Contains("reason=Resume")) &&
                        (!shutdownWritten || log.Contains("reason=Shutdown")) &&
                        collector.DroppedBatches == (pauseWritten ? 0 : 1) + (resumeWritten ? 0 : 1) + (shutdownWritten ? 0 : 1),
                        "Production host persists accepted boundaries and counts every rejected publication");
                }
            }
            Configure(true, true, true); FeatureRuntime.Failures.Add(Feature.Diagnostics); FeatureRuntime.Failures.Add(Feature.Profiling);
            DiagnosticEnvironment.Throw = true; FrameTimingManager.Throw = true;
            string failureRoot = Path.Combine(directory, "optional-failures");
            PerformanceMonitor.Start(failureRoot); PerformanceMonitor.Update();
            check(PerformanceMonitor.Active && !GameTelemetry.LifecycleAvailable && GameTelemetry.CorpseEventsAvailable && !PerformanceMonitor.Begin(ProfileSection.ZombieUpdate).Active,
                "Failed lifecycle/detail hooks do not disable independent corpse collector or light host");
            check(Read<HudSnapshot>("hudSnapshot").RecordingWarning && Read<HudSnapshot>("hudSnapshot").Recording.Contains("unavailable"),
                "HUD surfaces unavailable features as a concise warning and leaves feature details in reports");
            Write("nextTiming", 0d); PerformanceMonitor.Update();
            check(FrameTimingManager.Captures == 1, "Optional engine timing failure disables only timing and is not retried every frame");
            Write("nextInventory", 0d); PerformanceMonitor.Update();
            Write("nextInventory", 0d); PerformanceMonitor.Update(); PerformanceMonitor.Stop();
            string manifest = File.ReadAllText(Path.Combine(failureRoot, "diagnostics", "environment.txt"));
            check(manifest.Contains("environment_capture_failed=true") && !manifest.Contains("private path"), "Inventory failure remains bounded and excludes exception messages");
            check(DiagnosticEnvironment.Captures == 3, "Automatic inventory rescans retry failed optional inventory");

            Configure(true, false, false);
            var other = new BepInEx.Bootstrap.TestPluginInfo("minimap");
            other.Instance.Config.Add("Hotkeys", "Tuning", KeyCode.F9);
            BepInEx.Bootstrap.Chainloader.PluginInfos.Add("other", other);
            var second = new BepInEx.Bootstrap.TestPluginInfo("lamp");
            second.Instance.Config.Add("Hotkeys", "Panel", KeyCode.F9);
            BepInEx.Bootstrap.Chainloader.PluginInfos.Add("second", second);
            PerformanceMonitor.Start(Path.Combine(directory, "conflicts"));
            check(HotkeyInventory.Current.PairCount == 1, "Startup automatically reports hotkey overlaps between installed mods");
            Input.Pressed.Add(KeyCode.F9); PerformanceMonitor.Update(); PerformanceMonitor.Draw();
            check(Input.Pressed.Contains(KeyCode.F9), "Conflict reporting leaves other mods' input untouched");
            int conflictCaptures = DiagnosticEnvironment.Captures;
            other.Instance.Config.Set("Hotkeys", "Tuning", KeyCode.None);
            PerformanceMonitor.Update();
            check(HotkeyInventory.Current.PairCount == 0 && DiagnosticEnvironment.Captures == conflictCaptures + 1,
                "A binding edit automatically refreshes conflict detection and environment metadata");
            second.Instance.Config.Add("Hotkeys", "Added", KeyCode.F9);
            Write("nextInventory", 0d); PerformanceMonitor.Update();
            check(HotkeyInventory.Current.PairCount == 1 && double.IsPositiveInfinity(Read<double>("nextInventory")),
                "Single post-load scan discovers newly added bindings without scheduling recurring scans");
            ModSettings.DebugMode.Value = false;
            int beforeHide = GUI.Draws; PerformanceMonitor.Update(); PerformanceMonitor.Draw();
            check(GUI.Draws == beforeHide, "Turning debug mode off in settings hides the HUD without an in-game command");
            PerformanceMonitor.Stop();
        }
        finally
        {
            PerformanceMonitor.Stop();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
