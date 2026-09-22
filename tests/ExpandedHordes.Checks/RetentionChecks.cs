using System;
using System.IO;
using System.Linq;
using ExpandedHordes;

internal static class RetentionChecks
{
    internal static void Run(Action<bool, string> check)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "expanded-hordes-retention-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        try
        {
            bool rejected = false;
            try { new TelemetryFileSink(root, "invalid", "manifest", 0); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected && Directory.GetFiles(root).Length == 0, "Invalid retention threshold is rejected before touching files");
            var collector = new TelemetryCollector(0, new TelemetryBatch());
            collector.Frame(100); collector.CloseBucket(100, PopulationSample.Unavailable); collector.Complete(100, WindowEnd.Cadence);
            using (var sink = new TelemetryFileSink(root, "first", "first_manifest", 1024 * 1024))
                sink.Write(collector.Batch);
            check(!File.Exists(Path.Combine(root, "previous-performance.csv")), "Large configured threshold retains current data without early rotation");
            using (var sink = new TelemetryFileSink(root, "second", "second_manifest", 1024 * 1024))
                sink.Write(collector.Batch);
            check(File.ReadAllText(Path.Combine(root, "previous-environment.txt")).Contains("session=first") &&
                File.ReadAllLines(Path.Combine(root, "previous-performance.csv"))[1].Split(',')[1] == "first" &&
                File.ReadAllText(Path.Combine(root, "environment.txt")).Contains("session=second"), "Session rollover pairs current/previous data with their manifest identity");
            using (var sink = new TelemetryFileSink(root, "third", "third_manifest", 1))
            {
                for (int i = 0; i < 10; i++) sink.Write(collector.Batch);
            }
            string[] files = Directory.GetFiles(root).Select(Path.GetFileName).OrderBy(x => x).ToArray();
            check(files.SequenceEqual(new[] { "diagnostics.log", "environment.txt", "performance.csv", "previous-diagnostics.log", "previous-environment.txt", "previous-performance.csv" }),
                "Repeated rotation keeps exactly two files per stream and two manifests, with no history growth");
            check(File.ReadAllLines(Path.Combine(root, "performance.csv"))[0] == TelemetryFileSink.Header &&
                File.ReadAllLines(Path.Combine(root, "previous-performance.csv"))[0] == TelemetryFileSink.Header &&
                File.ReadAllLines(Path.Combine(root, "previous-performance.csv"))[1].Split(',')[1] == "third",
                "Repeated tiny-threshold rotation preserves schema and row session identity");
            check(File.ReadAllText(Path.Combine(root, "diagnostics.log")).Contains("rotation; retained history bounded"),
                "Rotation reports history loss instead of silently implying a full-session archive");
        }
        finally { Directory.Delete(root, true); }
    }
}
