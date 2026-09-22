using System;
using System.Linq;
using ExpandedHordes;

internal static class MetadataChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var report = new TelemetryMetadata("fixture-user", "FIXTURE-PC");
        foreach (string value in new[] { @"C:\Users\fixture-user\plugin.dll", "/home/player/plugin", "file:///tmp/plugin", "name@example.test" })
            check(report.Safe(value) == "[path-or-identifier omitted]", "Metadata excludes paths and email-like identifiers");
        check(report.Safe("made for FIXTURE-USER") == "[personal metadata omitted]" &&
            report.Safe("running on fixture-pc") == "[personal metadata omitted]", "Metadata masks local user and machine names without case dependence");
        check(report.Safe("account 76561198012345678") == "[account-like identifier omitted]", "Metadata does not emit account-like numeric identifiers from names/errors");
        check(report.Safe("rkgamemods.humanhost.expandedhordes") == "rkgamemods.humanhost.expandedhordes" &&
            report.Safe("AMD Ryzen 7 9800X3D") == "AMD Ryzen 7 9800X3D", "Useful plugin identity and hardware model survive sanitation");
        string controls = report.Safe("a\r\nb\t\u001b\u202ec");
        check(!controls.Any(char.IsControl) && !controls.Contains('\u202e'), "Control and bidi characters cannot forge metadata lines or direction");
        check(report.Safe(null) == "unavailable", "Null metadata remains explicitly unavailable");
        string huge = new string('x', 1024 * 1024);
        report.Safe(huge); // Warm the long-input path before measuring copied storage.
        long started = GC.GetAllocatedBytesForCurrentThread();
        string clipped = report.Safe(huge);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - started;
        check(clipped.Length == TelemetryMetadata.MaxValueCharacters && clipped.EndsWith("...") && bytes < 4096,
            "Oversized values have bounded copied storage before appending");
        report.AppendLine("value=" + clipped);
        check(report.ToString().Contains("metadata_values_truncated=true"), "Value clipping is disclosed");
        check(report.Ordering(new[] { "owner-a", "owner-b" }) == "owner-a|owner-b", "Ordering retains ordinary owner identities");
        string[] manyOwners = Enumerable.Repeat(new string('q', 100), 10000).ToArray();
        check(report.Ordering(manyOwners).Length <= TelemetryMetadata.MaxValueCharacters, "Ordering metadata cannot allocate an unbounded joined string");
        for (int i = 0; i < 10000; i++) report.AppendLine("plugin=" + clipped);
        string full = report.ToString();
        check(full.Length <= TelemetryMetadata.MaxCharacters && report.Full && full.EndsWith("metadata_values_truncated=true\n") &&
            full.Contains("metadata_truncated=true\n"), "Full metadata is bounded including complete truncation flags");
        check(full == report.ToString() && full.Split('\n').All(line => line.Length == 0 || line.StartsWith("plugin=") ||
            line.StartsWith("value=") || line.StartsWith("metadata_")), "Repeated metadata snapshots are stable and retain complete lines");
        report.AppendLine("ignored"); check(report.ToString() == full, "A full report cannot grow after further appends");
        var empty = new TelemetryMetadata(null, null);
        empty.AppendLine(huge);
        check(empty.ToString() == "metadata_truncated=true\n", "An oversized first line cannot force builder growth or produce partial records");
    }
}
