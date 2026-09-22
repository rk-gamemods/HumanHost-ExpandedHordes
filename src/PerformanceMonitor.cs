using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace ExpandedHordes
{
    internal enum ProfileSection { ZombieUpdate, Placement, CorpseCreation, HordeSave, Resistance, RunSpeed, Composition, Count }
    internal static class PerformanceMonitor
    {
        private static TelemetryCollector collector;
        private static TelemetryWriter writer;
        private static DetailSampler sampler;
        private static TelemetryHud hudData;
        private static readonly FrameTiming[] Timing = new FrameTiming[1];
        private static bool active, detail, persist, hud, timing, warnedWriter, writerQuarantined;
        private static int mainThread, gc0, gc1, gc2, marker;
        private static long origin;
        private static double nextSlow, nextTiming, nextHud, nextInventory;
        private static ulong lastTiming;
        private static double cachedNow;
        private static long lastHordeSummaryAttempt;
        private static string session, reportDirectory;
        private static GUIStyle style;
        private static GUIContent content;
        private static Rect bounds;
        private static KeyCode hudKey, markerKey, rescanKey, folderKey;
        private static float hudScale;
        private static bool hudLayoutDirty;
        internal static bool Active => active;
        internal static long WrongThreadEvents;
        internal static bool OnMain
        {
            get
            {
                if (!active) return false;
                if (Thread.CurrentThread.ManagedThreadId == mainThread) return true;
                Interlocked.Increment(ref WrongThreadEvents); return false;
            }
        }
        private static double Now => (Stopwatch.GetTimestamp() - origin) * (1000d / Stopwatch.Frequency);

        internal static void Record(TelemetryEvent kind)
        {
            if (!active) return;
            if (Thread.CurrentThread.ManagedThreadId != mainThread) { Interlocked.Increment(ref WrongThreadEvents); return; }
            collector.Record(kind);
        }
        internal static void ObserveRegisteredHorde()
        {
            if (OnMain) collector.ObserveHorde(GameTelemetry.HordeId, cachedNow);
        }
        internal static TimingSample Begin(ProfileSection section)
        {
            if (!detail || !OnMain || !sampler.Select((int)section, collector.Batch)) return default;
            return new TimingSample(collector.Batch, (int)section, Stopwatch.GetTimestamp());
        }
        internal static void End(TimingSample started)
        {
            if (!started.Active || !detail || !OnMain) return;
            started.Complete(collector.Batch, Stopwatch.GetTimestamp());
        }
        internal static void Start(string directory)
        {
            if (active) return;
            if (writerQuarantined) { Plugin.Log.LogWarning("Diagnostics requires a game restart after an incomplete writer shutdown."); return; }
            marker = 0; WrongThreadEvents = 0; cachedNow = 0; lastHordeSummaryAttempt = 0; lastTiming = 0;
            warnedWriter = false;
            style = null; content = null;
            GameTelemetry.RestoreDepth = GameTelemetry.DeathDepth = 0;
            hud = ModSettings.DebugHud.Value;
            persist = ModSettings.Profiling.Value || ModSettings.DebugMode.Value;
            if (!hud && !persist) return;
            mainThread = Thread.CurrentThread.ManagedThreadId; origin = Stopwatch.GetTimestamp();
            session = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
            hudKey = ModSettings.HudKey.Value; markerKey = ModSettings.MarkerKey.Value; rescanKey = ModSettings.RescanKey.Value;
            folderKey = ModSettings.FolderKey.Value; reportDirectory = Path.Combine(directory, "diagnostics");
            hudScale = ModSettings.HudScale.Value;
            bounds = new Rect(ModSettings.HudX.Value, ModSettings.HudY.Value, 920 * hudScale, 260 * hudScale);
            GameTelemetry.Initialize();
            if (persist) writer = new TelemetryWriter(new TelemetryFileSink(reportDirectory, session, CaptureInventory(), ModSettings.ReportFileMiB.Value * 1024L * 1024));
            collector = new TelemetryCollector(0, writer?.Initial ?? new TelemetryBatch());
            sampler = new DetailSampler(); hudData = new TelemetryHud(); active = true;
            timing = ModSettings.Profiling.Value;
            detail = ModSettings.Profiling.Value && ModSettings.DetailedTimings.Value;
            if (detail) { FeatureRuntime.InstallProfiler(); detail = FeatureRuntime.Enabled(Feature.Profiling); }
            FeatureRuntime.Install(Feature.Diagnostics, typeof(PlacementResult), typeof(ContextResult),
                typeof(ObserveRegistration), typeof(ObserveRemoval), typeof(ObserveRestore), typeof(ObserveDeath));
            GameTelemetry.LifecycleAvailable = FeatureRuntime.Enabled(Feature.Diagnostics) && GameTelemetry.MembershipAvailable;
            FeatureRuntime.Install(Feature.CorpseDiagnostics, typeof(ObserveCorpseAdded), typeof(ObserveCorpseRemoved));
            GameTelemetry.CorpseEventsAvailable = FeatureRuntime.Enabled(Feature.CorpseDiagnostics);
            nextInventory = 5000; nextSlow = nextTiming = nextHud = 0;
            gc0 = GC.CollectionCount(0); gc1 = GC.CollectionCount(1); gc2 = GC.CollectionCount(2);
            Plugin.Log.LogInfo("Diagnostics enabled: nominal 100 ms buckets, 15-second background writes; detailed timings=" + detail + ". Runtime overhead remains unverified.");
        }
        internal static void Update()
        {
            if (!active) return;
            try
            {
                double now = Now; cachedNow = now;
                collector.Frame(now);
                collector.ObserveHorde(GameTelemetry.HordeId, now);
                if (collector.BucketDue(now))
                {
                    bool wasSpawning = collector.Latest.State.Spawning;
                    var sample = GameTelemetry.Sample(); collector.CloseBucket(now, sample); sampler.Rotate();
                    if (wasSpawning && sample.SpawningKnown && !sample.Spawning) collector.SnapshotHorde(now, WindowEnd.SpawningStopped);
                }
                if (persist && now >= nextSlow)
                {
                    nextSlow = now + 1000; var b = collector.Batch; b.ManagedBytes = GC.GetTotalMemory(false);
                    hudData.ManagedBytes = b.ManagedBytes; hudData.ManagedReadMs = now;
                    collector.Horde.PeakManaged = Math.Max(collector.Horde.PeakManaged, b.ManagedBytes);
                    int a = GC.CollectionCount(0), c = GC.CollectionCount(1), d = GC.CollectionCount(2);
                    b.Gc0 += a - gc0; b.Gc1 += c - gc1; b.Gc2 += d - gc2; gc0 = a; gc1 = c; gc2 = d;
                }
                if (timing && now >= nextTiming) { nextTiming = now + 200; ReadTiming(now); }
                bool newHordeSummary = collector.HordeSummarySequence != lastHordeSummaryAttempt;
                if (newHordeSummary || collector.BatchDue(now))
                {
                    lastHordeSummaryAttempt = collector.HordeSummarySequence;
                    Publish(now, newHordeSummary ? WindowEnd.HordeChange : WindowEnd.Cadence);
                }
                if (Input.GetKeyDown(hudKey)) hud = !hud;
                if (Input.GetKeyDown(markerKey))
                {
                    string note = ModSettings.MarkerNote.Value ?? "";
                    if (note.Length > 80) note = note.Substring(0, 80);
                    note = note.Replace('\r', ' ').Replace('\n', ' ');
                    collector.Mark(++marker, cachedNow, note);
                }
                if (persist && Input.GetKeyDown(folderKey))
                {
                    try { Process.Start(new ProcessStartInfo(reportDirectory) { UseShellExecute = true }); }
                    catch { Plugin.Log.LogWarning("Could not open diagnostics folder; reports may not have been written yet."); }
                }
                if ((persist && now >= nextInventory) || (persist && Input.GetKeyDown(rescanKey)))
                { nextInventory = double.PositiveInfinity; collector.SetMetadata(CaptureInventory(), now); }
                if (hud && now >= nextHud) { nextHud = now + 250; RefreshHud(now); }
                if (writer != null && writer.Failed && !warnedWriter)
                { warnedWriter = true; Plugin.Log.LogWarning("Diagnostics writer failed. Gameplay continues; unsaved batches are counted. Check report-folder permissions and free space."); }
            }
            catch (Exception ex) { Plugin.Log.LogError("Diagnostics stopped: " + ex.GetType().Name); Stop(); }
        }
        private static string CaptureInventory()
        {
            try { return DiagnosticEnvironment.Capture(session); }
            catch (Exception ex)
            {
                // Optional inventory failure cannot stop the numeric collector or
                // expose arbitrary third-party exception messages and private paths.
                return "environment_capture_failed=true; error_type=" + ex.GetType().Name;
            }
        }
        private static void ReadTiming(double now)
        {
            try
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, Timing) == 0) return;
                var t = Timing[0];
                if (t.frameStartTimestamp == 0 || t.frameStartTimestamp == lastTiming) return;
                lastTiming = t.frameStartTimestamp;
                var b = collector.Batch; b.TimingSource = lastTiming; b.TimingReadMs = now;
                if (t.cpuMainThreadFrameTime > 0 && !double.IsInfinity(t.cpuMainThreadFrameTime)) { b.CpuTotal += t.cpuMainThreadFrameTime; b.CpuSamples++; }
                if (t.gpuFrameTime > 0 && !double.IsInfinity(t.gpuFrameTime)) { b.GpuTotal += t.gpuFrameTime; b.GpuSamples++; }
            }
            catch { timing = false; }
        }
        private static void Publish(double now, WindowEnd reason)
        {
            collector.CloseBucket(now, GameTelemetry.Sample());
            // Explicit boundaries can carry final horde/marker/metadata records
            // even when no time has elapsed since the previous publication.
            collector.Complete(now, reason); var b = collector.Batch;
            b.LifecycleAvailable = GameTelemetry.LifecycleAvailable;
            b.CorpseEventsAvailable = GameTelemetry.CorpseEventsAvailable;
            b.PlacementAvailable = FeatureRuntime.Enabled(Feature.Diagnostics);
            b.CategoryAvailable = GameTelemetry.LifecycleAvailable && FeatureRuntime.Enabled(Feature.Catalog);
            b.WrongThreadEvents = Interlocked.Read(ref WrongThreadEvents);
            b.WriterFailures = writer?.FailedBatches ?? 0;
            b.WriterLagMs = writer?.MaxLagMs ?? 0;
            b.DebugMode = ModSettings.DebugMode.Value; b.ProfilingMode = ModSettings.Profiling.Value;
            b.HudMode = hud; b.DetailedMode = detail; b.EngineTimingAvailable = timing;
            hudData.CaptureWindow(b);
            b.PublishedTicks = Stopwatch.GetTimestamp();
            if (writer != null) { writer.TryPublish(b, out var next); collector.Next(next, now); }
            else { b.Reset(now); collector.HasPendingHorde = false; }
            lastHordeSummaryAttempt = collector.HordeSummarySequence;
        }
        private static void RefreshHud(double now)
        {
            hudData.Detailed = detail; hudData.LifecycleAvailable = GameTelemetry.LifecycleAvailable;
            hudData.CorpseEventsAvailable = GameTelemetry.CorpseEventsAvailable;
            hudData.PlacementAvailable = FeatureRuntime.Enabled(Feature.Diagnostics);
            hudData.CategoryAvailable = GameTelemetry.LifecycleAvailable && FeatureRuntime.Enabled(Feature.Catalog);
            hudData.DisabledFeatures = FeatureRuntime.DisabledSummary;
            if (content == null) content = new GUIContent();
            content.text = hudData.Format(collector, writer, marker, now, WrongThreadEvents);
            hudLayoutDirty = true;
        }
        internal static void Draw()
        {
            if (!active || !hud || content == null || Event.current.type != EventType.Repaint) return;
            if (style == null) style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = Mathf.RoundToInt(14 * hudScale), richText = false, wordWrap = true };
            if (hudLayoutDirty)
            {
                // Recalculate only when cached text changes. Wrapped unavailable
                // values and disabled-feature names must not be clipped by a fixed height.
                bounds.height = style.CalcHeight(content, bounds.width) + 8 * hudScale;
                hudLayoutDirty = false;
            }
            GUI.Box(bounds, content, style);
        }
        internal static void Boundary(WindowEnd reason)
        {
            if (!active) return;
            try
            {
                double now = Now;
                collector.CloseBucket(now, GameTelemetry.Sample());
                if (reason == WindowEnd.SceneUnload) collector.EndHorde(now, reason);
                else if (reason == WindowEnd.Pause) collector.SnapshotHorde(now, reason);
                Publish(now, reason);
            }
            catch { Stop(); }
        }
        internal static void Stop()
        {
            if (!active) return;
            long droppedBeforeStop = collector.DroppedBatches;
            try
            {
                double now = Now;
                collector.CloseBucket(now, GameTelemetry.Sample());
                collector.EndHorde(now, WindowEnd.Shutdown); Publish(now, WindowEnd.Shutdown);
            }
            catch { Plugin.Log.LogWarning("Could not snapshot final diagnostics window; final data may be unsaved."); }
            finally
            {
                active = detail = false;
                if (collector.DroppedBatches > droppedBeforeStop) Plugin.Log.LogWarning("Final diagnostics window could not be queued; session dropped buckets=" + collector.DroppedBuckets + "; lost marker notes=" + collector.LostMarkerNotes + "; lost metadata snapshots=" + collector.LostMetadataSnapshots);
                if (writer != null && !writer.Stop(250))
                { writerQuarantined = true; Plugin.Log.LogWarning("Diagnostics shutdown incomplete; unwritten batches=" + writer.Unwritten); }
                writer = null;
            }
        }
    }
    [HarmonyPatch(typeof(Zombie_Agent), "_Update")]
    internal static class ProfileZombieUpdate
    {
        private static void Prefix(out TimingSample __state) => __state = PerformanceMonitor.Begin(ProfileSection.ZombieUpdate);
        private static Exception Finalizer(Exception __exception, TimingSample __state) { PerformanceMonitor.End(__state); return __exception; }
    }
    [HarmonyPatch(typeof(GPUI_Dead_Body_Mgr), "Spawn_GPUI_Dead_Body")]
    internal static class ProfileCorpseCreation
    {
        private static void Prefix(out TimingSample __state) => __state = PerformanceMonitor.Begin(ProfileSection.CorpseCreation);
        private static Exception Finalizer(Exception __exception, TimingSample __state) { PerformanceMonitor.End(__state); return __exception; }
    }
    [HarmonyPatch(typeof(NPC_Horde_Mgr), "GetValidSpawnPosition")]
    internal static class ProfilePlacement
    {
        private static void Prefix(out TimingSample __state) => __state = PerformanceMonitor.Begin(ProfileSection.Placement);
        private static Exception Finalizer(Exception __exception, TimingSample __state) { PerformanceMonitor.End(__state); return __exception; }
    }
    [HarmonyPatch(typeof(NPC_Horde_Mgr), "Save_Horde_Data_To_Disk")]
    internal static class ProfileHordeSave
    {
        private static void Prefix(out TimingSample __state) => __state = PerformanceMonitor.Begin(ProfileSection.HordeSave);
        private static Exception Finalizer(Exception __exception, TimingSample __state) { PerformanceMonitor.End(__state); return __exception; }
    }
}
