using System;
using ExpandedHordes;

internal static class LivingSpecialChecks
{
    internal static void Run(Action<bool, string> check)
    {
        check(HordeRules.Category(0, 11, 11, 11, 0.5f, 0.25f) == 2 &&
            HordeRules.Category(24, 11, 11, 11, 0.5f, 0.25f) == 2 &&
            HordeRules.Category(25, 11, 11, 11, 0.5f, 0.25f) == 1 &&
            HordeRules.Category(74, 11, 11, 11, 0.5f, 0.25f) == 1 &&
            HordeRules.Category(75, 11, 11, 11, 0.5f, 0.25f) == 0,
            "Fractional boss and large chances keep distinct inclusive-start/exclusive-end intervals");
        foreach (float chance in new[] { 0.01f, 0.25f, 0.5f, 5f, 20f, 40f })
        {
            int boss = 0, large = 0, native = 0;
            for (int roll = 0; roll < 10000; roll++)
            {
                int category = HordeRules.Category(roll, 11, 11, 11, chance, chance);
                if (category == 2) boss++; else if (category == 1) large++; else native++;
            }
            int expected = (int)Math.Round(chance * 100);
            check(boss == expected && large == expected && native == 10000 - 2 * expected,
                $"{chance}% applies to precisely {expected} of 10,000 rolls per category");
        }
        check(HordeRules.Category(0, 10, 11, 11, 0.5f, 0.25f) == 0 &&
            HordeRules.Category(0, 11, 11, 12, 0.5f, 0.25f) == 1 &&
            HordeRules.Category(49, 11, 11, 12, 0.5f, 0.25f) == 1 &&
            HordeRules.Category(50, 11, 11, 12, 0.5f, 0.25f) == 0,
            "Fractional percentages retain independent biome gates");
        bool oldWholeNumberBehavior = true;
        foreach (int region in new[] { 1, 11, 12 })
            foreach (int large in new[] { 0, 1, 20, 40 })
                foreach (int boss in new[] { 0, 1, 5, 40 })
                    for (int roll = 0; roll < 100; roll++)
                    {
                        int activeBoss = region >= 12 ? boss : 0, activeLarge = region >= 11 ? large : 0;
                        int expected = roll < activeBoss ? 2 : roll < activeBoss + activeLarge ? 1 : 0;
                        oldWholeNumberBehavior &= HordeRules.Category(roll * 100, region, 11, 12, large, boss) == expected;
                    }
        check(oldWholeNumberBehavior, "Existing whole-number chances preserve category choices across the original 100 roll slots");

        check(HordeRules.AllowsExtra(2, true, 0, 0, 5, 1) &&
            !HordeRules.AllowsExtra(2, true, 0, 1, 5, 1) &&
            !HordeRules.AllowsExtra(2, true, 0, 2, 5, 1),
            "Default boss limit allows zero living bosses and suppresses at or above one");
        check(HordeRules.AllowsExtra(1, true, 4, 0, 5, 1) &&
            !HordeRules.AllowsExtra(1, true, 5, 0, 5, 1) &&
            !HordeRules.AllowsExtra(1, true, 6, 0, 5, 1),
            "Default large limit allows four living large zombies and suppresses at or above five");
        check(HordeRules.AllowsExtra(1, true, 4, 99, 5, 1) &&
            HordeRules.AllowsExtra(2, true, 99, 0, 5, 1) &&
            !HordeRules.AllowsExtra(1, true, 5, 0, 5, 1) &&
            !HordeRules.AllowsExtra(2, true, 0, 1, 5, 1),
            "Boss and large limits are independent even if the other population exceeds its cap");
        check(HordeRules.AllowsExtra(1, false, -1, -1, 0, 1) &&
            HordeRules.AllowsExtra(2, false, -1, -1, 5, 0) &&
            HordeRules.AllowsExtra(1, true, int.MaxValue, int.MaxValue, 0, 1) &&
            HordeRules.AllowsExtra(2, true, int.MaxValue, int.MaxValue, 5, 0),
            "Zero disables only its own category limit, including when population is unavailable");
        check(!HordeRules.AllowsExtra(1, false, 0, 0, 5, 1) &&
            !HordeRules.AllowsExtra(2, false, 0, 0, 5, 1) &&
            !HordeRules.AllowsExtra(1, true, -1, 0, 5, 1) &&
            !HordeRules.AllowsExtra(2, true, 0, -1, 5, 1),
            "Unavailable or invalid live counts cannot admit additional bounded-category replacements");
        check(!HordeRules.AllowsExtra(0, true, 0, 0, 0, 0) &&
            !HordeRules.AllowsExtra(3, true, 0, 0, 0, 0),
            "Only boss and large replacement categories can pass the cap gate");
        check(!HordeRules.AllowsExtra(2, true, 0, 1, 5, 1) &&
            HordeRules.AllowsExtra(2, true, 0, 0, 5, 1) &&
            !HordeRules.AllowsExtra(2, true, 0, 1, 5, 1) &&
            !HordeRules.AllowsExtra(1, true, 5, 0, 5, 1) &&
            HordeRules.AllowsExtra(1, true, 4, 0, 5, 1),
            "Current counts reopen capacity after death/removal and consume it after replacement without a permanent quota");

        int suppressedBoss = 0, permittedLarge = 0, originalNative = 0;
        for (int roll = 0; roll < 10000; roll++)
        {
            int category = HordeRules.Category(roll, 11, 11, 11, 20f, 5f);
            if (category == 0) originalNative++;
            else if (HordeRules.AllowsExtra(category, true, 0, 1, 5, 1)) permittedLarge++;
            else suppressedBoss++;
        }
        check(suppressedBoss == 500 && permittedLarge == 2000 && originalNative == 7500,
            "A full boss cap suppresses its selected rolls without increasing the independent large chance");
        AdapterChecks(check);
    }

    private static void AdapterChecks(Action<bool, string> check)
    {
        FeatureRuntime.Failures.Clear();
        var owner = new NPC_Horde_Mgr();
        check(HordeSpecialLimits.TryCount(owner, out int large, out int bosses) && large == 0 && bosses == 0,
            "Empty native membership has zero special population without diagnostics");
        var regular = new UnityEngine.GameObject();
        var vanillaLarge = new UnityEngine.GameObject();
        var restoredLarge = new UnityEngine.GameObject();
        var restoredBoss = new UnityEngine.GameObject();
        var nativeFlaggedBoss = new UnityEngine.GameObject { Zombie = new NPC_Input { is_Boss = true } };
        owner._aliveHordeNPCs.Add(regular, new NPC_Horde_Mgr.Horde_NPC_Info { Kind = ZombieKind.Regular });
        owner._aliveHordeNPCs.Add(vanillaLarge, new NPC_Horde_Mgr.Horde_NPC_Info { Kind = ZombieKind.Large });
        owner._aliveHordeNPCs.Add(restoredLarge, new NPC_Horde_Mgr.Horde_NPC_Info { Kind = ZombieKind.Large });
        owner._aliveHordeNPCs.Add(restoredBoss, new NPC_Horde_Mgr.Horde_NPC_Info { Kind = ZombieKind.Boss });
        owner._aliveHordeNPCs.Add(nativeFlaggedBoss, new NPC_Horde_Mgr.Horde_NPC_Info { Known = false });
        check(HordeSpecialLimits.TryCount(owner, out large, out bosses) && large == 2 && bosses == 2,
            "Live membership counts native and restored special identities without requiring this mod's spawn history");
        check(!HordeRules.AllowsExtra(2, true, large, bosses, 5, 1) && HordeRules.AllowsExtra(1, true, large, bosses, 5, 1),
            "Vanilla and restored bosses consume the boss cap while large capacity remains independent");
        owner._aliveHordeNPCs.Remove(restoredBoss); owner._aliveHordeNPCs.Remove(nativeFlaggedBoss);
        check(HordeSpecialLimits.TryCount(owner, out large, out bosses) && bosses == 0 &&
            HordeRules.AllowsExtra(2, true, large, bosses, 5, 1),
            "Confirmed native removals reopen boss capacity on the very next selection attempt");
        owner._aliveHordeNPCs.Add(restoredBoss, new NPC_Horde_Mgr.Horde_NPC_Info { Kind = ZombieKind.Boss });
        check(HordeSpecialLimits.TryCount(owner, out large, out bosses) && bosses == 1 &&
            !HordeRules.AllowsExtra(2, true, large, bosses, 5, 1),
            "A newly registered replacement immediately fills the reopened boss slot");
        owner._aliveHordeNPCs[regular].Known = false;
        check(!HordeSpecialLimits.TryCount(owner, out large, out bosses) &&
            !HordeRules.AllowsExtra(2, false, large, bosses, 5, 1),
            "Unknown member classification cannot silently undercount a bounded category");
        owner._aliveHordeNPCs[regular].Known = true; regular.Destroyed = true;
        check(!HordeSpecialLimits.TryCount(owner, out large, out bosses),
            "Destroyed membership entries make the snapshot unavailable until native cleanup");
        regular.Destroyed = false;
        FeatureRuntime.Failures.Add(Feature.Catalog);
        check(!HordeSpecialLimits.TryCount(owner, out large, out bosses), "Unavailable catalog does not masquerade as an empty population");
        FeatureRuntime.Failures.Clear();
        owner._aliveHordeNPCs = null;
        check(!HordeSpecialLimits.TryCount(owner, out large, out bosses) &&
            !HordeSpecialLimits.TryCount(null, out large, out bosses),
            "Missing native manager or membership dictionary preserves the original spawn through an unavailable result");
        owner._aliveHordeNPCs = new System.Collections.Generic.Dictionary<UnityEngine.GameObject, NPC_Horde_Mgr.Horde_NPC_Info>();
        for (int i = 0; i < HordeSpecialLimits.MaximumInspectedMembers; i++)
            owner._aliveHordeNPCs.Add(new UnityEngine.GameObject(), new NPC_Horde_Mgr.Horde_NPC_Info());
        check(HordeSpecialLimits.TryCount(owner, out large, out bosses) && large == 0 && bosses == 0,
            "The maximum supported membership size is counted normally");
        owner._aliveHordeNPCs.Add(new UnityEngine.GameObject(), new NPC_Horde_Mgr.Horde_NPC_Info());
        check(!HordeSpecialLimits.TryCount(owner, out large, out bosses),
            "Oversized membership stops the bounded scan and cannot admit capped extras from a partial count");

        HordeSpecialLimits.Exit();
        check(!HordeSpecialLimits.Enter(null), "An absent horde manager cannot enter replacement selection");
        owner._inAsyncSpawnNPC = true;
        check(!HordeSpecialLimits.Enter(owner), "An in-flight native asynchronous spawn suppresses another mod replacement");
        owner._inAsyncSpawnNPC = false;
        check(HordeSpecialLimits.Enter(owner) && !HordeSpecialLimits.Enter(owner),
            "Synchronous reentry cannot reserve the same currently empty special slot twice");
        HordeSpecialLimits.Exit();
        check(HordeSpecialLimits.Enter(owner), "Finalization releases the selection guard for the next ordinary spawn");
        HordeSpecialLimits.Exit(); HordeSpecialLimits.Exit();
        check(HordeSpecialLimits.Enter(owner), "Repeated guard cleanup is harmless and does not leave a negative reservation");
        HordeSpecialLimits.Exit();
    }
}
