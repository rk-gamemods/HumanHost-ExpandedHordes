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
    private static void Configure(bool hud, bool debug, bool profiling, bool detail)
    {
        ModSettings.DebugHud.Value = hud; ModSettings.DebugMode.Value = debug;
        ModSettings.Profiling.Value = profiling; ModSettings.DetailedTimings.Value = detail;
        FeatureRuntime.Installed.Clear(); FeatureRuntime.Failures.Clear(); Plugin.Log.Lines.Clear();
        GameTelemetry.Initializations = GameTelemetry.Samples = 0; GameTelemetry.MembershipAvailable = true;
        DiagnosticEnvironment.Captures = 0; DiagnosticEnvironment.Throw = false;
        FrameTimingManager.Captures = 0; FrameTimingManager.Throw = false;
        GUI.Draws = 0; Input.Pressed.Clear();
    }
    internal static void Run(Action<bool, string> check)
    {
        string directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "expanded-hordes-modes-" + Guid.NewGuid().ToString("N")));
        try
        {
            Configure(false, false, false, true); PerformanceMonitor.Start(directory);
            PerformanceMonitor.Update(); PerformanceMonitor.Draw(); PerformanceMonitor.Record(TelemetryEvent.Fresh);
            check(!PerformanceMonitor.Active && GameTelemetry.Initializations == 0 && FeatureRuntime.Installed.Count == 0 &&
                DiagnosticEnvironment.Captures == 0 && FrameTimingManager.Captures == 0 && !Directory.Exists(directory),
                "All-off host installs no hooks, initializes no game accessors, queries no engine timing and creates no reports");
            for (int i = 0; i < 1000; i++) { PerformanceMonitor.Update(); PerformanceMonitor.Record(TelemetryEvent.Fresh); PerformanceMonitor.Begin(ProfileSection.ZombieUpdate); }
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) { PerformanceMonitor.Update(); PerformanceMonitor.Record(TelemetryEvent.Fresh); PerformanceMonitor.Begin(ProfileSection.ZombieUpdate); }
            check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Warmed disabled production host paths allocate zero bytes");
            foreach (var mode in new[] { (true, false, false, true), (false, true, false, true), (false, false, true, false), (true, true, true, true) })
            {
                Configure(mode.Item1, mode.Item2, mode.Item3, mode.Item4);
                string root = Path.Combine(directory, Guid.NewGuid().ToString("N"));
                PerformanceMonitor.Start(root); PerformanceMonitor.Update(); PerformanceMonitor.Draw();
                var collector = Read<TelemetryCollector>("collector");
                bool persistent = mode.Item2 || mode.Item3, detailed = mode.Item3 && mode.Item4;
                check(PerformanceMonitor.Active && GameTelemetry.Initializations == 1 && FeatureRuntime.Installed.Contains(Feature.Diagnostics), "Enabled host initializes shared collector/lifecycle hooks");
                check((Read<TelemetryWriter>("writer") != null) == persistent && DiagnosticEnvironment.Captures == (persistent ? 1 : 0) &&
                    (collector.Batch.ManagedBytes >= 0) == persistent, "Writer, inventory and memory sampling follow persistent mode only");
                check(FrameTimingManager.Captures == (mode.Item3 ? 1 : 0) && FeatureRuntime.Installed.Contains(Feature.Profiling) == detailed,
                    "Engine timings require profiling; detailed hooks require both profiling and detailed option");
                check(GUI.Draws == (mode.Item1 ? 1 : 0), "Disabled HUD issues no GUI draw");
                int layouts = GUIStyle.Layouts; var cachedContent = Read<GUIContent>("content"); var cachedStyle = Read<GUIStyle>("style");
                PerformanceMonitor.Draw();
                check(GUIStyle.Layouts == layouts && ReferenceEquals(cachedContent, Read<GUIContent>("content")) && ReferenceEquals(cachedStyle, Read<GUIStyle>("style")) &&
                    (!mode.Item1 || Read<Rect>("bounds").height > 300), "Draw reuses content/style/layout and accommodates the measured text height");
                PerformanceMonitor.Record(TelemetryEvent.Fresh);
                if (!persistent)
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
                Input.Pressed.Add(KeyCode.F9); ModSettings.MarkerNote.Value = "phase\n" + new string('x', 100);
                PerformanceMonitor.Update();
                check(collector.Batch.MarkerCount == 1 && collector.Batch.Markers[0].Note.Length == 80 && !collector.Batch.Markers[0].Note.Contains('\n'), "Host marker note is bounded and single-line");
                long lost = collector.DroppedBatches;
                PerformanceMonitor.Boundary(WindowEnd.Pause); bool pauseWritten = collector.DroppedBatches == lost;
                lost = collector.DroppedBatches;
                PerformanceMonitor.Boundary(WindowEnd.Resume); bool resumeWritten = collector.DroppedBatches == lost;
                lost = collector.DroppedBatches;
                PerformanceMonitor.Stop(); PerformanceMonitor.Stop();
                bool shutdownWritten = collector.DroppedBatches == lost;
                check(!PerformanceMonitor.Active && Directory.Exists(Path.Combine(root, "diagnostics")) == persistent, "Host stop is idempotent and HUD-only writes no files");
                if (persistent)
                {
                    string log = File.ReadAllText(Path.Combine(root, "diagnostics", "diagnostics.log"));
                    check((!pauseWritten || log.Contains("reason=Pause")) && (!resumeWritten || log.Contains("reason=Resume")) &&
                        (!shutdownWritten || log.Contains("reason=Shutdown")) &&
                        (pauseWritten ? log.Contains("test_marker id=1 action=start") : collector.LostMarkerNotes == 1) &&
                        collector.DroppedBatches == (pauseWritten ? 0 : 1) + (resumeWritten ? 0 : 1) + (shutdownWritten ? 0 : 1),
                        "Production host persists accepted boundaries/markers and counts every rejected publication");
                }
            }
            Configure(true, true, true, true); FeatureRuntime.Failures.Add(Feature.Diagnostics); FeatureRuntime.Failures.Add(Feature.Profiling);
            DiagnosticEnvironment.Throw = true; FrameTimingManager.Throw = true;
            string failureRoot = Path.Combine(directory, "optional-failures");
            PerformanceMonitor.Start(failureRoot); PerformanceMonitor.Update();
            check(PerformanceMonitor.Active && !GameTelemetry.LifecycleAvailable && GameTelemetry.CorpseEventsAvailable && !PerformanceMonitor.Begin(ProfileSection.ZombieUpdate).Active,
                "Failed lifecycle/detail hooks do not disable independent corpse collector or light host");
            check(Read<GUIContent>("content").text.Contains("Disabled features: Diagnostics, Profiling"), "HUD reports disabled feature names without requiring log inspection");
            Write("nextTiming", 0d); PerformanceMonitor.Update();
            check(FrameTimingManager.Captures == 1, "Optional engine timing failure disables only timing and is not retried every frame");
            Input.Pressed.Add(KeyCode.F10); PerformanceMonitor.Update(); PerformanceMonitor.Stop();
            string manifest = File.ReadAllText(Path.Combine(failureRoot, "diagnostics", "environment.txt"));
            check(manifest.Contains("environment_capture_failed=true") && !manifest.Contains("private path"), "Inventory failure remains bounded and excludes exception messages");
            check(DiagnosticEnvironment.Captures == 2, "Explicit inventory rescan retries failed optional inventory");
        }
        finally
        {
            PerformanceMonitor.Stop();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
