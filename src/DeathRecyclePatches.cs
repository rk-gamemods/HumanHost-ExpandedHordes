using System;
using HarmonyLib;
using UnityEngine;

namespace ExpandedHordes
{
    // NPC_Input.On_Char_Died disables movement AFTER its base class dispatches death
    // listeners. The horde pool can finish resetting the same object inside a listener.
    // See docs/DEATH_RECYCLING.md for the native members and reproduction evidence.
    [HarmonyPatch(typeof(NPC_Input), nameof(NPC_Input.On_Char_Died))]
    internal static class HordeDeathRecycle
    {
        internal struct Scope { internal GameObject Target; internal long Token; }
        internal static void Prefix(NPC_Input __instance, out Scope __state)
        {
            __state = default;
            if (__instance && __instance is Zombie_Input && __instance._npcSpawnSource == 2)
                __state = new Scope { Target = __instance.gameObject,
                    Token = DeathRecycleGuard<GameObject>.Begin(__instance.gameObject) };
        }

        // Always release the scope, including failures in the game or another patch.
        // Preserve the original exception; a failed pool/death operation is not repaired.
        internal static Exception Finalizer(NPC_Input __instance, Scope __state, Exception __exception)
        {
            bool returned = DeathRecycleGuard<GameObject>.End(__state.Target, __state.Token);
            if (__exception != null || !returned) return __exception;
            try
            {
                if (__instance && !__instance.enabled && HordePoolReturn.Ready(__instance))
                {
                    // The GameObject is inactive, so this does not register AI or run OnEnable.
                    __instance.enabled = true;
                }
            }
            catch (Exception ex)
            {
                FeatureRuntime.WarnOnce("death-recycle-failed",
                    "Could not complete movement protection after a pool return. " + ex.GetType().Name);
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(NPC_Horde_Mgr), nameof(NPC_Horde_Mgr.Put_NPC_Back_To_Pool))]
    internal static class HordePoolReturn
    {
        internal static void Postfix(GameObject __0)
        {
            if (!__0 || !DeathRecycleGuard<GameObject>.Contains(__0)) return;
            try
            {
                var npc = __0.GetComponent<Zombie_Input>();
                // Observe a successful, already-enabled pool reset, not merely a call to the method.
                if (npc && npc.enabled && Ready(npc)) DeathRecycleGuard<GameObject>.Returned(__0);
            }
            catch (Exception ex)
            {
                FeatureRuntime.WarnOnce("death-recycle-observation-failed",
                    "Could not verify horde pool return; movement protection skipped it. " + ex.GetType().Name);
            }
        }

        internal static bool Ready(NPC_Input npc)
        {
            return npc && npc._npcSpawnSource == 2 && !npc.gameObject.activeSelf
                && npc.char_Status && npc.char_Status._CurrHP > 0
                && !float.IsInfinity(npc.char_Status._CurrHP)
                && NPC_Horde_Mgr.ins && NPC_Spawner_Mgr.ins
                && !NPC_Horde_Mgr.ins.spawned_Horde_NPCs.ContainsKey(npc.gameObject)
                && !NPC_Spawner_Mgr.ins._waitBackPoolDead_NPCs.ContainsKey(npc.gameObject);
        }
    }
}
