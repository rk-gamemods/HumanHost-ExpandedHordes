using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using ExpandedHordes;

internal static class WriterResourceChecks
{
    private sealed class MeasuredFileSink : ITelemetrySink
    {
        private readonly TelemetryFileSink file;
        internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim(), Release = new ManualResetEventSlim();
        internal long FirstWriteBytes;
        private int writes;
        internal MeasuredFileSink(string directory, string manifest) { file = new TelemetryFileSink(directory, "buffers", manifest); }
        public void Write(TelemetryBatch batch)
        {
            Entered.Set();
            if (!Release.Wait(2000)) throw new TimeoutException("Resource fixture was not released");
            long before = GC.GetAllocatedBytesForCurrentThread();
            file.Write(batch);
            if (writes++ == 0) FirstWriteBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        public void Dispose() => file.Dispose();
    }
    private sealed class SignalSink : ITelemetrySink
    {
        internal readonly AutoResetEvent Written = new AutoResetEvent(false);
        public void Write(TelemetryBatch batch) => Written.Set();
        public void Dispose() { }
    }
    internal static void Run(Action<bool, string> check)
    {
        TestContention(check);
        string directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "expanded-hordes-buffers-" + Guid.NewGuid().ToString("N")));
        char[] maximumMetadata = new char[TelemetryMetadata.MaxCharacters];
        Array.Fill(maximumMetadata, 'm');
        try
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var capture = new TelemetryMetadata(null, null); // Include a concurrent capture's fixed builder/value storage.
            var sink = new MeasuredFileSink(directory, new string(maximumMetadata));
            var writer = new TelemetryWriter(sink);
            var collector = new TelemetryCollector(0, writer.Initial);
            var sampler = new DetailSampler(); var hud = new TelemetryHud();
            var state = PopulationSample.Unavailable; state.Alive = 1000; state.Corpses = 5000;
            double now = 0;
            for (int batch = 0; batch < 4; batch++)
            {
                collector.Batch.Metadata = new string(maximumMetadata);
                for (int marker = 0; marker < 16; marker++) collector.Mark(marker, now, new string('n', 80));
                for (int i = 0; i < 150; i++)
                { collector.Frame(now += 100); collector.Record(TelemetryEvent.Fresh); collector.CloseBucket(now, state); }
                collector.Complete(now, WindowEnd.Cadence);
                if (batch == 3) break; // Fourth full buffer stays producer-owned.
                if (!writer.TryPublish(collector.Batch, out var next)) throw new Exception("Resource fixture could not acquire all four buffers");
                collector.Next(next, now);
                if (batch == 0 && !sink.Entered.Wait(2000)) throw new Exception("Resource fixture writer did not start");
            }
            long producerBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            sink.Release.Set(); check(writer.Stop(2000), "Full-metadata resource fixture drains real file sink");
            long conservativeBytes = producerBytes + sink.FirstWriteBytes;
            Console.WriteLine($"HOST collector/writer allocation envelope: producer construction/full buffers={producerBytes}; first real writer batch={sink.FirstWriteBytes}; combined={conservativeBytes} bytes. Includes four maximal metadata snapshots, manifest, 64 marker notes, concurrent metadata builder, streams/encoding, and first-batch formatting; excludes Unity adapters/UI assets and runtime-specific dispatch. This is a conservative allocation envelope, not whole-process RAM.");
            check(conservativeBytes <= 1024 * 1024, "Collector/writer host allocation envelope stays under 1 MiB including metadata and real streams");
            GC.KeepAlive(capture); GC.KeepAlive(collector); GC.KeepAlive(sampler); GC.KeepAlive(hud);
            sink.Entered.Dispose(); sink.Release.Dispose();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    private static void TestContention(Action<bool, string> check)
    {
        var sink = new SignalSink(); var writer = new TelemetryWriter(sink);
        object gate = typeof(TelemetryWriter).GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(writer);
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var holder = new Thread(() => { lock (gate) { entered.Set(); release.Wait(2000); } });
        holder.IsBackground = true; holder.Start();
        check(entered.Wait(2000), "Contention fixture owns the production handoff gate");
        long begin = Stopwatch.GetTimestamp();
        bool accepted = writer.TryPublish(writer.Initial, out var current);
        double rejectedMicroseconds = Stopwatch.GetElapsedTime(begin).TotalMicroseconds;
        release.Set(); holder.Join();
        check(!accepted && ReferenceEquals(current, writer.Initial) && rejectedMicroseconds < 100000,
            "Contended handoff fails immediately and preserves producer ownership");
        var times = new double[1000]; int successes = 0, rejected = 0;
        for (int i = 0; i < times.Length; i++)
        {
            current.PublishedTicks = Stopwatch.GetTimestamp();
            begin = Stopwatch.GetTimestamp(); accepted = writer.TryPublish(current, out current);
            times[i] = Stopwatch.GetElapsedTime(begin).TotalMicroseconds;
            if (!accepted) { rejected++; continue; }
            successes++;
            if (!sink.Written.WaitOne(2000)) throw new Exception("Handoff benchmark failed to drain");
        }
        check(writer.Stop(2000) && successes > 0, "Handoff benchmark publishes and drains production buffers");
        sink.Written.Dispose(); Array.Sort(times);
        Console.WriteLine($"HOST production handoff: successes={successes}; immediate_rejections={rejected}; median/p95/p99/max_us={times[500]:0.###}/{times[950]:0.###}/{times[990]:0.###}/{times[999]:0.###}; forced_contention_reject_us={rejectedMicroseconds:0.###}. Includes producer scheduling noise; no Unity dispatch measured.");
    }
}
