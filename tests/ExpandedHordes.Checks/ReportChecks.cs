using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ExpandedHordes;

internal static class ReportChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var fast = new FrameWindow(); var slow = new FrameWindow();
        for (int i = 0; i < 99; i++) fast.Add(1);
        slow.Add(101); fast.Merge(slow);
        check(fast.Count == 100 && fast.Total == 200 && fast.Mean == 2 && 1000 / fast.Mean == 500 && fast.Percentile95() == 1 && fast.Percentile(.99) == 1 && fast.Percentile(1) == 101,
            "Merged unequal frame windows use sample weights and nearest-rank percentiles, not averages of means/FPS");
        foreach (double value in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity, 0, -5 }) fast.Add(value);
        check(fast.Count == 100 && fast.Total == 200, "Invalid frame values do not contaminate weighted totals");
        var sampler = new DetailSampler(); var sampled = new TelemetryBatch();
        for (int section = 0; section < 7; section++) for (int i = 0; i < 320; i++) sampler.Select(section, sampled);
        check(sampled.Timed.All(n => n == 10) && sampled.Observed.All(n => n == 320) && sampled.Skipped.All(n => n == 0),
            "Uncapped detail sampling selects exactly 1/32 in each complete 320-call section");

        string directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "expanded-hordes-report-" + Guid.NewGuid().ToString("N")));
        try
        {
            var c = new TelemetryCollector(0, new TelemetryBatch());
            var state = PopulationSample.Unavailable; state.Budget = 20; state.Emitted = 3; state.Alive = 2;
            state.LivingTarget = 5; state.Spawning = state.SpawningKnown = true; state.GameMinutes = 123; state.Region = 4;
            c.ObserveHorde(2, 0); c.Frame(100); c.Record(TelemetryEvent.Fresh); c.Record(TelemetryEvent.Context); c.Record(TelemetryEvent.ContextFailed);
            c.CloseBucket(100, state); c.Mark(1, 105, "baseline"); c.Mark(2, 110, "end"); c.SetMetadata("fixture_inventory=true", 115);
            c.CloseBucket(120, state); c.EndHorde(120, WindowEnd.Shutdown); c.Complete(120, WindowEnd.Shutdown);
            var batch = c.Batch; batch.LifecycleAvailable = batch.PlacementAvailable = batch.CategoryAvailable = batch.CorpseEventsAvailable = true;
            batch.ProfilingMode = true; batch.DetailedMode = true;
            batch.Observed[0] = 320; batch.Timed[0] = 10; batch.CompletedTimings[0] = 9; batch.CrossWindowTimings = 1;
            using (var sink = new TelemetryFileSink(directory, "fixture", "fixture_manifest=true")) sink.Write(batch);
            string log = File.ReadAllText(Path.Combine(directory, "diagnostics.log"));
            check(log.Contains("environment_snapshot batch=1 time_ms=115") && log.Contains("fixture_inventory=true") &&
                log.Contains("test_marker id=1 action=start time_ms=105 note=baseline") && log.Contains("test_marker id=2 action=stop time_ms=110 note=end"),
                "Readback associates inventory and both marker actions with their monotonic session timeline");
            check(log.Contains("modes debug/profiling/hud/detailed=False/True/False/True") && log.Contains("horde_observation_cumulative id=2 reason=Shutdown") &&
                log.Contains("remaining=17 game_minutes=123 spawn_region=4 sampled_below_target_ms_horde=120") && log.Contains("context_failed/calls=1/1"),
                "Horde readback retains effective state, shortfall duration, context failures and active mode flags");
            check(log.Contains("observed=320 timed=10 cap_skipped=0") && log.Contains("completed=9") && log.Contains("detail_cross_window_completions_discarded=1"),
                "Detail readback distinguishes observed, selected, completed and cross-window counts");
            var empty = new TelemetryCollector(0, new TelemetryBatch()); empty.ObserveHorde(1, 0); empty.EndHorde(0, WindowEnd.Shutdown); empty.Complete(0, WindowEnd.Shutdown);
            string emptyRoot = Path.Combine(directory, "empty");
            using (var sink = new TelemetryFileSink(emptyRoot, "empty", "")) sink.Write(empty.Batch);
            log = File.ReadAllText(Path.Combine(emptyRoot, "diagnostics.log"));
            check(log.Contains("mean/p95/p99/max_ms=unavailable/unavailable/unavailable/unavailable") && log.Contains("mean_ms=unavailable") && log.Contains("achieved_hz=unavailable"),
                "Empty boundary frame distributions are unavailable in both window and horde reports");

            // Stress every numeric field with its widest finite representation,
            // independent of attainable gameplay values. Test output/retention bounds,
            // not transient formatting allocation against the working-buffer target.
            object boxedState = default(PopulationSample); Maximize(boxedState);
            object boxedBucket = default(TelemetryBucket); Maximize(boxedBucket);
            var value = (TelemetryBucket)boxedBucket; value.State = (PopulationSample)boxedState; value.EndMs = 0;
            var extreme = new TelemetryBatch(); extreme.Reset(0); extreme.Count = TelemetryBatch.Capacity;
            extreme.LifecycleAvailable = extreme.CorpseEventsAvailable = extreme.PlacementAvailable = extreme.CategoryAvailable = true;
            for (int i = 0; i < extreme.Count; i++) extreme.Buckets[i] = value;
            extreme.Metadata = new string('\u0800', TelemetryMetadata.MaxCharacters);
            extreme.MarkerCount = extreme.Markers.Length;
            for (int i = 0; i < extreme.MarkerCount; i++) extreme.Markers[i] = new TestMarker { Id = int.MaxValue, TimeMs = double.MaxValue, Note = new string('\u0800', 80) };
            extreme.HasHorde = true; extreme.Horde.Reset(int.MaxValue, 0); extreme.Horde.EndMs = double.MaxValue;
            extreme.Horde.Frames.Add(double.MaxValue); extreme.Horde.Last = (PopulationSample)boxedState;
            string stressRoot = Path.Combine(directory, "extreme");
            using (var sink = new TelemetryFileSink(stressRoot, "20260922T000000000", extreme.Metadata, 1)) sink.Write(extreme);
            string csv = File.ReadAllText(Path.Combine(stressRoot, "previous-performance.csv"));
            long csvBytes = new FileInfo(Path.Combine(stressRoot, "previous-performance.csv")).Length;
            long logBytes = new FileInfo(Path.Combine(stressRoot, "previous-diagnostics.log")).Length;
            check(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).All(line => line.TrimEnd('\r').Split(',').Length == TelemetryFileSink.Header.Split(',').Length) &&
                csvBytes < 1024 * 1024 && logBytes < 256 * 1024, "Extreme numeric and Unicode batch stays parseable with bounded rotation overshoot");
            Console.WriteLine($"HOST extreme-format retention fixture: CSV={csvBytes} bytes; log={logBytes} bytes; limits tested <1 MiB CSV and <256 KiB log per batch. Uses maximal numeric widths and metadata/marker capacities, not an expected hourly rate.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    private static void Maximize(object value)
    {
        foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            if (field.FieldType == typeof(long)) field.SetValue(value, long.MaxValue);
            else if (field.FieldType == typeof(int)) field.SetValue(value, int.MaxValue);
            else if (field.FieldType == typeof(double)) field.SetValue(value, double.MaxValue);
    }
}
