using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ExpandedHordes;

internal static class TelemetryChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var f = new FrameWindow(); f.Add(.251); f.Add(512); f.Add(3270);
        check(f.Percentile(.01) == .5 && f.Percentile(.5) == 512 && double.IsPositiveInfinity(f.Percentile95()), "Histogram bin bounds and overflow");
        var other = new FrameWindow(); other.Add(10); other.Merge(f);
        check(other.Count == 4 && other.Total == 3792.251 && other.Maximum == 3270 && other.Overflow == 1, "Histogram merge retains weights, overflow and exact max");
        other.Reset(); check(other.Count == 0 && other.Overflow == 0 && other.Percentile95() == 0, "Histogram reset");
        var state = PopulationSample.Unavailable; state.Alive = 1000; state.Corpses = 5000; state.Spawning = true; state.SpawningKnown = true;
        var c = new TelemetryCollector(0, new TelemetryBatch());
        for (int i = 1; i <= 10; i++) { c.Frame(i * 10); c.Record(TelemetryEvent.Fresh); }
        c.CloseBucket(100, state);
        check(c.Latest.Frames == 10 && c.Latest.Fresh == 10 && c.Latest.Duration == 100 && c.Latest.Missed == 0, "Regular bucket and event alignment");
        c.Frame(427); c.Record(TelemetryEvent.Death); c.CloseBucket(427, state);
        check(c.Latest.Frames == 1 && c.Latest.FrameMaxMs == 327 && c.Latest.Missed == 2 && c.Batch.Count == 2, "327 ms freeze is one full long frame and one real observation");
        c.Frame(5427); c.CloseBucket(5427, state);
        check(c.Latest.Missed == 49 && c.Batch.Count == 3 && c.Latest.FrameMaxMs == 5000, "Multi-second stall does not fabricate catch-up samples");
        c.Record(TelemetryEvent.Restored); c.Record(TelemetryEvent.OtherRemoval); c.CloseBucket(5470, state); c.Complete(5470, WindowEnd.Shutdown);
        check(c.Latest.Duration == 43 && c.Latest.Restored == 1 && c.Latest.OtherRemoved == 1 && c.Batch.Reason == WindowEnd.Shutdown, "Partial stop preserves events");
        check(c.Batch.Buckets.Take(c.Batch.Count).Sum(b => b.Fresh) == c.Totals[0] && c.Totals[0] == 10 && c.Totals[1] == 1 && c.Totals[2] == 1, "Fresh, restoration, death and removal remain distinct");
        c.Next(c.Batch, 5470);
        check(c.DroppedBatches == 1 && c.DroppedBuckets == 4 && c.Totals[0] == 10 && c.SessionFrames.Count == 12, "Dropping a detail batch preserves session counts/distribution");
        c.Record(TelemetryEvent.Placement); c.Record(TelemetryEvent.ContextFailed);
        c.Frame(5510); c.CloseBucket(5510, state);
        check(c.Latest.Fresh == 0 && c.Latest.Placement == 1 && c.Latest.ContextFailed == 1, "Rotation resets only interval counters");

        var sampler = new DetailSampler(); var samples = new TelemetryBatch();
        bool[] phases = new bool[32];
        for (int epoch = 0; epoch < 32; epoch++)
        {
            sampler.Rotate(); samples.Reset(0);
            for (int section = 0; section < 7; section++)
                for (int call = 0; call < 10000; call++)
                    if (sampler.Select(section, samples) && section == 0) phases[call & 31] = true;
            check(samples.Timed.Sum() == 98 && samples.Timed.All(n => n == 14) && samples.Skipped.All(n => n > 0), "Timing cap and section fairness");
        }
        check(phases.All(v => v), "Rotating sampler covers stable-order phases");
        check(samples.Observed.Sum() == 70000, "Observed calls include skipped calls");
        var epochs = new TelemetryCollector(0, new TelemetryBatch());
        epochs.ObserveHorde(7, 0); epochs.Record(TelemetryEvent.Fresh); epochs.Frame(100); epochs.CloseBucket(100, state);
        epochs.Record(TelemetryEvent.Death); epochs.Frame(200); epochs.CloseBucket(200, state);
        epochs.ObserveHorde(8, 200); epochs.Record(TelemetryEvent.Restored); epochs.Complete(200, WindowEnd.HordeChange);
        check(epochs.Batch.HasHorde && epochs.Batch.Horde.Id == 7 && epochs.Batch.Horde.Events[0] == 1 && epochs.Batch.Horde.Events[2] == 1 && epochs.Batch.Horde.Frames.Count == 2, "Horde change snapshots prior totals and full histogram");
        epochs.Next(epochs.Batch, 200);
        check(epochs.HasPendingHorde && epochs.Horde.Id == 8 && epochs.Horde.Events[1] == 1, "Writer backpressure preserves pending summary and new horde totals");
        epochs.Complete(300, WindowEnd.Cadence); epochs.Next(new TelemetryBatch(), 300);
        check(!epochs.HasPendingHorde && epochs.Totals[0] == 1 && epochs.Totals[1] == 1, "Accepted summary releases pending slot without clearing session totals");
        epochs.EndHorde(350, WindowEnd.SceneUnload); epochs.Complete(350, WindowEnd.SceneUnload);
        check(epochs.Batch.Horde.Id == 8 && epochs.Batch.Horde.Reason == WindowEnd.SceneUnload && epochs.Batch.Horde.Events[1] == 1, "Scene unload preserves partial horde summary");
        var reconcile = new TelemetryCollector(0, new TelemetryBatch());
        state.Alive = 10; reconcile.CloseBucket(100, state); reconcile.Record(TelemetryEvent.Fresh);
        state.Alive = 11; reconcile.CloseBucket(200, state); reconcile.Complete(200, WindowEnd.Cadence);
        check(reconcile.Batch.ReconciliationDelta == 0, "Observed registration reconciles with authoritative population");
        state.Alive = 4; reconcile.CloseBucket(300, state); reconcile.Complete(300, WindowEnd.Cadence);
        check(reconcile.Batch.ReconciliationDelta == -7 && reconcile.Totals[(int)TelemetryEvent.Death] == 0, "Unexplained disappearance is a discrepancy, never invented kills");
        for (int i = 1; i <= 17; i++) reconcile.Mark(i, 300, "phase");
        check(reconcile.Batch.MarkerCount == 16 && reconcile.Batch.LostMarkerNotes == 1 && reconcile.Batch.Markers[0].Id == 1, "Marker notes are bounded with visible loss");
        TestWriter(check);
        state.Alive = 1000;
        Benchmark(check, state, 100); Benchmark(check, state, 1000);
        long beforeBuffers = GC.GetAllocatedBytesForCurrentThread();
        var buffers = new TelemetryBatch[4]; for (int i = 0; i < 4; i++) buffers[i] = new TelemetryBatch();
        var collectorBuffers = new TelemetryCollector(0, buffers[0]); var samplerBuffers = new DetailSampler();
        long bufferBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBuffers;
        check(bufferBytes < 1024 * 1024, "Collector and batch numeric buffers under 1 MiB on host runtime");
        Console.WriteLine($"HOST numeric collector/four-batch/sampler allocations: {bufferBytes} bytes; excludes writer/runtime/metadata/UI.");
        GC.KeepAlive(buffers); GC.KeepAlive(collectorBuffers); GC.KeepAlive(samplerBuffers);
    }

    private sealed class BlockedSink : ITelemetrySink
    {
        internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim(), Release = new ManualResetEventSlim();
        internal readonly bool Fail;
        internal int Count;
        internal long FirstSequence;
        internal BlockedSink(bool fail = false) { Fail = fail; }
        public void Write(TelemetryBatch b)
        {
            Entered.Set(); Release.Wait();
            if (Fail) throw new IOException("Injected disk-full/permission failure");
            if (Count++ == 0) FirstSequence = b.Sequence;
        }
        public void Dispose() { }
    }
    private static void TestWriter(Action<bool, string> check)
    {
        var sink = new BlockedSink(); var writer = new TelemetryWriter(sink);
        var b = writer.Initial; b.Sequence = 1; b.PublishedTicks = Stopwatch.GetTimestamp();
        check(writer.TryPublish(b, out var next), "First batch accepted");
        check(sink.Entered.Wait(2000), "Writer entered blocked sink");
        next.Sequence = 2; check(writer.TryPublish(next, out next), "Second fixed buffer accepted");
        next.Sequence = 3; check(writer.TryPublish(next, out next), "Third fixed buffer accepted");
        next.Sequence = 4; var start = Stopwatch.GetTimestamp();
        check(!writer.TryPublish(next, out var same) && ReferenceEquals(same, next), "Full writer drops newest, retains producer ownership");
        check(Stopwatch.GetElapsedTime(start).TotalMilliseconds < 100, "Blocked disk cannot block producer");
        next.Sequence = 40; sink.Release.Set();
        check(writer.Stop(2000) && sink.Count == 3 && sink.FirstSequence == 1, "Queued buffer ownership and orderly drain");
        var failure = new BlockedSink(true); var failing = new TelemetryWriter(failure);
        failing.TryPublish(failing.Initial, out _); check(failure.Entered.Wait(2000), "Failure sink reached");
        failure.Release.Set();
        check(!failing.Stop(2000) && failing.Failed && failing.FailedBatches == 1, "Writer failure contained and reported");
        var stalled = new BlockedSink(); var stalledWriter = new TelemetryWriter(stalled);
        stalledWriter.TryPublish(stalledWriter.Initial, out _); check(stalled.Entered.Wait(2000), "Shutdown stall reached");
        check(!stalledWriter.Stop(1) && stalledWriter.Unwritten == 1, "Shutdown wait is bounded and retains owned buffers");
        stalled.Release.Set(); check(stalledWriter.Stop(2000), "Timed-out worker can complete without producer reclaiming buffers");

        string dir = Path.Combine(Path.GetTempPath(), "expanded-hordes-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var collector = new TelemetryCollector(0, new TelemetryBatch());
            collector.Record(TelemetryEvent.Fresh); collector.Frame(327); collector.CloseBucket(327, PopulationSample.Unavailable); collector.Complete(327, WindowEnd.Shutdown);
            collector.Batch.LifecycleAvailable = true;
            // Production formatter, independently parsed below under a non-English culture.
            var old = CultureInfo.CurrentCulture; CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            try { using var file = new TelemetryFileSink(dir, "test", "privacy allowlist", 1); file.Write(collector.Batch); }
            finally { CultureInfo.CurrentCulture = old; }
            var lines = File.ReadAllLines(Path.Combine(dir, "previous-performance.csv"));
            var names = lines[0].Split(','); var values = lines[1].Split(',');
            check(values.Length == names.Length && values[Array.IndexOf(names, "duration_ms")] == "327", "CSV schema and invariant numeric serialization");
            check(values[Array.IndexOf(names, "fresh")] == "1" && values[Array.IndexOf(names, "corpse_added")] == "-1", "Unavailable is not zero-filled");
            check(File.ReadAllLines(Path.Combine(dir, "performance.csv"))[0] == TelemetryFileSink.Header, "Rotated file retains schema");
            check(File.ReadAllText(Path.Combine(dir, "previous-diagnostics.log")).Contains("reason=Shutdown"), "Partial shutdown is readable");
            Console.WriteLine($"OUTPUT: bucket row UTF8 bytes={System.Text.Encoding.UTF8.GetByteCount(lines[1]) + 2}; projected 10 Hz bytes/hour={(System.Text.Encoding.UTF8.GetByteCount(lines[1]) + 2) * 36000L}; struct bytes={Marshal.SizeOf<TelemetryBucket>()}");
            string formatDir = Path.Combine(dir, "format");
            var full = new TelemetryCollector(0, new TelemetryBatch());
            var population = PopulationSample.Unavailable; population.Alive = 1000; population.Corpses = 5000; population.Budget = 400000;
            for (int i = 1; i <= 150; i++) { full.Frame(i * 100); full.Record(TelemetryEvent.Fresh); full.CloseBucket(i * 100, population); }
            full.Complete(15000, WindowEnd.Cadence); full.Batch.LifecycleAvailable = true;
            using (var format = new TelemetryFileSink(formatDir, "synthetic-session", "synthetic environment"))
            {
                format.Write(full.Batch);
                long originalBytes = Directory.GetFiles(formatDir).Sum(p => new FileInfo(p).Length);
                long allocated = GC.GetAllocatedBytesForCurrentThread(), formatStart = Stopwatch.GetTimestamp();
                format.Write(full.Batch);
                long elapsed = Stopwatch.GetTimestamp() - formatStart, formattingBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                long outputBytes = Directory.GetFiles(formatDir).Sum(p => new FileInfo(p).Length) - originalBytes;
                Console.WriteLine($"HOST warmed 150-bucket formatter + buffered flush: {elapsed * 1000d / Stopwatch.Frequency:F3} ms; allocations={formattingBytes} bytes; output={outputBytes} bytes/batch; extrapolated={outputBytes * 240} bytes/hour (synthetic workload, excludes manifests/horde snapshots).");
            }
        }
        finally { Directory.Delete(dir, true); }
    }
    private static void Benchmark(Action<bool, string> check, PopulationSample state, int eventsPerBucket)
    {
        var c = new TelemetryCollector(0, new TelemetryBatch());
        c.ObserveHorde(1, 0);
        var times = new double[10000];
        double now = 0;
        for (int round = 0; round < 2; round++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < times.Length; i++)
            {
                for (int j = 0; j < eventsPerBucket; j++) c.Record(TelemetryEvent.Fresh);
                c.Frame(now += 100);
                long start = Stopwatch.GetTimestamp();
                c.CloseBucket(now, state);
                times[i] = (Stopwatch.GetTimestamp() - start) * 1000000d / Stopwatch.Frequency;
                if (c.BatchDue(now)) { c.Complete(now, WindowEnd.Cadence); c.Next(c.Batch, now); }
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (round == 1) check(allocated == 0, "Warmed event/frame/bucket/rotation operations allocate zero bytes on host .NET");
        }
        Array.Sort(times);
        Console.WriteLine($"HOST .NET benchmark: {eventsPerBucket * 10} event callbacks/s, 2,000 simulated seconds; bucket median/p95/p99/max us={times[5000]:F3}/{times[9500]:F3}/{times[9900]:F3}/{times[^1]:F3}; zero-byte hot loop checked. Unity Mono/Harmony/HUD/engine/writer overhead NOT measured.");
    }
}
