using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ExpandedHordes
{
    // Admin Panel 1.1.9 applies player Fly Mode to these shared Creature.dll methods:
    // C_Controller_Base.DetermineControllerState and Input_WSAD (game 0.8.316).
    // Only the verified DLL and its untouched registrations qualify. No NPC scans.
    internal static class AdminPanelMovementGuard
    {
        internal const string AdminOwner = "humanhost.admin.panel";
        internal const string KnownHash = "a2b02ef182a1c1e88aa591ada1fc87adfb3e3b0a02ce6336328e0a246de0ac6d";
        private delegate void StatePatch(ref object result);
        private static StatePatch originalState;
        private static Func<bool> originalInput;
        private static Entry[] entries;

        private sealed class Entry
        {
            internal MethodInfo Target, Wrapper;
            internal Patch Original;
            internal bool Prefix;
        }

        internal static bool Install(Type plugin, string version, string hash)
        {
            if (entries != null) return true;
            if (plugin == null || version != "1.1.9" ||
                !string.Equals(hash, KnownHash, StringComparison.OrdinalIgnoreCase)) return false;
            var state = AccessTools.DeclaredMethod(plugin, "DetermineControllerState_Postfix");
            var input = AccessTools.DeclaredMethod(plugin, "Input_WSAD_Prefix");
            if (state == null || input == null || !state.IsStatic || !input.IsStatic ||
                state.ReturnType != typeof(void) || state.GetParameters().Length != 1 ||
                state.GetParameters()[0].ParameterType != typeof(object).MakeByRefType() ||
                input.ReturnType != typeof(bool) || input.GetParameters().Length != 0 ||
                Patched(state) || Patched(input)) return false;

            var stateEntry = Prepare("DetermineControllerState", state, nameof(StatePostfix), false);
            var inputEntry = Prepare("Input_WSAD", input, nameof(InputPrefix), true);
            if (stateEntry == null || inputEntry == null) return false;
            originalState = (StatePatch)Delegate.CreateDelegate(typeof(StatePatch), state);
            originalInput = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), input);
            entries = new[] { stateEntry, inputEntry };
            try
            {
                foreach (var entry in entries)
                {
                    // Preserve the upstream owner as well as priority/before/after so
                    // other mods' ordering constraints still refer to the same hook.
                    var harmony = new Harmony(entry.Original.owner);
                    var wrapper = Metadata(entry.Wrapper, entry.Original);
                    harmony.Patch(entry.Target, entry.Prefix ? wrapper : null, entry.Prefix ? null : wrapper);
                    harmony.Unpatch(entry.Target, entry.Original.PatchMethod);
                }
                if (entries.Any(e => !Registered(e, e.Wrapper) || Registered(e, e.Original.PatchMethod)))
                    throw new InvalidOperationException("Admin Panel wrapper registration failed.");
                return true;
            }
            catch
            {
                Stop();
                throw;
            }
        }

        private static bool Patched(MethodInfo method) => Harmony.GetPatchInfo(method)?.Owners.Count > 0;

        private static Entry Prepare(string member, MethodInfo original, string wrapper, bool prefix)
        {
            var target = AccessTools.DeclaredMethod(typeof(C_Controller_Base), member);
            if (target == null) return null;
            var patches = Harmony.GetPatchInfo(target);
            var current = prefix ? patches?.Prefixes : patches?.Postfixes;
            var matches = current?.Where(p => p.PatchMethod == original).ToArray();
            if (matches == null || matches.Length != 1 || matches[0].owner != AdminOwner) return null;
            return new Entry { Target = target, Original = matches[0], Prefix = prefix,
                Wrapper = AccessTools.DeclaredMethod(typeof(AdminPanelMovementGuard), wrapper) };
        }

        private static HarmonyMethod Metadata(MethodInfo method, Patch previous) => new HarmonyMethod(method)
        { priority = previous.priority, before = previous.before, after = previous.after };

        private static bool Registered(Entry entry, MethodInfo method)
        {
            var info = Harmony.GetPatchInfo(entry.Target);
            var current = entry.Prefix ? info?.Prefixes : info?.Postfixes;
            return current != null && current.Any(p => p.PatchMethod == method && p.owner == entry.Original.owner);
        }

        internal static void Stop()
        {
            if (entries == null) return;
            foreach (var entry in entries)
            {
                // If somebody removed our wrapper, do not resurrect a hook they fixed.
                if (!Registered(entry, entry.Wrapper)) continue;
                var harmony = new Harmony(entry.Original.owner);
                if (!Registered(entry, entry.Original.PatchMethod))
                {
                    var original = Metadata(entry.Original.PatchMethod, entry.Original);
                    harmony.Patch(entry.Target, entry.Prefix ? original : null, entry.Prefix ? null : original);
                }
                harmony.Unpatch(entry.Target, entry.Wrapper);
            }
            entries = null;
        }

        private static void StatePostfix(C_Controller_Base __instance, ref C_Controller_Base.Controller_State __result)
        {
            if (!__instance || !__instance._isPlayer) return;
            object result = __result;
            originalState(ref result);
            __result = (C_Controller_Base.Controller_State)result;
        }

        private static bool InputPrefix(C_Controller_Base __instance) =>
            !__instance || !__instance._isPlayer || originalInput();
    }
}
