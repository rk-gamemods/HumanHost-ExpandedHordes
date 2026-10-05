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
        check(RefocusRules.TickAllowed(true, 1, true, false, false), "Nighttime living horde and player allow refocus");
        check(!RefocusRules.TickAllowed(false, 1, true, false, false), "Daytime skips refocus");
        check(!RefocusRules.TickAllowed(true, 0, true, false, false), "Empty horde skips refocus");
        check(!RefocusRules.TickAllowed(true, 1, false, false, false), "Missing or dead player skips refocus");
        check(!RefocusRules.TickAllowed(true, 1, true, true, false), "Quit skips refocus");
        check(!RefocusRules.TickAllowed(true, 1, true, false, true), "Native AI sorting skips refocus");

        int calls = 0, ticks = RefocusRules.IdleTicks(0, true, 1, false, false);
        check(ticks == 1 && !RefocusRules.TryRefocus(ref ticks, ref calls) && calls == 0, "First idle tick grants grace");
        ticks = RefocusRules.IdleTicks(ticks, true, 1, false, false);
        check(RefocusRules.TryRefocus(ref ticks, ref calls) && ticks == 0 && calls == 1, "Second consecutive idle tick broadcasts and resets grace");
        ticks = RefocusRules.IdleTicks(ticks, true, 1, false, false);
        check(!RefocusRules.TryRefocus(ref ticks, ref calls), "Vetoed broadcasts also wait another grace period");
        check(RefocusRules.IdleTicks(1, false, 1, false, false) == 0, "Inactive or disabled members reset grace");
        check(RefocusRules.IdleTicks(1, true, 0, false, false) == 0 && RefocusRules.IdleTicks(1, true, -1, false, false) == 0, "Dead members reset grace");
        check(RefocusRules.IdleTicks(1, true, float.NaN, false, false) == 0, "Invalid HP does not qualify as alive");
        check(RefocusRules.IdleTicks(1, true, 1, true, false) == 0, "Ragdolls reset grace");
        check(RefocusRules.IdleTicks(1, true, 1, false, true) == 0, "Existing AI focus resets grace");

        // More than two batches, with every native broadcast vetoed: ensure the
        // production cursor policy still reaches deferred members without starvation.
        const int population = 65;
        var counters = new int[population];
        var attempts = new int[population];
        int cursor = 0;
        for (int tick = 0; tick < 6; tick++)
        {
            calls = 0;
            int index = cursor;
            for (int i = 0; i < population; i++)
            {
                counters[index] = RefocusRules.IdleTicks(counters[index], true, 1, false, false);
                if (RefocusRules.TryRefocus(ref counters[index], ref calls))
                {
                    attempts[index]++;
                    cursor = RefocusRules.NextIndex(index, population);
                }
                index = RefocusRules.NextIndex(index, population);
            }
            check(calls == (tick == 0 ? 0 : 16), "Refocus cap is exactly sixteen calls per eligible tick");
            if (tick == 1) check(counters[16] == 2 && attempts[16] == 0, "Cap-deferred member retains its completed grace");
            if (tick == 2) check(attempts[16] == 1 && attempts[31] == 1, "Next tick continues the deferred batch");
        }
        bool allServed = true;
        foreach (int attempted in attempts) allServed &= attempted > 0;
        check(allServed, "Every persistently idle member gets a turn despite repeated vetoes");
        check(RefocusRules.IdleTicks(2, true, 1, false, false) == 2, "Deferred idle counters saturate at the grace threshold");
    }
}
