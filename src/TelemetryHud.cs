using System;

namespace ExpandedHordes
{
    // Strings are prepared at the coordinator's 4 Hz cadence, never during repaint.
    internal sealed class HudSnapshot
    {
        internal readonly string Status, Horde, Alive, AliveTarget, Spawned, Remaining, Fps, FrameTime, WindowAge, Conflicts, Recording;
        internal readonly bool Spawning, StateKnown, FrameStale, HasProgress, HasWarning, RecordingWarning;
        internal readonly float Progress;
        internal HudSnapshot(string status, string horde, string alive, string aliveTarget, string spawned, string remaining,
            string fps, string frameTime, string windowAge, string conflicts, string recording, bool spawning, bool stateKnown,
            bool frameStale, bool hasProgress, float progress, bool hasWarning, bool recordingWarning)
        {
            Status = status; Horde = horde; Alive = alive; AliveTarget = aliveTarget; Spawned = spawned; Remaining = remaining;
            Fps = fps; FrameTime = frameTime; WindowAge = windowAge; Conflicts = conflicts; Recording = recording;
            Spawning = spawning; StateKnown = stateKnown; FrameStale = frameStale; HasProgress = hasProgress;
            Progress = progress; HasWarning = hasWarning; RecordingWarning = recordingWarning;
        }
    }
    // Pure presentation of the shared collector. Unity owns only the cached GUIContent/style.
    internal sealed class TelemetryHud
    {
        internal bool LifecycleAvailable, CorpseEventsAvailable, PlacementAvailable, CategoryAvailable, Detailed;
        internal long ManagedBytes = -1;
        internal double ManagedReadMs = -1;
        internal string DisabledFeatures = "none";
        private bool hasWindow;
        private double mean, p95, p99, maximum, startMs, endMs, spawnRate, cpu, gpu;
        private long cpuSamples, gpuSamples;
        internal void CaptureWindow(TelemetryBatch batch)
        {
            hasWindow = batch.Frames.Count > 0;
            mean = batch.Frames.Mean; p95 = batch.Frames.Percentile95(); p99 = batch.Frames.Percentile(.99);
            maximum = batch.Frames.Maximum; startMs = batch.StartMs; endMs = batch.EndMs;
            long fresh = 0;
            for (int i = 0; i < batch.Count; i++) fresh += batch.Buckets[i].Fresh;
            spawnRate = endMs > startMs ? fresh * 60000d / (endMs - startMs) : 0;
            cpuSamples = batch.CpuSamples; gpuSamples = batch.GpuSamples;
            cpu = cpuSamples > 0 ? batch.CpuTotal / cpuSamples : -1;
            gpu = gpuSamples > 0 ? batch.GpuTotal / gpuSamples : -1;
        }
        private static string N(double value) => value < 0 ? "unavailable" : TelemetryFileSink.Number(value);
        private static string Count(long value, bool available) => available ? N(value) : "unavailable";
        private static string Whole(long value) => value < 0 ? "?" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        internal HudSnapshot BuildSnapshot(TelemetryCollector collector, TelemetryWriter writer, double now, long rejected,
            int conflictCount, bool scanIncomplete)
        {
            var s = collector.Latest.State;
            double age = Math.Max(0, now - endMs) / 1000d;
            bool frames = hasWindow && mean > 0 && !double.IsNaN(mean) && !double.IsInfinity(mean);
            bool stale = frames && age > 20;
            bool progress = s.Budget > 0 && s.Emitted >= 0;
            string conflicts = conflictCount > 0
                ? "Hotkeys: " + Whole(conflictCount) + " possible overlaps - see report"
                : scanIncomplete ? "Hotkey scan incomplete - see report" : "No hotkey overlaps found; coverage partial";
            bool lost = collector.DroppedBuckets > 0 || collector.LostMetadataSnapshots > 0 || rejected > 0;
            bool disabled = !string.IsNullOrEmpty(DisabledFeatures) && DisabledFeatures != "none";
            string recording = writer == null ? "Reports off" : writer.Failed ? "Report writer failed - see log"
                : lost ? "Report data incomplete - see log" : disabled ? "Some features unavailable - see log" : "Recording reports";
            return new HudSnapshot(
                !s.SpawningKnown ? "Horde status unavailable" : s.Spawning ? "Spawning horde" : "Not spawning",
                s.Horde >= 0 ? "HORDE " + Whole(s.Horde) : "HORDE ?",
                Whole(s.Alive), s.LivingTarget < 0 ? "cap unknown" : "/ " + Whole(s.LivingTarget) + " cap",
                "Spawned " + Whole(s.Emitted) + " / " + Whole(s.Budget),
                s.Remaining < 0 ? "budget unknown" : Whole(s.Remaining) + " unspawned budget",
                frames ? (1000d / mean).ToString("0", System.Globalization.CultureInfo.InvariantCulture) : "--",
                frames ? "95% below ~" + p95.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms" : "Frame timing unavailable",
                frames ? (stale ? "Stale sample" : "Last " + ((endMs - startMs) / 1000d).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "s sample")
                    + " | " + age.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "s ago" : "Waiting for a completed sample",
                conflicts, recording, s.Spawning, s.SpawningKnown, stale, progress,
                progress ? (float)Math.Max(0, Math.Min(1, (double)s.Emitted / s.Budget)) : 0,
                conflictCount > 0 || scanIncomplete, writer == null || writer.Failed || lost || disabled);
        }
        internal string Format(TelemetryCollector collector, TelemetryWriter writer, double now, long rejected)
        {
            var s = collector.Latest.State; var t = collector.Totals;
            string state = !s.SpawningKnown ? "unavailable" : s.Spawning ? "SPAWNING" : "IDLE";
            string frame = hasWindow
                ? $"Window {N((endMs - startMs) / 1000)}s, age {N((now - endMs) / 1000)}s | FPS {N(1000 / mean)} | p95/p99/max ~{N(p95)}/{N(p99)}/{N(maximum)} ms"
                : "Frame window unavailable (waiting for first completed aggregate)";
            string memory = ManagedBytes < 0 ? "unavailable" : N(ManagedBytes / 1048576d) + " MiB, age " + N((now - ManagedReadMs) / 1000) + "s";
            return $"Expanded Hordes | {state} | {(Detailed ? "detailed" : "light")} | horde {N(s.Horde)}\n" +
                $"Registered fresh {Count(t[0], LifecycleAvailable)} | restored {Count(t[1], LifecycleAvailable)} | budget/emitted {N(s.Budget)}/{N(s.Emitted)} remaining {N(s.Remaining)}\n" +
                $"Alive {N(s.Alive)}/{N(s.LivingTarget)} peak {N(collector.PeakAlive)} | deaths {Count(t[2], LifecycleAvailable)} | other removals {Count(t[3], LifecycleAvailable)}\n" +
                $"Settled pool {N(s.Corpses)}/{N(s.CorpseLimit)} peak {N(collector.PeakCorpses)} | adds/removes {Count(t[4], CorpseEventsAvailable)}/{Count(t[5], CorpseEventsAvailable)}\n" +
                $"Placement failures/calls {Count(t[7], PlacementAvailable)}/{Count(t[6], PlacementAvailable)} | last window fresh/min {(hasWindow && LifecycleAvailable ? N(spawnRate) : "unavailable")}\n" +
                $"Context failures/calls {Count(t[9], PlacementAvailable)}/{Count(t[8], PlacementAvailable)} | known fresh large/boss {Count(t[10], CategoryAvailable)}/{Count(t[11], CategoryAvailable)}\n" +
                frame + "\n" +
                $"CPU/GPU delayed window ms {N(cpuSamples > 0 ? cpu : -1)}/{N(gpuSamples > 0 ? gpu : -1)} | samples {cpuSamples}/{gpuSamples}\n" +
                $"Managed {memory} | writer {(writer == null ? "off" : writer.Failed ? "FAILED" : "active")} pending {writer?.Unwritten ?? 0}\n" +
                $"Lost buckets/inventories {collector.DroppedBuckets}/{collector.LostMetadataSnapshots} | rejected off-thread {rejected} | lifecycle/placement/corpse {(LifecycleAvailable ? "on" : "UNAVAILABLE")}/{(PlacementAvailable ? "on" : "UNAVAILABLE")}/{(CorpseEventsAvailable ? "on" : "UNAVAILABLE")}\n" +
                $"Disabled features: {DisabledFeatures}";
        }
    }
}
