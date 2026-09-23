using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ExpandedHordes
{
    internal static class DebugHordeTrigger
    {
        private static ConfigEntry<KeyboardShortcut> hotkey;
        private static string hint = "";
        private static MethodInfo start, context;
        private static FieldInfo spawning, save;
        private static readonly DebugHordeRequest request = new DebugHordeRequest();
        private static NPC_Horde_Mgr owner;
        private static Player_Input player;
        private static int scene;
        private static bool available;

        internal static string Hint => ModSettings.DebugMode?.Value == true
            ? (available ? hint : "Start Horde Now: unavailable") : "";

        internal static void Initialize(ConfigFile config)
        {
            hotkey = config.Bind("Diagnostics", "Start Horde Hotkey",
                new KeyboardShortcut(KeyCode.Pause, KeyCode.LeftControl, KeyCode.LeftShift),
                "Debug only: start the next horde now; daytime advances to 19:00. Clears previous horde survivors; unavailable during spawning. None disables.");
            hotkey.SettingChanged += OnHotkeyChanged;
            RefreshHint();
            try
            {
                start = AccessTools.Method(typeof(NPC_Horde_Mgr), "StartHordeEvent", new[] { typeof(bool) });
                context = AccessTools.Method(typeof(NPC_Horde_Mgr), "Try_Get_Spawn_Context");
                spawning = AccessTools.Field(typeof(NPC_Horde_Mgr), "_corHordeSpawn");
                save = AccessTools.Field(typeof(NPC_Horde_Mgr), "_hordeSaveData");
                available = start != null && context != null && spawning != null && save != null;
                if (!available) Plugin.Log.LogWarning("Start Horde Now unavailable: native horde contract changed.");
            }
            catch (Exception ex) { available = false; Plugin.Log.LogWarning("Start Horde Now unavailable: " + ex.GetType().Name); }
        }

        private static void OnHotkeyChanged(object sender, EventArgs args) { Cancel(); RefreshHint(); }

        private static void RefreshHint()
        {
            var shortcut = hotkey.Value;
            if (shortcut.MainKey == KeyCode.None) { hint = "Start Horde Now: no key assigned"; return; }
            string keys = "";
            foreach (KeyCode modifier in shortcut.Modifiers) keys += KeyLabel(modifier) + " + ";
            hint = "[" + keys + KeyLabel(shortcut.MainKey) + "] Start Horde Now";
        }

        private static string KeyLabel(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftControl: return "Ctrl";
                case KeyCode.RightControl: return "R Ctrl";
                case KeyCode.LeftShift: return "Shift";
                case KeyCode.RightShift: return "R Shift";
                case KeyCode.LeftAlt: return "Alt";
                case KeyCode.RightAlt: return "R Alt";
                default: return key.ToString();
            }
        }

        internal static void Update()
        {
            if (!available || ModSettings.DebugMode?.Value != true) { Cancel(); return; }
            try
            {
                if (request.Pending) { Advance(); return; }
                if (hotkey.Value.MainKey == KeyCode.None || !hotkey.Value.IsDown()) return;
                var mgr = NPC_Horde_Mgr.ins;
                string refusal = Refusal(mgr);
                if (refusal != null) { Report("not started: " + refusal); return; }
                if (!(bool)context.Invoke(mgr, new object[] { null })) { Report("not started: no loaded horde spawn terrain."); return; }
                owner = mgr;
                player = Player_Input.ins;
                scene = SceneManager.GetActiveScene().handle;
                request.TryQueue(Time.realtimeSinceStartup, 10);
                // Weather_Controller.Update owns propagating this into Creature_Mgr._IsDayTime.
                var time = Weather_Controller.ins._enviroMgr.Time;
                if (Creature_Mgr.ins.Is_Day_Time(time.GetTimeOfDay()))
                {
                    time.SetTimeOfDay(19f);
                    Report("requested; clock set to 19:00, waiting for night state.");
                }
                else Report("requested; preserving current nighttime.");
            }
            catch (Exception ex) { Cancel(); Report("failed: " + (ex.InnerException ?? ex).GetType().Name); }
        }

        private static void Advance()
        {
            bool valid = owner && player && NPC_Horde_Mgr.ins == owner && Player_Input.ins == player &&
                scene == SceneManager.GetActiveScene().handle && Refusal(owner) == null;
            bool nightReady = valid && !Creature_Mgr.ins._IsDayTime &&
                !Creature_Mgr.ins.Is_Day_Time(Weather_Controller.ins._enviroMgr.Time.GetTimeOfDay());
            var state = request.Poll(Time.realtimeSinceStartup, valid, nightReady);
            if (state == DebugHordeRequestState.Waiting) return;
            var mgr = owner;
            owner = null;
            player = null;
            if (state != DebugHordeRequestState.Ready)
            {
                Report(state == DebugHordeRequestState.Expired ? "cancelled: night state did not update within 10 seconds." : "cancelled: world, player or horde state changed.");
                return;
            }
            var before = ((NPC_Horde_Mgr.Horde_Save_Data)save.GetValue(mgr)).spawnedWaveCount;
            start.Invoke(mgr, new object[] { false });
            var data = (NPC_Horde_Mgr.Horde_Save_Data)save.GetValue(mgr);
            Report(data.spawnedWaveCount > before && spawning.GetValue(mgr) != null
                ? "started horde " + data.spawnedWaveCount + " with current settings. Next natural horde was rescheduled."
                : "did not begin spawning; check terrain and spawn budget.");
        }

        private static string Refusal(NPC_Horde_Mgr mgr)
        {
            if (G_Save.isQuit) return "the world is closing.";
            if (!Application.isFocused || Time.timeScale <= 0f || Cursor.visible || GUIUtility.keyboardControl != 0)
                return "return to unpaused gameplay first.";
            var currentPlayer = Player_Input.ins;
            if (!mgr || !mgr.isActiveAndEnabled || !currentPlayer || !currentPlayer.gameObject.activeInHierarchy ||
                !currentPlayer.char_Status || currentPlayer.char_Status._CurrHP <= 0 || currentPlayer._NotAllowPlay)
                return "player or world is not ready.";
            if (!Creature_Mgr.ins || !ScenePropManager.ins || ScenePropManager.ins._foundTerrasAround != 4 ||
                !Weather_Controller.ins || Weather_Controller.ins._enviroMgr == null || Weather_Controller.ins._enviroMgr.Time == null ||
                G_Save._config == null || G_Save._config._Horde_IntervalF <= 0 || save.GetValue(mgr) == null)
                return "world is still loading or natural hordes are disabled.";
            if (spawning.GetValue(mgr) != null)
                return "a horde is already spawning.";
            return null;
        }

        private static void Report(string message) => Plugin.Log.LogInfo("Start Horde Now " + message);
        internal static void Cancel() { request.Clear(); owner = null; player = null; }
        internal static void Stop() { Cancel(); available = false; if (hotkey != null) hotkey.SettingChanged -= OnHotkeyChanged; }
    }
}
