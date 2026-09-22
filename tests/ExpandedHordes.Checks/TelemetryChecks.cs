using System;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;
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
        TestHordeWindows(check);
        BoundaryChecks.Run(check);
        TestTimingCompletion(check);
        TestAccessorsAndTransitions(check);
        TestHud(check);
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

    private static void TestHordeWindows(Action<bool, string> check)
    {
        var c = new TelemetryCollector(0, new TelemetryBatch());
        var state = PopulationSample.Unavailable; state.Alive = 5; state.Corpses = 9;
        c.ObserveHorde(1, 0);
        for (int i = 1; i <= 150; i++)
        {
            c.Frame(i * 100); c.CloseBucket(i * 100, state);
            if (i == 50 || i == 100)
            {
                c.SnapshotHorde(i * 100, WindowEnd.SpawningStopped);
                c.Complete(i * 100, WindowEnd.Pause); c.Next(new TelemetryBatch(), i * 100);
            }
        }
        c.EndHorde(15000, WindowEnd.Shutdown); c.Complete(15000, WindowEnd.Shutdown);
        check(c.Batch.Horde.WorstWindowFps == 10,
            "Horde worst 15-second FPS survives partial publications and includes final completed interval");
        check(c.Batch.Horde.Frames.Count == 150 && c.Batch.Horde.Frames.Total == 15000,
            "Horde mean uses all frames independently of batch boundaries");
        c.Next(new TelemetryBatch(), 15000); c.ObserveHorde(2, 15000);
        c.Frame(15010); c.CloseBucket(15010, state); c.EndHorde(15010, WindowEnd.SceneUnload); c.Complete(15010, WindowEnd.SceneUnload);
        check(double.IsPositiveInfinity(c.Batch.Horde.WorstWindowFps),
            "Short new horde cannot inherit prior horde's completed-window FPS");
    }

    private static void TestTimingCompletion(Action<bool, string> check)
    {
        var batch = new TelemetryBatch(); batch.Reset(0);
        var outer = new TimingSample(batch, 0, 0);
        var inner = new TimingSample(batch, 1, 5);
        try { throw new InvalidOperationException("Simulated original-method exception"); }
        catch (InvalidOperationException) { }
        finally { inner.Complete(batch, 15); outer.Complete(batch, 40); }
        check(batch.CompletedTimings[0] == 1 && batch.CompletedTimings[1] == 1 && batch.Ticks[0] == 40 && batch.Ticks[1] == 10,
            "Nested timing completion retains separate inclusive sections across exceptions, including timestamp zero");
        var crossing = new TimingSample(batch, 0, 50);
        var next = new TelemetryBatch(); next.Reset(100);
        crossing.Complete(next, 110);
        check(next.CrossWindowTimings == 1 && next.CompletedTimings[0] == 0 && next.Ticks[0] == 0 && batch.Ticks[0] == 40,
            "Completion after publication cannot mutate writer-owned data or charge a new window");
        batch.Reset(200); crossing.Complete(batch, 210);
        check(batch.CrossWindowTimings == 1 && batch.CompletedTimings[0] == 0,
            "Reused or dropped buffer generation rejects a stale completion");
        default(TimingSample).Complete(batch, 300);
        check(batch.CrossWindowTimings == 1 && batch.Ticks[0] == 0,
            "Disabled or unselected timing sample performs no bookkeeping");
        var warmed = new TimingSample(batch, 0, 300); warmed.Complete(batch, 310);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) new TimingSample(batch, 0, i).Complete(batch, i + 1);
        check(GC.GetAllocatedBytesForCurrentThread() == before, "Timing sample capture/completion allocates zero bytes after warm-up");
    }

    private sealed class PopulationFixture
    {
        internal Dictionary<int, object> Living;
        internal List<object> Unsupported = new List<object>();
    }
    private static void TestAccessorsAndTransitions(Action<bool, string> check)
    {
        var fixture = new PopulationFixture();
        var field = typeof(PopulationFixture).GetField(nameof(PopulationFixture.Living), BindingFlags.Instance | BindingFlags.NonPublic);
        var count = TelemetryAccessors.DictionaryCount<PopulationFixture>(field);
        check(count != null && count(fixture) == -1, "Uninitialized dictionary is unavailable, not an exception or zero");
        check(TelemetryAccessors.DictionaryCount<PopulationFixture>(null) == null &&
            TelemetryAccessors.DictionaryCount<PopulationFixture>(typeof(PopulationFixture).GetField(nameof(PopulationFixture.Unsupported), BindingFlags.Instance | BindingFlags.NonPublic)) == null,
            "Missing/wrong collection access fails independently without an enumeration fallback");
        fixture.Living = new Dictionary<int, object>();
        check(count(fixture) == 0, "Known empty dictionary is distinct from unavailable");
        var entity = new object();
        var collector = new TelemetryCollector(0, new TelemetryBatch());
        void Register(bool succeeds, bool restored)
        {
            bool before = fixture.Living.ContainsKey(1);
            if (succeeds) fixture.Living[1] = entity;
            var e = LifecycleObservation.Registration(before, fixture.Living.ContainsKey(1), restored);
            if (e != TelemetryEvent.Count) collector.Record(e);
        }
        void Remove(bool succeeds, bool dead)
        {
            bool before = fixture.Living.ContainsKey(1);
            if (succeeds) fixture.Living.Remove(1);
            var e = LifecycleObservation.Removal(before, fixture.Living.ContainsKey(1), dead);
            if (e != TelemetryEvent.Count) collector.Record(e);
        }
        Register(false, false); Register(true, true); Register(true, true); Remove(false, true);
        check(collector.Totals[0] == 0 && collector.Totals[1] == 1 && collector.Totals[2] == 0 && count(fixture) == 1,
            "Failed attempts, duplicate restoration and failed removal produce no invented lifecycle counts");
        Remove(true, true); Remove(true, true); Register(true, false); Remove(true, false);
        check(collector.Totals[0] == 1 && collector.Totals[1] == 1 && collector.Totals[2] == 1 && collector.Totals[3] == 1 && count(fixture) == 0,
            "Reused identity can register again; duplicate death is ignored and live pool return is other removal");
        fixture.Living = null;
        check(count(fixture) == -1, "Collection disposal returns unavailable without disabling the accessor");
        fixture.Living = new Dictionary<int, object> { [2] = entity, [3] = entity };
        check(count(fixture) == 2, "Typed accessor follows replacement collection after load");
        long beforeBytes = GC.GetAllocatedBytesForCurrentThread(); int total = 0;
        for (int i = 0; i < 1000; i++) total += count(fixture);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
        check(total == 2000 && allocated == 0, "Warmed typed dictionary reads allocate zero bytes on host .NET");
    }

    private static void TestHud(Action<bool, string> check)
    {
        var c = new TelemetryCollector(0, new TelemetryBatch()); var hud = new TelemetryHud();
        string initial = hud.Format(c, null, 0, 0, 0);
        check(initial.Contains("Frame window unavailable") && initial.Contains("Alive unavailable/unavailable peak unavailable"),
            "HUD startup cannot display unsampled population or empty histogram as authoritative zero");
        check(initial.Contains("Placement failures/calls unavailable/unavailable") && initial.Contains("CPU/GPU delayed window ms unavailable/unavailable"),
            "HUD distinguishes unsupported hooks and absent timing samples from measured zeros");
        var state = PopulationSample.Unavailable;
        state.SpawningKnown = true; state.Spawning = false; state.Alive = 10;
        c.Record(TelemetryEvent.Fresh); c.Frame(100); c.CloseBucket(100, state); c.Complete(100, WindowEnd.Cadence);
        c.Batch.CpuSamples = 2; c.Batch.CpuTotal = 10;
        hud.LifecycleAvailable = true; hud.PlacementAvailable = true; hud.CorpseEventsAvailable = false;
        hud.Detailed = false; hud.ManagedBytes = 1048576; hud.ManagedReadMs = 100;
        hud.CaptureWindow(c.Batch); c.Next(new TelemetryBatch(), 100);
        string text = hud.Format(c, null, 3, 350, 2);
        check(text.Contains("| IDLE | light") && text.Contains("Registered fresh 1") && text.Contains("Window 0.1s, age 0.25s | FPS 10"),
            "HUD shows known idle state and labels completed aggregate duration and age");
        check(text.Contains("CPU/GPU delayed window ms 5/unavailable | samples 2/0") && text.Contains("Managed 1 MiB, age 0.25s"),
            "HUD preserves completed timing coverage and slow-memory value across batch reset");
        check(text.Contains("Placement failures/calls 0/0") && text.Contains("adds/removes unavailable/unavailable") && text.Contains("marker 3"),
            "HUD displays measured zero only for available hooks and keeps marker identity");
        hud.Format(c, null, 3, 350, 2);
        long before = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
        for (int i = 0; i < 100; i++) text = hud.Format(c, null, 3, 350, 2);
        long elapsed = Stopwatch.GetTimestamp() - started, allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"HOST HUD text formatter: {allocated / 100d:0.###} bytes/refresh; mean={elapsed * 1000000d / Stopwatch.Frequency / 100:0.###} us/refresh; excludes Unity GUIContent/style/draw and game adapters.");
        GC.KeepAlive(text);
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
        check(writer.Stop(2000), "Completed writer shutdown is idempotent");
        var failure = new BlockedSink(true); var failing = new TelemetryWriter(failure);
        failing.TryPublish(failing.Initial, out _); check(failure.Entered.Wait(2000), "Failure sink reached");
        failure.Release.Set();
        check(!failing.Stop(2000) && failing.Failed && failing.FailedBatches == 1, "Writer failure contained and reported");
        check(!failing.Stop(2000) && failing.Unwritten == 1, "Repeated failed shutdown preserves unsaved-data status");
        var stalled = new BlockedSink(); var stalledWriter = new TelemetryWriter(stalled);
        stalledWriter.TryPublish(stalledWriter.Initial, out _); check(stalled.Entered.Wait(2000), "Shutdown stall reached");
        check(!stalledWriter.Stop(1) && stalledWriter.Unwritten == 1, "Shutdown wait is bounded and retains owned buffers");
        stalled.Release.Set(); check(stalledWriter.Stop(2000), "Timed-out worker can complete without producer reclaiming buffers");

        string dir = Path.Combine(Path.GetTempPath(), "expanded-hordes-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string blockedPath = Path.Combine(dir, "not-a-directory");
            File.WriteAllText(blockedPath, "sentinel");
            var deniedWriter = new TelemetryWriter(new TelemetryFileSink(blockedPath, "unwritable", "test"));
            check(deniedWriter.TryPublish(deniedWriter.Initial, out _), "Unwritable report path is handled by worker after handoff");
            check(!deniedWriter.Stop(2000) && deniedWriter.Failed && deniedWriter.Unwritten == 1 &&
                File.ReadAllText(blockedPath) == "sentinel", "Real file-system failure preserves caller file and reports unsaved batch");
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
