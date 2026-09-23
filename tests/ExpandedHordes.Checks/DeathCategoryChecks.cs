using System;
using System.IO;
using System.Linq;
using ExpandedHordes;

internal static class DeathCategoryChecks
{
    internal static void Run(Action<bool, string> check)
    {
        check(!DeathObservation.ClassificationKnown(true, false, false) &&
            DeathObservation.ClassificationKnown(true, false, true) &&
            DeathObservation.ClassificationKnown(true, true, false) &&
            !DeathObservation.ClassificationKnown(false, true, true),
            "Partial catalog preserves explicit special types but cannot label unmatched or missing identity regular");
        check(DeathObservation.Classify(false, true, ZombieKind.Regular) == TelemetryEvent.DeathRegular &&
            DeathObservation.Classify(false, true, ZombieKind.Large) == TelemetryEvent.DeathLarge &&
            DeathObservation.Classify(false, true, ZombieKind.Boss) == TelemetryEvent.DeathBoss,
            "Confirmed death classification preserves regular, large and catalog boss categories");
        check(DeathObservation.Classify(true, false, ZombieKind.Regular) == TelemetryEvent.DeathBoss &&
            DeathObservation.Classify(false, false, ZombieKind.Boss) == TelemetryEvent.DeathUnknown,
            "Native boss flag survives unavailable catalog; otherwise unavailable identity remains unknown");
        check(DeathObservation.Removal(true, true, true, TelemetryEvent.DeathBoss) == TelemetryEvent.Count &&
            DeathObservation.Removal(false, false, true, TelemetryEvent.DeathBoss) == TelemetryEvent.Count,
            "Failed removal and repeated death notification do not count any death category");
        check(DeathObservation.Removal(true, false, false, TelemetryEvent.DeathBoss) == TelemetryEvent.OtherRemoval,
            "Despawn or another entity removed inside a death notification remains a non-death removal");

        var c = new TelemetryCollector(0, new TelemetryBatch());
        c.ObserveHorde(18, 0);
        foreach (var kind in new[] { TelemetryEvent.DeathRegular, TelemetryEvent.DeathLarge, TelemetryEvent.DeathBoss, TelemetryEvent.DeathUnknown })
            c.Record(DeathObservation.Removal(true, false, true, kind));
        c.Record(TelemetryEvent.Death); // Legacy/unclassified observer must still reconcile.
        c.Record(DeathObservation.Removal(true, false, false, TelemetryEvent.DeathBoss));
        c.CloseBucket(100, PopulationSample.Unavailable);
        check(c.Latest.Deaths == 5 && c.Latest.DeathRegular == 1 && c.Latest.DeathLarge == 1 && c.Latest.DeathBoss == 1 &&
            c.Latest.DeathUnknown == 2 && c.Latest.OtherRemoved == 1,
            "Bucket death total equals exactly one category per confirmed death and excludes despawns");
        c.EndHorde(100, WindowEnd.SceneUnload); c.Complete(100, WindowEnd.SceneUnload);
        check(c.Totals[(int)TelemetryEvent.Death] == 5 && c.Totals.Skip((int)TelemetryEvent.DeathRegular).Sum() == 5 &&
            c.Batch.Horde.Events[(int)TelemetryEvent.Death] == 5 && c.Batch.Horde.Events.Skip((int)TelemetryEvent.DeathRegular).Sum() == 5,
            "Session and completed horde category totals preserve the death reconciliation invariant");

        string root = Path.Combine(Path.GetTempPath(), "expanded-hordes-deaths-" + Guid.NewGuid().ToString("N"));
        try
        {
            c.Batch.LifecycleAvailable = true;
            // Classification availability for fresh spawns must not hide unknown deaths.
            c.Batch.CategoryAvailable = false;
            using (var sink = new TelemetryFileSink(root, "death-fixture", "")) sink.Write(c.Batch);
            var lines = File.ReadAllLines(Path.Combine(root, "performance.csv"));
            var header = lines[0].Split(','); var fields = lines[1].Split(',');
            check(fields.Length == header.Length && fields[0] == "2" &&
                fields[Array.IndexOf(header, "deaths")] == "5" && fields[Array.IndexOf(header, "deaths_regular")] == "1" &&
                fields[Array.IndexOf(header, "deaths_large")] == "1" && fields[Array.IndexOf(header, "deaths_boss")] == "1" &&
                fields[Array.IndexOf(header, "deaths_unknown")] == "2",
                "Schema 2 CSV retains existing total and appends four reconciled death categories");
            string log = File.ReadAllText(Path.Combine(root, "diagnostics.log"));
            check(log.Contains("window_deaths regular=1 large=1 boss=1 unknown=2 attribution=all_causes") &&
                log.Contains("horde_deaths id=18 regular=1 large=1 boss=1 unknown=2 attribution=all_causes") &&
                log.Contains("DeathRegular=1 DeathLarge=1 DeathBoss=1 DeathUnknown=2") && log.Contains("not player-attributed kills"),
                "Readable window, horde and session summaries report categories without claiming player kills");
            c.Next(new TelemetryBatch(), 100); c.ObserveHorde(19, 100); c.Record(TelemetryEvent.DeathBoss);
            c.CloseBucket(200, PopulationSample.Unavailable); c.EndHorde(200, WindowEnd.Shutdown); c.Complete(200, WindowEnd.Shutdown);
            c.Batch.LifecycleAvailable = false;
            using (var sink = new TelemetryFileSink(root, "unavailable-fixture", "")) sink.Write(c.Batch);
            lines = File.ReadAllLines(Path.Combine(root, "performance.csv")); fields = lines[1].Split(',');
            check(fields[Array.IndexOf(header, "deaths_boss")] == "-1" &&
                File.ReadAllLines(Path.Combine(root, "previous-performance.csv"))[1].StartsWith("2,death-fixture,"),
                "Unavailable lifecycle is not zero deaths and a new session rotates the prior categorical report intact");
            check(c.Batch.Horde.Events[(int)TelemetryEvent.DeathBoss] == 1 && c.Batch.Horde.Events[(int)TelemetryEvent.DeathUnknown] == 0 &&
                c.Totals[(int)TelemetryEvent.DeathBoss] == 2,
                "New horde categories reset independently while session totals survive publication");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
