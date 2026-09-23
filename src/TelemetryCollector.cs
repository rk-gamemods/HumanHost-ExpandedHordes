using System;

namespace ExpandedHordes
{
    internal enum TelemetryEvent { Fresh, Restored, Death, OtherRemoval, CorpseAdded, CorpseRemoved, Placement, PlacementFailed, Context, ContextFailed, Large, Boss, DeathRegular, DeathLarge, DeathBoss, DeathUnknown, Count }
    internal static class DeathObservation
    {
        internal static bool ClassificationKnown(bool identityKnown, bool catalogComplete, bool explicitlyMatched) =>
            identityKnown && (catalogComplete || explicitlyMatched);
        internal static TelemetryEvent Classify(bool nativeBoss, bool catalogKnown, ZombieKind kind) =>
            nativeBoss ? TelemetryEvent.DeathBoss : !catalogKnown ? TelemetryEvent.DeathUnknown :
            kind == ZombieKind.Boss ? TelemetryEvent.DeathBoss : kind == ZombieKind.Large ? TelemetryEvent.DeathLarge : TelemetryEvent.DeathRegular;
        internal static TelemetryEvent Removal(bool before, bool after, bool matchingDeath, TelemetryEvent category)
        {
            var transition = LifecycleObservation.Removal(before, after, matchingDeath);
            return transition == TelemetryEvent.Death ? category : transition;
        }
    }
    internal enum WindowEnd { Cadence, HordeChange, SceneUnload, Shutdown, Pause, Resume, SpawningStopped }

    internal sealed class HordeObservation
    {
        internal readonly long[] Events = new long[(int)TelemetryEvent.Count];
        internal readonly FrameWindow Frames = new FrameWindow();
        internal int Id = -1, PeakAlive, PeakCorpses;
        internal double StartMs, EndMs, WorstWindowFps, SampledBelowTargetMs;
        private double windowStartMs, windowFrameMs;
        private long windowFrames;
        internal long PeakManaged, LostBuckets;
        internal long Epoch;
        internal PopulationSample Last;
        internal WindowEnd Reason;
        internal void Reset(int id, double now, long epoch = 0)
        {
            Id = id; Epoch = epoch; StartMs = EndMs = now; PeakAlive = PeakCorpses = -1;
            WorstWindowFps = double.PositiveInfinity; PeakManaged = -1; LostBuckets = 0;
            windowStartMs = now; windowFrameMs = 0; windowFrames = 0;
            SampledBelowTargetMs = 0;
            Last = PopulationSample.Unavailable; Array.Clear(Events, 0, Events.Length); Frames.Reset();
        }
        internal void Frame(double now, double milliseconds)
        {
            Frames.Add(milliseconds); windowFrameMs += milliseconds; windowFrames++;
            // A horde's full intervals are independent of partial file publications.
            // The entire spanning frame is retained, including after a stall.
            if (now - windowStartMs < 15000) return;
            WorstWindowFps = Math.Min(WorstWindowFps, 1000d * windowFrames / windowFrameMs);
            windowStartMs = now; windowFrameMs = 0; windowFrames = 0;
        }
        internal void CopyTo(HordeObservation target)
        {
            target.Reset(Id, StartMs, Epoch); target.EndMs = EndMs; target.PeakAlive = PeakAlive; target.PeakCorpses = PeakCorpses;
            target.WorstWindowFps = WorstWindowFps; target.PeakManaged = PeakManaged; target.LostBuckets = LostBuckets;
            target.SampledBelowTargetMs = SampledBelowTargetMs;
            target.Last = Last; target.Reason = Reason; Array.Copy(Events, target.Events, Events.Length); target.Frames.Merge(Frames);
        }
    }

    // All values are numeric and main-thread owned until the whole batch is published.
    internal struct PopulationSample
    {
        internal int Alive, Corpses, Horde, Budget, Emitted, LivingTarget, CorpseLimit, Region;
        internal bool Spawning, SpawningKnown;
        internal double GameMinutes;
        internal long Remaining => Budget < 0 || Emitted < 0 ? -1 : Math.Max(0L, (long)Budget - Emitted);
        internal static PopulationSample Unavailable => new PopulationSample
        { Alive = -1, Corpses = -1, Horde = -1, Budget = -1, Emitted = -1, LivingTarget = -1, CorpseLimit = -1, Region = -1, GameMinutes = -1 };
    }

    internal struct TelemetryBucket
    {
        internal long Sequence, Frames, Slow17, Slow33, Slow50, Missed;
        internal long FirstHordeEpoch, LastHordeEpoch;
        internal double StartMs, EndMs, FrameTotalMs, FrameMaxMs;
        internal long Fresh, Restored, Deaths, OtherRemoved, CorpseAdded, CorpseRemoved;
        internal long Placement, PlacementFailed, Context, ContextFailed, Large, Boss;
        internal long DeathRegular, DeathLarge, DeathBoss, DeathUnknown;
        internal int AliveMin, AliveMax, CorpseMin, CorpseMax, Marker;
        internal PopulationSample State;
        internal double Duration => EndMs - StartMs;
    }
    internal struct TestMarker
    {
        internal int Id;
        internal double TimeMs;
        internal string Note;
    }

    internal sealed class TelemetryBatch
    {
        internal const int Capacity = 150;
        internal readonly TelemetryBucket[] Buckets = new TelemetryBucket[Capacity];
        internal readonly FrameWindow Frames = new FrameWindow();
        internal readonly long[] Observed = new long[7], Timed = new long[7], CompletedTimings = new long[7], Skipped = new long[7], Ticks = new long[7], MaxTicks = new long[7];
        internal long Generation, CrossWindowTimings;
        internal int Count;
        internal long PublishedTicks;
        internal bool LifecycleAvailable = false, CorpseEventsAvailable = false;
        internal bool PlacementAvailable = false, CategoryAvailable = false;
        internal long WrongThreadEvents = 0, WriterFailures = 0;
        internal double WriterLagMs = 0;
        internal string Metadata;
        internal double MetadataTimeMs;
        internal bool DebugMode, ProfilingMode, HudMode, DetailedMode, EngineTimingAvailable;
        internal readonly long[] TotalSnapshot = new long[(int)TelemetryEvent.Count];
        internal readonly HordeObservation Horde = new HordeObservation();
        internal bool HasHorde;
        internal long LostHordeSummaries;
        internal readonly TestMarker[] Markers = new TestMarker[16];
        internal int MarkerCount;
        internal long LostMarkerNotes, LostMetadataSnapshots;
        internal long ReconciliationDelta;
        internal bool ReconciliationAvailable;
        internal double SampledBelowTargetMs;
        internal long Sequence, DroppedBatches, DroppedBuckets, ManagedBytes, Gc0, Gc1, Gc2;
        internal long CpuSamples, GpuSamples;
        internal double CpuTotal, GpuTotal, TimingReadMs;
        internal ulong TimingSource;
        internal WindowEnd Reason;
        internal double StartMs, EndMs;
        internal void Reset(double now)
        {
            Generation++; CrossWindowTimings = 0; Array.Clear(CompletedTimings, 0, CompletedTimings.Length);
            Count = MarkerCount = 0; LostMarkerNotes = LostMetadataSnapshots = 0; Array.Clear(Markers, 0, Markers.Length);
            Metadata = null; HasHorde = false; Frames.Reset(); StartMs = EndMs = now;
            ManagedBytes = -1; Gc0 = Gc1 = Gc2 = 0;
            CpuSamples = GpuSamples = 0; CpuTotal = GpuTotal = 0; TimingReadMs = -1; TimingSource = 0;
            Array.Clear(Observed, 0, 7); Array.Clear(Timed, 0, 7); Array.Clear(Skipped, 0, 7);
            Array.Clear(Ticks, 0, 7); Array.Clear(MaxTicks, 0, 7);
        }
    }

    // The host supplies monotonic milliseconds, making boundary policy testable without Unity.
    internal sealed class TelemetryCollector
    {
        internal TelemetryBatch Batch;
        internal TelemetryBucket Latest;
        internal readonly long[] Totals = new long[(int)TelemetryEvent.Count];
        internal readonly FrameWindow SessionFrames = new FrameWindow();
        internal readonly HordeObservation Horde = new HordeObservation();
        private readonly HordeObservation pendingHorde = new HordeObservation();
        internal bool HasPendingHorde;
        internal long HordeSummarySequence;
        internal long LostHordeSummaries;
        internal long DroppedBatches, DroppedBuckets;
        internal long LostMarkerNotes, LostMetadataSnapshots;
        internal int PeakAlive = -1, PeakCorpses = -1;
        private bool baselineKnown;
        private long aliveBaseline;
        internal double SampledBelowTargetMs;
        private TelemetryBucket bucket;
        private long bucketSequence, batchSequence;
        private long hordeEpoch;
        private bool bucketHasData;
        private double lastFrameMs;
        internal TelemetryCollector(double now, TelemetryBatch batch)
        {
            Batch = batch; batch.Reset(now); lastFrameMs = now; NewBucket(now);
            Latest.State = PopulationSample.Unavailable;
        }
        private void NewBucket(double now)
        {
            bucketHasData = false;
            bucket = new TelemetryBucket { StartMs = now, AliveMin = -1, AliveMax = -1, CorpseMin = -1, CorpseMax = -1, State = PopulationSample.Unavailable };
        }
        private void TouchHorde()
        {
            if (Horde.Id < 0) return;
            if (bucket.FirstHordeEpoch == 0) bucket.FirstHordeEpoch = Horde.Epoch;
            bucket.LastHordeEpoch = Horde.Epoch;
        }
        internal void Record(TelemetryEvent kind)
        {
            // Every confirmed death has exactly one category, including callers
            // which only know that a death occurred. Other removals never enter here.
            if (kind == TelemetryEvent.Death) kind = TelemetryEvent.DeathUnknown;
            if (kind >= TelemetryEvent.DeathRegular && kind <= TelemetryEvent.DeathUnknown)
            {
                Totals[(int)TelemetryEvent.Death]++;
                if (Horde.Id >= 0) Horde.Events[(int)TelemetryEvent.Death]++;
                bucket.Deaths++;
            }
            bucketHasData = true; TouchHorde();
            Totals[(int)kind]++;
            if (Horde.Id >= 0) Horde.Events[(int)kind]++;
            switch (kind)
            {
                case TelemetryEvent.Fresh: bucket.Fresh++; break;
                case TelemetryEvent.Restored: bucket.Restored++; break;
                case TelemetryEvent.DeathRegular: bucket.DeathRegular++; break;
                case TelemetryEvent.DeathLarge: bucket.DeathLarge++; break;
                case TelemetryEvent.DeathBoss: bucket.DeathBoss++; break;
                case TelemetryEvent.DeathUnknown: bucket.DeathUnknown++; break;
                case TelemetryEvent.OtherRemoval: bucket.OtherRemoved++; break;
                case TelemetryEvent.CorpseAdded: bucket.CorpseAdded++; break;
                case TelemetryEvent.CorpseRemoved: bucket.CorpseRemoved++; break;
                case TelemetryEvent.Placement: bucket.Placement++; break;
                case TelemetryEvent.PlacementFailed: bucket.PlacementFailed++; break;
                case TelemetryEvent.Context: bucket.Context++; break;
                case TelemetryEvent.ContextFailed: bucket.ContextFailed++; break;
                case TelemetryEvent.Large: bucket.Large++; break;
                case TelemetryEvent.Boss: bucket.Boss++; break;
            }
        }
        internal bool BucketDue(double now) => now - bucket.StartMs >= 100;
        internal bool BatchDue(double now) => now - Batch.StartMs >= 15000 || Batch.Count == TelemetryBatch.Capacity;
        internal void Mark(int marker, double now, string note)
        {
            bucketHasData = true;
            bucket.Marker = marker;
            if (Batch.MarkerCount == Batch.Markers.Length) { LostMarkerNotes++; Batch.LostMarkerNotes = LostMarkerNotes; return; }
            Batch.Markers[Batch.MarkerCount++] = new TestMarker { Id = marker, TimeMs = now, Note = note };
        }
        internal void SetMetadata(string metadata, double now = 0)
        {
            if (Batch.Metadata != null) LostMetadataSnapshots++;
            Batch.Metadata = metadata;
            Batch.MetadataTimeMs = now;
        }
        internal void Frame(double now)
        {
            double ms = now - lastFrameMs;
            if (ms <= 0 || double.IsInfinity(ms) || double.IsNaN(ms)) return;
            lastFrameMs = now;
            bucketHasData = true; TouchHorde();
            bucket.Frames++; bucket.FrameTotalMs += ms; bucket.FrameMaxMs = Math.Max(bucket.FrameMaxMs, ms);
            if (ms > 16.7) bucket.Slow17++;
            if (ms > 33.3) bucket.Slow33++;
            if (ms > 50) bucket.Slow50++;
            Batch.Frames.Add(ms); SessionFrames.Add(ms);
            if (Horde.Id >= 0) Horde.Frame(now, ms);
        }
        internal void ObserveHorde(int id, double now)
        {
            if (id == Horde.Id) return;
            EndHorde(now, WindowEnd.HordeChange);
            Horde.Reset(id, now, id >= 0 ? ++hordeEpoch : 0);
        }
        internal void EndHorde(double now, WindowEnd reason)
        {
            if (Horde.Id < 0) return;
            SnapshotHorde(now, reason);
            Horde.Reset(-1, now);
        }
        internal void SnapshotHorde(double now, WindowEnd reason)
        {
            if (Horde.Id < 0) return;
            if (HasPendingHorde) LostHordeSummaries++;
            Horde.EndMs = now; Horde.Reason = reason; Horde.CopyTo(pendingHorde); HasPendingHorde = true; HordeSummarySequence++;
        }
        internal void CloseBucket(double now, PopulationSample state)
        {
            if (now < bucket.StartMs || (now == bucket.StartMs && !bucketHasData) || Batch.Count == TelemetryBatch.Capacity) return;
            TouchHorde();
            bucket.EndMs = now; bucket.Sequence = ++bucketSequence; bucket.State = state;
            // One observation after a stall. The entire spanning frame belongs to this bucket.
            bucket.Missed = Math.Max(0, (long)Math.Floor((now - bucket.StartMs) / 100) - 1);
            bucket.AliveMin = bucket.AliveMax = state.Alive;
            bucket.CorpseMin = bucket.CorpseMax = state.Corpses;
            PeakAlive = Math.Max(PeakAlive, state.Alive); PeakCorpses = Math.Max(PeakCorpses, state.Corpses);
            if (Horde.Id >= 0)
            {
                Horde.Last = state; Horde.PeakAlive = Math.Max(Horde.PeakAlive, state.Alive); Horde.PeakCorpses = Math.Max(Horde.PeakCorpses, state.Corpses);
            }
            if (!baselineKnown && state.Alive >= 0)
            {
                aliveBaseline = state.Alive - (Totals[0] + Totals[1] - Totals[2] - Totals[3]); baselineKnown = true;
            }
            if (state.Spawning && state.Budget > state.Emitted && state.Alive >= 0 && state.Alive < state.LivingTarget)
            {
                SampledBelowTargetMs += bucket.Duration;
                if (Horde.Id >= 0) Horde.SampledBelowTargetMs += bucket.Duration;
            }
            Latest = bucket; Batch.Buckets[Batch.Count++] = bucket; NewBucket(now);
        }
        internal void Complete(double now, WindowEnd reason)
        {
            Batch.EndMs = now; Batch.Sequence = ++batchSequence; Batch.Reason = reason;
            Batch.DroppedBatches = DroppedBatches; Batch.DroppedBuckets = DroppedBuckets;
            Array.Copy(Totals, Batch.TotalSnapshot, Totals.Length);
            Batch.ReconciliationAvailable = baselineKnown && Latest.State.Alive >= 0;
            Batch.ReconciliationDelta = Latest.State.Alive - (aliveBaseline + Totals[0] + Totals[1] - Totals[2] - Totals[3]);
            Batch.SampledBelowTargetMs = SampledBelowTargetMs;
            if (HasPendingHorde) { pendingHorde.CopyTo(Batch.Horde); Batch.HasHorde = true; }
            Batch.LostHordeSummaries = LostHordeSummaries;
            Batch.LostMarkerNotes = LostMarkerNotes; Batch.LostMetadataSnapshots = LostMetadataSnapshots;
        }
        internal void Next(TelemetryBatch next, double now)
        {
            if (ReferenceEquals(next, Batch))
            {
                DroppedBatches++; DroppedBuckets += Batch.Count;
                LostMarkerNotes += Batch.MarkerCount;
                if (Batch.Metadata != null) LostMetadataSnapshots++;
                if (Horde.Id >= 0) Horde.LostBuckets += LostBucketsFor(Horde.Epoch);
                if (HasPendingHorde) pendingHorde.LostBuckets += LostBucketsFor(pendingHorde.Epoch);
            }
            else HasPendingHorde = false;
            Batch = next; Batch.Reset(now);
        }
        private int LostBucketsFor(long epoch)
        {
            int lost = 0;
            for (int i = 0; i < Batch.Count; i++)
            {
                var value = Batch.Buckets[i];
                if (value.FirstHordeEpoch > 0 && value.FirstHordeEpoch <= epoch && value.LastHordeEpoch >= epoch) lost++;
            }
            return lost;
        }
    }

    // A game callback may unload a scene or publish a batch before its finalizer.
    // Never write that completion into an unrelated or already writer-owned window.
    internal readonly struct TimingSample
    {
        private readonly TelemetryBatch owner;
        private readonly long generation, started;
        private readonly int section;
        internal bool Active => owner != null;
        internal TimingSample(TelemetryBatch batch, int section, long timestamp)
        { owner = batch; generation = batch.Generation; started = timestamp; this.section = section; }
        internal void Complete(TelemetryBatch current, long timestamp)
        {
            if (owner == null) return;
            if (!ReferenceEquals(owner, current) || generation != current.Generation)
            { current.CrossWindowTimings++; return; }
            long elapsed = Math.Max(0, timestamp - started);
            current.CompletedTimings[section]++; current.Ticks[section] += elapsed;
            current.MaxTicks[section] = Math.Max(current.MaxTicks[section], elapsed);
        }
    }

    // Per-section quotas prevent stable iteration order from starving later sections.
    // Phase changes every interval without touching game RNG; 7 * 14 <= 100 timings.
    internal sealed class DetailSampler
    {
        private readonly long[] calls = new long[7];
        private readonly int[] used = new int[7];
        private int phase;
        internal void Rotate() { phase = (phase + 13) & 31; Array.Clear(used, 0, used.Length); Array.Clear(calls, 0, calls.Length); }
        internal bool Select(int section, TelemetryBatch batch)
        {
            batch.Observed[section]++;
            if (((calls[section]++ + phase + section * 7) & 31) != 0) return false;
            if (used[section] >= 14) { batch.Skipped[section]++; return false; }
            used[section]++; batch.Timed[section]++; return true;
        }
    }
}
