using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExpandedHordes
{
    // Read the game's current membership, not diagnostic counters or our own
    // spawn history: native choices, restored enemies and removals all count.
    internal static class HordeSpecialLimits
    {
        internal const int MaximumInspectedMembers = 10000;
        private static AccessTools.FieldRef<NPC_Horde_Mgr, Dictionary<GameObject, NPC_Horde_Mgr.Horde_NPC_Info>> alive;
        private static AccessTools.FieldRef<NPC_Horde_Mgr, bool> loading;
        private static int selectionDepth;

        // Native spawning waits for _inAsyncSpawnNPC before the next request.
        // Also reject reentrant calls during synchronous native construction.
        // This suppresses overlapping mod replacements, not native spawns.
        internal static bool Enter(NPC_Horde_Mgr owner)
        {
            if (!owner || selectionDepth != 0) return false;
            loading ??= AccessTools.FieldRefAccess<NPC_Horde_Mgr, bool>("_inAsyncSpawnNPC");
            if (loading(owner)) return false;
            selectionDepth++;
            return true;
        }

        internal static void Exit() { if (selectionDepth > 0) selectionDepth--; }

        internal static bool TryCount(NPC_Horde_Mgr owner, out int large, out int bosses)
        {
            large = bosses = 0;
            try
            {
                if (!owner || !FeatureRuntime.Enabled(Feature.Catalog)) return Unavailable();
                alive ??= AccessTools.FieldRefAccess<NPC_Horde_Mgr, Dictionary<GameObject, NPC_Horde_Mgr.Horde_NPC_Info>>("_aliveHordeNPCs");
                var members = alive(owner);
                if (members == null || members.Count > MaximumInspectedMembers) return Unavailable();
                foreach (var member in members)
                {
                    if (!member.Key) return Unavailable();
                    var npc = member.Key.GetComponent<NPC_Input>();
                    if (npc && npc.is_Boss) { bosses++; continue; }
                    if (!CreatureCatalog.TryClassifyDeath(member.Value, out var kind)) return Unavailable();
                    if (kind == ZombieKind.Boss) bosses++;
                    else if (kind == ZombieKind.Large) large++;
                }
                return true;
            }
            catch (Exception) { return Unavailable(); }
        }

        private static bool Unavailable()
        {
            FeatureRuntime.WarnOnce("special-limits-membership",
                "Extra special selection suppressed: living horde categories could not be counted reliably. Native choices are unchanged.");
            return false;
        }
    }
}
