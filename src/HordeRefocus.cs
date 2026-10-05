using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExpandedHordes
{
    internal static class HordeRefocus
    {
        private static AccessTools.FieldRef<NPC_Horde_Mgr, Dictionary<GameObject, NPC_Horde_Mgr.Horde_NPC_Info>> alive;
        private static AccessTools.FieldRef<AI_Agen_Mgr, Coroutine> sorting;
        private static readonly Dictionary<GameObject, int> idle = new Dictionary<GameObject, int>();
        private static readonly List<KeyValuePair<GameObject, NPC_Horde_Mgr.Horde_NPC_Info>> members =
            new List<KeyValuePair<GameObject, NPC_Horde_Mgr.Horde_NPC_Info>>();
        private static readonly List<GameObject> removed = new List<GameObject>();
        private static float nextTick;
        private static int cursor;

        internal static void Initialize()
        {
            alive = AccessTools.FieldRefAccess<NPC_Horde_Mgr, Dictionary<GameObject, NPC_Horde_Mgr.Horde_NPC_Info>>("_aliveHordeNPCs");
            sorting = AccessTools.FieldRefAccess<AI_Agen_Mgr, Coroutine>("_InWaitSorting");
            Clear();
        }

        internal static void Clear()
        {
            idle.Clear();
            members.Clear();
            removed.Clear();
            cursor = 0;
            nextTick = Time.time + RefocusRules.Interval;
        }

        internal static void Forget(GameObject member) { if (!ReferenceEquals(member, null)) idle.Remove(member); }

        internal static void Update()
        {
            if (!FeatureRuntime.Enabled(Feature.Refocus)) return;
            try
            {
                if (!RefocusRules.TickDue(Time.time, ref nextTick)) return;
                Tick();
            }
            catch (Exception ex) { Clear(); FeatureRuntime.Fail(Feature.Refocus, ex); }
        }

        private static void Tick()
        {
            NPC_Horde_Mgr owner = NPC_Horde_Mgr.ins;
            AI_Agen_Mgr ai = AI_Agen_Mgr.ins;
            Creature_Mgr creatures = Creature_Mgr.ins;
            Player_Input player = Player_Input.ins;
            if (!owner || !ai || !creatures) { Clear(); return; }
            var living = alive(owner);
            // Reconcile even skipped ticks; removal hooks also cover leave/rejoin between ticks.
            removed.Clear();
            foreach (var entry in idle)
                if (!living.ContainsKey(entry.Key)) removed.Add(entry.Key);
            foreach (var member in removed) idle.Remove(member);
            removed.Clear();
            if (!RefocusRules.TickAllowed(!creatures._IsDayTime, living.Count,
                player && player.char_Status && player.char_Status._CurrHP > 0f,
                G_Save.isQuit, sorting(ai) != null)) return;

            // A broadcast can cause another mod to remove membership. Reuse a snapshot
            // and recheck identity before touching each member, without LINQ allocations.
            members.Clear();
            foreach (var member in living) members.Add(member);
            int count = members.Count, calls = 0, index = cursor % count;
            for (int i = 0; i < count; i++)
            {
                var member = members[index];
                if (member.Key && living.TryGetValue(member.Key, out var current) && ReferenceEquals(current, member.Value))
                {
                    idle.TryGetValue(member.Key, out int ticks);
                    bool registered = ai._allZombies2Agent.TryGetValue(member.Key, out var agent) && agent;
                    NPC_Input npc = registered ? agent._NPC_Input : null;
                    ticks = RefocusRules.IdleTicks(ticks,
                        registered && agent.enabled && npc && npc.enabled && member.Key.activeInHierarchy,
                        npc && npc.char_Status ? npc.char_Status._CurrHP : 0f,
                        npc && npc._isFallGround, registered && (agent._focusTrans || agent._focusCollider));
                    bool send = RefocusRules.TryRefocus(ref ticks, ref calls);
                    if (ticks == 0) idle.Remove(member.Key);
                    else idle[member.Key] = ticks;
                    if (send)
                    {
                        cursor = RefocusRules.NextIndex(index, count);
                        // Native respawn refocus uses zero/false (AI:1188), retaining
                        // the origin initialized by the spawn-time broadcast (Terrain:837).
                        owner.Broadcast_Npc_Focus_Player_Event(member.Key, Vector3.zero, false);
                    }
                }
                else Forget(member.Key);
                index = RefocusRules.NextIndex(index, count);
            }
            members.Clear();
            if (calls > 0 && ModSettings.DebugMode.Value)
                FeatureRuntime.DebugLog($"Horde refocus: sent {calls} native focus broadcasts.");
        }
    }

    [HarmonyPatch(typeof(NPC_Horde_Mgr), nameof(NPC_Horde_Mgr.Remove_AliveHordeNPC))]
    internal static class HordeRefocusMembership
    {
        // Install validates accessors inside this feature's failure boundary.
        static HordeRefocusMembership() => HordeRefocus.Initialize();
        private static void Postfix(GameObject npcObj) => HordeRefocus.Forget(npcObj);
    }
}
