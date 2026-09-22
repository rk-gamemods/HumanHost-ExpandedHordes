using System;

namespace ExpandedHordes
{
    // Pure presentation of the shared collector. Unity owns only the cached GUIContent/style.
    internal sealed class TelemetryHud
    {
        internal bool LifecycleAvailable, CorpseEventsAvailable, PlacementAvailable, Detailed;
        internal long ManagedBytes = -1;
        internal double ManagedReadMs = -1;
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
        internal string Format(TelemetryCollector collector, TelemetryWriter writer, int marker, double now, long rejected)
        {
            var s = collector.Latest.State; var t = collector.Totals;
            string state = !s.SpawningKnown ? "unavailable" : s.Spawning ? "SPAWNING" : "IDLE";
            string frame = hasWindow
                ? $"Window {N((endMs - startMs) / 1000)}s, age {N((now - endMs) / 1000)}s | FPS {N(1000 / mean)} | p95/p99/max ~{N(p95)}/{N(p99)}/{N(maximum)} ms"
                : "Frame window unavailable (waiting for first completed aggregate)";
            string memory = ManagedBytes < 0 ? "unavailable" : N(ManagedBytes / 1048576d) + " MiB, age " + N((now - ManagedReadMs) / 1000) + "s";
            return $"Expanded Hordes | {state} | {(Detailed ? "detailed" : "light")} | horde {N(s.Horde)}\n" +
                $"Registered fresh {Count(t[0], LifecycleAvailable)} | restored {Count(t[1], LifecycleAvailable)} | budget/emitted {N(s.Budget)}/{N(s.Emitted)}\n" +
                $"Alive {N(s.Alive)}/{N(s.LivingTarget)} peak {N(collector.PeakAlive)} | deaths {Count(t[2], LifecycleAvailable)} | other removals {Count(t[3], LifecycleAvailable)}\n" +
                $"Settled pool {N(s.Corpses)}/{N(s.CorpseLimit)} peak {N(collector.PeakCorpses)} | adds/removes {Count(t[4], CorpseEventsAvailable)}/{Count(t[5], CorpseEventsAvailable)}\n" +
                $"Placement failures/calls {Count(t[7], PlacementAvailable)}/{Count(t[6], PlacementAvailable)} | last window fresh/min {(hasWindow && LifecycleAvailable ? N(spawnRate) : "unavailable")} | marker {marker}\n" +
                frame + "\n" +
                $"CPU/GPU delayed window ms {N(cpuSamples > 0 ? cpu : -1)}/{N(gpuSamples > 0 ? gpu : -1)} | samples {cpuSamples}/{gpuSamples}\n" +
                $"Managed {memory} | writer {(writer == null ? "off" : writer.Failed ? "FAILED" : "active")} pending {writer?.Unwritten ?? 0}\n" +
                $"Lost buckets {collector.DroppedBuckets} | rejected off-thread events {rejected} | lifecycle/placement/corpse {(LifecycleAvailable ? "on" : "UNAVAILABLE")}/{(PlacementAvailable ? "on" : "UNAVAILABLE")}/{(CorpseEventsAvailable ? "on" : "UNAVAILABLE")}";
        }
    }
}
