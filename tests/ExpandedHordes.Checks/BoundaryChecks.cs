using System;
using System.Linq;
using ExpandedHordes;

internal static class BoundaryChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var state = PopulationSample.Unavailable;
        var c = new TelemetryCollector(0, new TelemetryBatch());
        c.ObserveHorde(1, 0); c.Record(TelemetryEvent.Fresh);
        c.CloseBucket(0, state); c.EndHorde(0, WindowEnd.Shutdown); c.Complete(0, WindowEnd.Shutdown);
        check(c.Batch.Count == 1 && c.Batch.Buckets[0].Duration == 0 && c.Batch.Buckets[0].Fresh == 1 && c.Batch.Horde.Events[0] == 1,
            "Same-timestamp shutdown preserves events without inventing elapsed time");
        c.Next(new TelemetryBatch(), 0); c.CloseBucket(0, state);
        check(c.Batch.Count == 0, "Repeated zero-duration close does not duplicate a bucket");

        c = new TelemetryCollector(0, new TelemetryBatch());
        c.ObserveHorde(7, 0); c.Frame(100); c.CloseBucket(100, state);
        c.ObserveHorde(8, 100); c.Frame(200); c.CloseBucket(200, state); c.Complete(200, WindowEnd.HordeChange);
        c.Next(c.Batch, 200); c.Complete(200, WindowEnd.Cadence);
        check(c.Batch.Horde.Id == 7 && c.Batch.Horde.LostBuckets == 1 && c.Horde.LostBuckets == 1,
            "Dropped mixed-horde batch charges only each observation's buckets and updates pending summary");
        c.Next(new TelemetryBatch(), 200); c.EndHorde(200, WindowEnd.SceneUnload); c.Complete(200, WindowEnd.SceneUnload);
        check(c.Batch.Horde.Id == 8 && c.Batch.Horde.LostBuckets == 1, "Final snapshot retains correct prior loss");

        c = new TelemetryCollector(0, new TelemetryBatch());
        c.ObserveHorde(9, 0); c.Record(TelemetryEvent.Fresh); c.Frame(25); c.CloseBucket(25, state);
        c.Complete(25, WindowEnd.Pause); var paused = c.Batch; c.Next(new TelemetryBatch(), 25);
        c.Complete(25, WindowEnd.Resume); var resumed = c.Batch; c.Next(new TelemetryBatch(), 25);
        c.Record(TelemetryEvent.Restored); c.Frame(1025); c.CloseBucket(1025, state);
        c.EndHorde(1025, WindowEnd.SceneUnload); c.Complete(1025, WindowEnd.SceneUnload); var unloaded = c.Batch;
        c.Next(new TelemetryBatch(), 1025); c.ObserveHorde(9, 1025);
        c.Record(TelemetryEvent.OtherRemoval); c.CloseBucket(1025, state); c.EndHorde(1025, WindowEnd.Shutdown); c.Complete(1025, WindowEnd.Shutdown);
        check(paused.Reason == WindowEnd.Pause && resumed.Reason == WindowEnd.Resume && resumed.Count == 0 && resumed.EndMs == resumed.StartMs,
            "Pause/resume at one timestamp preserves explicit boundaries without fake frames");
        check(unloaded.Horde.Frames.Count == 2 && unloaded.Horde.Frames.Total == 1025 && unloaded.Buckets[0].FrameMaxMs == 1000,
            "Resume stall retains full spanning frame and scene-unload horde distribution");
        check(c.Batch.Horde.Events[0] == 0 && c.Batch.Horde.Events[3] == 1 && c.Batch.Horde.Frames.Count == 0 && c.Totals.Sum() == 3,
            "Reloading the same horde ID creates a separate observation while preserving session totals");

        c = new TelemetryCollector(0, new TelemetryBatch());
        for (int i = 1; i <= 18; i++) c.Mark(i, 0, "note");
        c.SetMetadata("first"); c.SetMetadata("second");
        c.CloseBucket(0, state); c.Complete(0, WindowEnd.Pause); c.Next(c.Batch, 0);
        c.Complete(0, WindowEnd.Resume);
        check(c.Batch.LostMarkerNotes == 18 && c.Batch.LostMetadataSnapshots == 2 && c.DroppedBuckets == 1,
            "Dropped batch discloses marker overflow, discarded notes and overwritten/dropped metadata");
        c.SetMetadata("third"); c.Mark(19, 0, "retained"); c.CloseBucket(0, state); c.Complete(0, WindowEnd.Shutdown);
        c.Next(new TelemetryBatch(), 0); c.Complete(0, WindowEnd.Shutdown);
        check(c.Batch.LostMarkerNotes == 18 && c.Batch.LostMetadataSnapshots == 2,
            "Successful later publication preserves cumulative loss without counting delivered notes or metadata");
    }
}
