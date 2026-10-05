using System;
using ExpandedHordes;

internal static class RefocusChecks
{
    internal static void Run(Action<bool, string> check)
    {
        float next = RefocusRules.Interval;
        check(!RefocusRules.TickDue(0, ref next) && !RefocusRules.TickDue(4.99f, ref next), "Refocus waits five scaled seconds");
        check(RefocusRules.TickDue(5, ref next) && next == 10, "Refocus schedules the next scaled tick");
        check(!RefocusRules.TickDue(5, ref next), "Paused scaled time cannot repeat a refocus tick");
        check(RefocusRules.TickDue(100, ref next) && next == 105 && !RefocusRules.TickDue(100, ref next), "Delayed update runs one tick without a catch-up burst");
        check(RefocusRules.TickAllowed(true, 1, true, false, false, false), "Nighttime living horde and player allow refocus");
        check(!RefocusRules.TickAllowed(false, 1, true, false, false, false), "Daytime skips refocus");
        check(!RefocusRules.TickAllowed(true, 0, true, false, false, false), "Empty horde skips refocus");
        check(!RefocusRules.TickAllowed(true, 1, false, false, false, false), "Missing or dead player skips refocus");
        check(!RefocusRules.TickAllowed(true, 1, true, true, false, false), "Quit skips refocus");
        check(!RefocusRules.TickAllowed(true, 1, true, false, true, false), "Native AI sorting skips refocus");
        check(!RefocusRules.TickAllowed(true, 1, true, false, false, true), "Admin Panel ZombiesIgnoreYou skips the whole tick");

        check(RefocusRules.GraceTicks == 9 && RefocusRules.GraceTicks * RefocusRules.Interval == 45, "Refocus grants nine idle intervals, or 45 scaled seconds");
        int calls = 0;
        var sample = new RefocusSample();
        RefocusRules.Observe(ref sample, true, 1, false, false, 10, 20);
        check(sample.HasPosition && sample.IdleTicks == 0, "First observation establishes position without assuming stationary time");
        for (int tick = 1; tick < 9; tick++)
        {
            RefocusRules.Observe(ref sample, true, 1, false, false, 10, 20);
            check(sample.IdleTicks == tick && !RefocusRules.TryRefocus(ref sample.IdleTicks, ref calls) && calls == 0,
                "Stationary unfocused members retain grace through idle tick " + tick);
        }
        RefocusRules.Observe(ref sample, true, 1, false, false, 10, 20);
        check(RefocusRules.TryRefocus(ref sample.IdleTicks, ref calls) && sample.IdleTicks == 0 && calls == 1,
            "Ninth consecutive idle tick broadcasts and resets grace");
        RefocusRules.Observe(ref sample, true, 1, false, false, 10, 20);
        check(sample.IdleTicks == 1 && !RefocusRules.TryRefocus(ref sample.IdleTicks, ref calls), "Vetoed broadcasts restart the full grace with the position baseline retained");

        sample = new RefocusSample { HasPosition = true, IdleTicks = 8 };
        RefocusRules.Observe(ref sample, true, 1, false, false, 0.999f, 0);
        check(sample.IdleTicks == 9, "Horizontal displacement below one metre counts as stationary");
        RefocusRules.Observe(ref sample, true, 1, false, false, sample.X + 1, 0);
        check(sample.IdleTicks == 0, "Exactly one metre of horizontal movement resets grace");
        RefocusRules.Observe(ref sample, true, 1, false, false, sample.X, sample.Z);
        check(sample.IdleTicks == 1, "Standing still after movement starts a new grace period");
        sample = new RefocusSample { HasPosition = true, IdleTicks = 8 };
        RefocusRules.Observe(ref sample, true, 1, false, false, 0.7f, 0.7f);
        check(sample.IdleTicks == 9, "Diagonal displacement uses horizontal distance");
        RefocusRules.Observe(ref sample, true, 1, false, false, 1.5f, 1.5f);
        check(sample.IdleTicks == 0, "Diagonal travel exceeding one metre resets grace");
        RefocusRules.Observe(ref sample, true, 1, false, false, float.NaN, 0);
        check(sample.IdleTicks == 0, "Invalid position cannot count as standing still");
        foreach (var condition in new[] { (false, 1f, false, false), (true, 0f, false, false),
            (true, -1f, false, false), (true, float.NaN, false, false), (true, 1f, true, false), (true, 1f, false, true) })
        {
            sample = new RefocusSample { HasPosition = true, IdleTicks = 8 };
            RefocusRules.Observe(ref sample, condition.Item1, condition.Item2, condition.Item3, condition.Item4, 0, 0);
            check(sample.IdleTicks == 0, "Inactive, dead, invalid-health, ragdolled or focused members reset grace");
        }

        // More than two batches, with every native broadcast vetoed: ensure the
        // production cursor policy still reaches deferred members without starvation.
        const int population = 65;
        var samples = new RefocusSample[population];
        var attempts = new int[population];
        int cursor = 0;
        for (int tick = 0; tick < 14; tick++)
        {
            calls = 0;
            int index = cursor;
            for (int i = 0; i < population; i++)
            {
                RefocusRules.Observe(ref samples[index], true, 1, false, false, 0, 0);
                if (RefocusRules.TryRefocus(ref samples[index].IdleTicks, ref calls))
                {
                    attempts[index]++;
                    cursor = RefocusRules.NextIndex(index, population);
                }
                index = RefocusRules.NextIndex(index, population);
            }
            check(calls == (tick < 9 ? 0 : tick < 13 ? 16 : 1), "Refocus obeys the grace and sixteen-call cap");
            if (tick == 9) check(samples[16].IdleTicks == 9 && attempts[16] == 0, "Cap-deferred member retains its completed grace");
            if (tick == 10) check(attempts[16] == 1 && attempts[31] == 1, "Next tick continues the deferred batch");
        }
        bool allServed = true;
        foreach (int attempted in attempts) allServed &= attempted > 0;
        check(allServed, "Every persistently idle member gets a turn despite repeated vetoes");
        sample = new RefocusSample { HasPosition = true, IdleTicks = 9 };
        RefocusRules.Observe(ref sample, true, 1, false, false, 0, 0);
        check(sample.IdleTicks == 9, "Deferred idle counters saturate at the grace threshold");
    }
}
