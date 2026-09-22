using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace ExpandedHordes
{
    internal interface ITelemetrySink : IDisposable { void Write(TelemetryBatch batch); }

    // Four fixed buffers. Only the 15-second handoff uses synchronization; TryEnter
    // never waits on the producer. The writer never holds this gate during I/O.
    internal sealed class TelemetryWriter
    {
        private readonly object gate = new object();
        private readonly TelemetryBatch[] free = new TelemetryBatch[3], queue = new TelemetryBatch[3];
        private int freeCount = 3, head, queued;
        private readonly ITelemetrySink sink;
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly Thread thread;
        private volatile bool stopping, failed;
        private bool joined;
        private long written, failedBatches, maxLagTicks;
        private long accepted;
        internal long Unwritten => Interlocked.Read(ref accepted) - Written;
        internal TelemetryBatch Initial { get; } = new TelemetryBatch();
        internal long Written => Interlocked.Read(ref written);
        internal long FailedBatches => Interlocked.Read(ref failedBatches);
        internal double MaxLagMs => Interlocked.Read(ref maxLagTicks) * 1000d / Stopwatch.Frequency;
        internal bool Failed => failed;
        internal TelemetryWriter(ITelemetrySink sink)
        {
            this.sink = sink;
            for (int i = 0; i < free.Length; i++) free[i] = new TelemetryBatch();
            thread = new Thread(Run) { IsBackground = true, Name = "Expanded Hordes diagnostics" };
            thread.Start();
        }
        internal bool TryPublish(TelemetryBatch completed, out TelemetryBatch replacement)
        {
            replacement = completed;
            if (stopping || failed || !Monitor.TryEnter(gate)) return false;
            try
            {
                if (freeCount == 0) return false;
                replacement = free[--freeCount]; free[freeCount] = null;
                queue[(head + queued) % queue.Length] = completed; queued++;
                Interlocked.Increment(ref accepted);
            }
            finally { Monitor.Exit(gate); }
            wake.Set(); return true;
        }
        private void Run()
        {
            try
            {
                while (true)
                {
                    TelemetryBatch batch = null;
                    lock (gate)
                    {
                        if (queued > 0) { batch = queue[head]; queue[head] = null; head = (head + 1) % queue.Length; queued--; }
                    }
                    if (batch == null)
                    {
                        if (stopping) break;
                        wake.WaitOne(100); continue;
                    }
                    long lag = Math.Max(0, Stopwatch.GetTimestamp() - batch.PublishedTicks);
                    if (lag > Interlocked.Read(ref maxLagTicks)) Interlocked.Exchange(ref maxLagTicks, lag);
                    try { sink.Write(batch); Interlocked.Increment(ref written); }
                    catch { failed = true; Interlocked.Increment(ref failedBatches); }
                    finally { lock (gate) free[freeCount++] = batch; }
                    if (failed) break;
                }
            }
            catch { failed = true; }
            finally { try { sink.Dispose(); } catch { failed = true; } }
        }
        internal bool Stop(int milliseconds)
        {
            // The producer owns Stop, including retries after a bounded timeout.
            if (joined) return !failed;
            stopping = true; wake.Set();
            joined = thread.Join(milliseconds);
            // A timed-out worker retains its buffers and handle; it may still be writing.
            // Never reclaim those objects on the Unity thread.
            if (joined) wake.Dispose();
            return joined && !failed;
        }
    }

    // Constructed on the main thread with copied strings only; all file operations
    // and formatting happen in Write/Dispose on the persistent worker.
    internal sealed class TelemetryFileSink : ITelemetrySink
    {
        internal const string Header = "schema,session,batch,seq,start_ms,duration_ms,frames,frame_sum_ms,frame_max_ms,gt16_7,gt33_3,gt50,missed,horde,spawning,budget,emitted,alive_sample,corpse_pool_sample,living_target,corpse_limit,fresh,restored,deaths,other_removed,corpse_added,corpse_removed,placement,placement_failed,context,context_failed,large,boss,marker,dropped_batches,dropped_buckets,game_minutes,spawn_region";
        private readonly string directory, session, manifest;
        private readonly long limit;
        private StreamWriter csv, log;
        private readonly StringBuilder row = new StringBuilder(1024);
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal TelemetryFileSink(string directory, string session, string manifest, long limit = 5 * 1024 * 1024)
        { this.directory = directory; this.session = session; this.manifest = manifest; this.limit = limit; }
        private StreamWriter Open(string name, bool header)
        {
            string path = Path.Combine(directory, name);
            bool empty = !File.Exists(path) || new FileInfo(path).Length == 0;
            var writer = new StreamWriter(path, true, new UTF8Encoding(false), 16384);
            if (empty && header) writer.WriteLine(Header);
            return writer;
        }
        private void Rotate(ref StreamWriter writer, string name, bool header)
        {
            writer.Flush();
            if (writer.BaseStream.Length < limit) return;
            writer.Dispose(); writer = null;
            string path = Path.Combine(directory, name), previous = Path.Combine(directory, "previous-" + name);
            if (File.Exists(previous)) File.Delete(previous);
            File.Move(path, previous); writer = Open(name, header);
            if (!header) writer.WriteLine("session=" + session + " rotation; retained history bounded to current/previous files");
        }
        internal static string Number(double value) => double.IsInfinity(value) ? "out_of_range" : double.IsNaN(value) ? "unavailable" : value.ToString("0.###", Inv);
        private void Field(long value) { row.Append(','); row.Append(value.ToString(Inv)); }
        private void Field(double value) { row.Append(','); row.Append(Number(value)); }
        public void Write(TelemetryBatch b)
        {
            if (csv == null)
            {
                Directory.CreateDirectory(directory);
                foreach (string name in new[] { "performance.csv", "diagnostics.log" })
                {
                    string current = Path.Combine(directory, name), previous = Path.Combine(directory, "previous-" + name);
                    if (File.Exists(current))
                    {
                        if (File.Exists(previous)) File.Delete(previous);
                        File.Move(current, previous);
                    }
                }
                // One manifest for each retained session; current and previous only.
                string path = Path.Combine(directory, "environment.txt"), old = Path.Combine(directory, "previous-environment.txt");
                if (File.Exists(old)) File.Delete(old);
                if (File.Exists(path)) File.Move(path, old);
                File.WriteAllText(path, "session=" + session + Environment.NewLine + manifest);
                csv = Open("performance.csv", true); log = Open("diagnostics.log", false);
                log.WriteLine("session=" + session + " schema=1; -1 means unavailable; population is sampled; timings are delayed; no causal attribution");
            }
            if (b.Metadata != null) { log.WriteLine(b.Metadata); b.Metadata = null; }
            for (int i = 0; i < b.MarkerCount; i++)
            {
                var marker = b.Markers[i];
                log.WriteLine("test_marker id=" + marker.Id + " action=" + ((marker.Id & 1) == 1 ? "start" : "stop") + " time_ms=" + Number(marker.TimeMs) + " note=" + marker.Note);
            }
            if (b.LostMarkerNotes > 0) log.WriteLine("lost_marker_notes=" + b.LostMarkerNotes);
            for (int i = 0; i < b.Count; i++)
            {
                var v = b.Buckets[i];
                row.Clear(); row.Append("1,").Append(session); Field(b.Sequence); Field(v.Sequence);
                Field(v.StartMs); Field(v.Duration); Field(v.Frames); Field(v.FrameTotalMs); Field(v.FrameMaxMs);
                Field(v.Slow17); Field(v.Slow33); Field(v.Slow50); Field(v.Missed); Field(v.State.Horde);
                Field(!v.State.SpawningKnown ? -1 : v.State.Spawning ? 1 : 0); Field(v.State.Budget); Field(v.State.Emitted); Field(v.State.Alive); Field(v.State.Corpses);
                Field(v.State.LivingTarget); Field(v.State.CorpseLimit);
                Field(b.LifecycleAvailable ? v.Fresh : -1); Field(b.LifecycleAvailable ? v.Restored : -1); Field(b.LifecycleAvailable ? v.Deaths : -1); Field(b.LifecycleAvailable ? v.OtherRemoved : -1);
                Field(b.CorpseEventsAvailable ? v.CorpseAdded : -1); Field(b.CorpseEventsAvailable ? v.CorpseRemoved : -1);
                Field(b.PlacementAvailable ? v.Placement : -1); Field(b.PlacementAvailable ? v.PlacementFailed : -1); Field(b.PlacementAvailable ? v.Context : -1); Field(b.PlacementAvailable ? v.ContextFailed : -1);
                Field(b.CategoryAvailable ? v.Large : -1); Field(b.CategoryAvailable ? v.Boss : -1); Field(v.Marker); Field(b.DroppedBatches); Field(b.DroppedBuckets);
                Field(v.State.GameMinutes); Field(v.State.Region);
                csv.WriteLine(row.ToString());
            }
            row.Clear(); row.Append("window=").Append(b.Sequence).Append(" reason=").Append(b.Reason)
                .Append(" start_ms=").Append(Number(b.StartMs)).Append(" duration_ms=").Append(Number(b.EndMs - b.StartMs))
                .Append(" frames=").Append(b.Frames.Count).Append(" mean/p95/p99/max_ms=").Append(Number(b.Frames.Mean))
                .Append('/').Append(Number(b.Frames.Percentile95())).Append('/').Append(Number(b.Frames.Percentile(.99))).Append('/').Append(Number(b.Frames.Maximum))
                .Append(" cpu/gpu_delayed_ms=").Append(b.CpuSamples == 0 ? "unavailable" : Number(b.CpuTotal / b.CpuSamples))
                .Append('/').Append(b.GpuSamples == 0 ? "unavailable" : Number(b.GpuTotal / b.GpuSamples))
                .Append(" cpu/gpu_samples=").Append(b.CpuSamples).Append('/').Append(b.GpuSamples)
                .Append(" last_timing_source_raw=").Append(b.TimingSource).Append(" timing_read_ms=").Append(Number(b.TimingReadMs))
                .Append(" managed_bytes=").Append(b.ManagedBytes).Append(" gc0/1/2=").Append(b.Gc0).Append('/').Append(b.Gc1).Append('/').Append(b.Gc2)
                .Append(" dropped_batches/buckets=").Append(b.DroppedBatches).Append('/').Append(b.DroppedBuckets);
            log.WriteLine(row.ToString());
            log.WriteLine("writer_failures=" + b.WriterFailures + " max_writer_lag_ms=" + Number(b.WriterLagMs) + " off_thread_events_rejected=" + b.WrongThreadEvents);
            log.WriteLine("alive_reconciliation_delta=" + (b.ReconciliationAvailable && b.LifecycleAvailable ? b.ReconciliationDelta.ToString(Inv) : "unavailable") + " sampled_below_target_ms_session=" + Number(b.SampledBelowTargetMs));
            long fresh = 0, deaths = 0, restored = 0, other = 0, placements = 0, rejected = 0, missed = 0;
            int aliveMin = int.MaxValue, aliveMax = -1, corpseMin = int.MaxValue, corpseMax = -1;
            for (int i = 0; i < b.Count; i++)
            {
                var v = b.Buckets[i]; fresh += v.Fresh; restored += v.Restored; deaths += v.Deaths; other += v.OtherRemoved;
                placements += v.Placement; rejected += v.PlacementFailed; missed += v.Missed;
                if (v.State.Alive >= 0) { aliveMin = Math.Min(aliveMin, v.State.Alive); aliveMax = Math.Max(aliveMax, v.State.Alive); }
                if (v.State.Corpses >= 0) { corpseMin = Math.Min(corpseMin, v.State.Corpses); corpseMax = Math.Max(corpseMax, v.State.Corpses); }
            }
            log.WriteLine(string.Format(Inv, "window_events fresh={0} restored={1} deaths={2} other_removed={3} placement_failed/calls={4}/{5} sampled_alive_min/max={6}/{7} sampled_corpse_min/max={8}/{9} missed_boundaries={10} achieved_hz={11:0.###}",
                b.LifecycleAvailable ? fresh : -1, b.LifecycleAvailable ? restored : -1, b.LifecycleAvailable ? deaths : -1, b.LifecycleAvailable ? other : -1,
                b.PlacementAvailable ? rejected : -1, b.PlacementAvailable ? placements : -1, aliveMin == int.MaxValue ? -1 : aliveMin, aliveMax,
                corpseMin == int.MaxValue ? -1 : corpseMin, corpseMax, missed, b.EndMs > b.StartMs ? b.Count * 1000d / (b.EndMs - b.StartMs) : 0));
            row.Clear(); row.Append("session_totals");
            for (int i = 0; i < b.TotalSnapshot.Length; i++)
                row.Append(' ').Append((TelemetryEvent)i).Append('=').Append((i == 4 || i == 5 ? b.CorpseEventsAvailable : i >= 10 ? b.CategoryAvailable : i >= 6 ? b.PlacementAvailable : b.LifecycleAvailable) ? b.TotalSnapshot[i] : -1);
            log.WriteLine(row.ToString());
            for (int i = 0; i < 7; i++)
                if (b.Observed[i] != 0)
                    log.WriteLine(string.Format(Inv, "detail section={0} observed={1} timed={2} cap_skipped={3} sampled_inclusive_ms={4:0.###} sampled_max_ms={5:0.###} completed={6}",
                        i, b.Observed[i], b.Timed[i], b.Skipped[i], b.Ticks[i] * 1000d / Stopwatch.Frequency, b.MaxTicks[i] * 1000d / Stopwatch.Frequency, b.CompletedTimings[i]));
            log.WriteLine("detail_cross_window_completions_discarded=" + b.CrossWindowTimings);
            if (b.HasHorde)
            {
                var h = b.Horde;
                log.WriteLine(string.Format(Inv, "horde_observation_cumulative id={0} reason={1} duration_ms={2:0.###} fresh={3} restored={4} deaths={5} other_removed={6} peak_alive={7} peak_pool={8} frame_count={9} mean_ms={10} p95_approx_ms={11} p99_approx_ms={12} max_ms={13} lost_buckets={14} peak_managed_bytes={15} worst_completed_window_fps={16}",
                    h.Id, h.Reason, h.EndMs - h.StartMs, b.LifecycleAvailable ? h.Events[0] : -1, b.LifecycleAvailable ? h.Events[1] : -1,
                    b.LifecycleAvailable ? h.Events[2] : -1, b.LifecycleAvailable ? h.Events[3] : -1, h.PeakAlive, h.PeakCorpses,
                    h.Frames.Count, Number(h.Frames.Mean), Number(h.Frames.Percentile95()), Number(h.Frames.Percentile(.99)), Number(h.Frames.Maximum), h.LostBuckets,
                    h.PeakManaged, double.IsInfinity(h.WorstWindowFps) ? "unavailable" : Number(h.WorstWindowFps)));
                log.WriteLine("horde_start_ms=" + Number(h.StartMs) + " budget/emitted=" + h.Last.Budget + "/" + h.Last.Emitted + " living_target=" + h.Last.LivingTarget + " corpse_limit=" + h.Last.CorpseLimit +
                    " known_fresh_large/boss=" + (b.CategoryAvailable ? h.Events[10] : -1) + "/" + (b.CategoryAvailable ? h.Events[11] : -1) +
                    " placement_failed/calls=" + (b.PlacementAvailable ? h.Events[7] : -1) + "/" + (b.PlacementAvailable ? h.Events[6] : -1) +
                    " corpse_pool_adds/removes=" + (b.CorpseEventsAvailable ? h.Events[4] : -1) + "/" + (b.CorpseEventsAvailable ? h.Events[5] : -1));
            }
            log.WriteLine("lost_horde_summaries=" + b.LostHordeSummaries);
            Rotate(ref csv, "performance.csv", true); Rotate(ref log, "diagnostics.log", false);
        }
        public void Dispose() { try { csv?.Dispose(); } finally { log?.Dispose(); } }
    }
}
