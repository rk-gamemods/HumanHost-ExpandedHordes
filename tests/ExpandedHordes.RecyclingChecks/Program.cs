using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExpandedHordes;
using UnityEngine;

internal static class Program
{
    private static int assertions;
    private static void Check(bool pass, string message)
    { assertions++; if (!pass) throw new Exception(message); }

    private static void Pool(Zombie_Input npc, bool guard = true)
    {
        npc.char_Status._CurrHP = 100;
        npc.gameObject.activeSelf = false;
        npc.enabled = true;
        if (guard) HordePoolReturn.Postfix(npc.gameObject);
    }

    // Behavioral fixture from the observed nested death -> pool reset -> late disable order.
    // No proprietary game source is copied into this repository.
    private static int Burst(int count, bool guard, bool upstreamFixed = false)
    {
        var zombies = new List<Zombie_Input>();
        for (int i = 0; i < count; i++)
        {
            var npc = new Zombie_Input(); zombies.Add(npc);
            HordeDeathRecycle.Scope scope = default;
            if (guard) HordeDeathRecycle.Prefix(npc, out scope);
            npc.char_Status._CurrHP = 0;
            if (upstreamFixed) npc.enabled = false;
            if (i >= 4) Pool(npc, guard);
            if (!upstreamFixed) npc.enabled = false;
            if (guard) HordeDeathRecycle.Finalizer(npc, scope, null);
        }
        return zombies.Count(n => !n.gameObject.activeSelf && !n.enabled);
    }

    private static void Exclusion(string label, Action<Zombie_Input> change, bool beforePool = false)
    {
        var npc = new Zombie_Input();
        HordeDeathRecycle.Prefix(npc, out var scope);
        Pool(npc, false);
        if (beforePool) change(npc);
        HordePoolReturn.Postfix(npc.gameObject);
        npc.enabled = false;
        if (!beforePool) change(npc);
        int writes = npc.EnableWrites;
        HordeDeathRecycle.Finalizer(npc, scope, null);
        Check(npc.EnableWrites == writes, label);
        Check(DeathRecycleGuard<GameObject>.Depth == 0, label + " releases references");
        NPC_Horde_Mgr.ins = new NPC_Horde_Mgr(); NPC_Spawner_Mgr.ins = new NPC_Spawner_Mgr();
    }

    private static void Main(string[] args)
    {
        bool fault = args.Contains("--without-guard");
        int reproduced = Burst(12, !fault);
        Console.WriteLine($"Recorded 12-death ordering: broken recycled objects={reproduced}");
        Check(reproduced == 0, "Reused zombies must retain movement after simultaneous deaths");
        Check(Burst(12, false) == 8, "Negative control reaches the recorded eight failures");
        foreach (int count in new[] { 1, 4, 5, 12, 32 })
            Check(Burst(count, true) == 0, "Guard protects different burst sizes");
        Check(Burst(12, true, true) == 0, "Future upstream ordering fix remains valid");

        var npc = new Zombie_Input(); HordeDeathRecycle.Prefix(npc, out var state);
        Pool(npc); int writes = npc.EnableWrites;
        HordeDeathRecycle.Finalizer(npc, state, null);
        Check(npc.EnableWrites == writes, "Already-correct upstream state receives no write");
        npc.enabled = false; HordeDeathRecycle.Finalizer(npc, state, null);
        Check(!npc.enabled, "Repeated finalizer cannot replay a consumed repair");

        npc = new Zombie_Input(); HordeDeathRecycle.Prefix(npc, out state);
        Pool(npc); npc.enabled = false;
        var error = new InvalidOperationException("native death failure");
        Check(ReferenceEquals(HordeDeathRecycle.Finalizer(npc, state, error), error) && !npc.enabled,
            "Original exception is preserved and incomplete death is not repaired");
        Check(DeathRecycleGuard<GameObject>.Depth == 0, "Exception releases the scope");

        npc = new Zombie_Input(); Pool(npc); npc.enabled = false;
        HordeDeathRecycle.Finalizer(npc, default, null);
        Check(!npc.enabled, "Skipped prefix/unrelated pool operation cannot authorize repair");

        Exclusion("Active respawn is untouched", n => n.gameObject.activeSelf = true);
        Exclusion("Actual corpse is not revived", n => n.char_Status._CurrHP = 0);
        Exclusion("Invalid health is rejected", n => n.char_Status._CurrHP = float.NaN);
        Exclusion("Infinite health is rejected", n => n.char_Status._CurrHP = float.PositiveInfinity);
        Exclusion("Destroyed object is untouched", n => n.Destroyed = true);
        Exclusion("Changed spawn ownership is rejected", n => n._npcSpawnSource = 1);
        Exclusion("Missing horde manager is rejected", n => NPC_Horde_Mgr.ins = null);
        Exclusion("Missing spawner is rejected", n => NPC_Spawner_Mgr.ins = null);
        Exclusion("Registered horde object is rejected", n => NPC_Horde_Mgr.ins.spawned_Horde_NPCs.Add(n.gameObject, n));
        Exclusion("Pending death is rejected", n => NPC_Spawner_Mgr.ins._waitBackPoolDead_NPCs.Add(n.gameObject, n));
        Exclusion("Pool that did not enable movement is rejected", n => n.enabled = false, true);
        Exclusion("Exception in observation leaves native state alone", n => n.gameObject.ThrowRead = true, true);

        foreach (var excluded in new NPC_Input[] { new NPC_Input(), new Zombie_Input { _npcSpawnSource = 1 } })
        {
            HordeDeathRecycle.Prefix(excluded, out state);
            Check(state.Token == 0, "Non-zombies and ambient zombies do not acquire a scope");
            HordeDeathRecycle.Finalizer(excluded, state, null);
        }

        var a = new GameObject(); var b = new GameObject();
        long outer = DeathRecycleGuard<GameObject>.Begin(a), inner = DeathRecycleGuard<GameObject>.Begin(b);
        DeathRecycleGuard<GameObject>.Returned(a);
        Check(!DeathRecycleGuard<GameObject>.End(b, inner), "Nested different death cannot steal pool proof");
        Check(DeathRecycleGuard<GameObject>.End(a, outer), "Outer death retains its pool proof");
        outer = DeathRecycleGuard<GameObject>.Begin(a); inner = DeathRecycleGuard<GameObject>.Begin(a);
        DeathRecycleGuard<GameObject>.Returned(a);
        Check(DeathRecycleGuard<GameObject>.End(a, inner) && DeathRecycleGuard<GameObject>.End(a, outer), "Same-object reentrancy preserves both scopes");
        outer = DeathRecycleGuard<GameObject>.Begin(a); inner = DeathRecycleGuard<GameObject>.Begin(b);
        DeathRecycleGuard<GameObject>.Returned(a);
        Check(!DeathRecycleGuard<GameObject>.End(a, outer), "Out-of-order finalizer fails closed");
        Check(!DeathRecycleGuard<GameObject>.End(b, inner) && DeathRecycleGuard<GameObject>.Depth == 0, "Out-of-order cleanup does not strand references");
        outer = DeathRecycleGuard<GameObject>.Begin(a); DeathRecycleGuard<GameObject>.Returned(a);
        var worker = new Thread(() => { Check(DeathRecycleGuard<GameObject>.Depth == 0, "Worker has separate scopes"); DeathRecycleGuard<GameObject>.Returned(b); });
        worker.Start(); worker.Join();
        Check(DeathRecycleGuard<GameObject>.End(a, outer), "Worker cannot consume main-thread proof");

        // Measure the actual production callbacks with boundary stubs; excludes Unity and Harmony.
        // Warm once, then measure a bounded burst. No assertion on noisy wall-clock duration.
        npc = new Zombie_Input();
        void Cycle() { HordeDeathRecycle.Prefix(npc, out var s); Pool(npc); npc.enabled = false; HordeDeathRecycle.Finalizer(npc, s, null); }
        for (int i = 0; i < 1000; i++) Cycle();
        var timer = new Stopwatch(); long bytes = GC.GetAllocatedBytesForCurrentThread(); timer.Start();
        for (int i = 0; i < 10000; i++) Cycle();
        timer.Stop(); bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Check(bytes == 0, "Steady-state production callbacks allocate no managed objects with boundary stubs");
        Check(DeathRecycleGuard<GameObject>.Depth == 0, "Repeated deaths leave no pending references");
        Console.WriteLine($"Production-hook microbenchmark: {timer.Elapsed.TotalMilliseconds:F3} ms / 10,000 death-and-pool cycles; {bytes} managed bytes. Host .NET only, not game FPS.");
        Console.WriteLine($"PASS: {assertions} recycling assertions. Unity callbacks and Harmony detours need runtime verification.");
    }
}
