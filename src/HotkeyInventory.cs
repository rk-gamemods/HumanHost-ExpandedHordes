using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace ExpandedHordes
{
    internal static class HotkeyInventory
    {
        internal static HotkeyConflicts Current { get; private set; } = new HotkeyConflicts();
        private static readonly HashSet<ConfigFile> Watched = new HashSet<ConfigFile>();
        internal static volatile bool Dirty;
        private static void Changed(object sender, SettingChangedEventArgs args) => Dirty = true;
        internal static void Stop()
        {
            foreach (var config in Watched) config.SettingChanged -= Changed;
            Watched.Clear(); Dirty = false;
        }
        internal static void Failed() { Current = new HotkeyConflicts { Truncated = true }; }
        internal static void Scan()
        {
            Stop();
            var result = new HotkeyConflicts();
            int plugins = 0, entries = 0;
            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                if (++plugins > 128) { result.Truncated = true; break; }
                bool readable = false;
                try
                {
                    if (plugin.Instance == null) { result.UnknownPlugins++; continue; }
                    if (Watched.Add(plugin.Instance.Config)) plugin.Instance.Config.SettingChanged += Changed;
                    foreach (var pair in plugin.Instance.Config)
                    {
                        if (++entries > 4096) { result.Truncated = true; break; }
                        var value = pair.Value.BoxedValue;
                        HotkeyBinding binding;
                        string name = pair.Key.Section + " :: " + pair.Key.Key;
                        if (value is KeyCode key) binding = new HotkeyBinding(plugin.Metadata.GUID, name, key);
                        else if (value is KeyboardShortcut shortcut)
                            binding = new HotkeyBinding(plugin.Metadata.GUID, name, shortcut.MainKey, shortcut.Modifiers);
                        else continue;
                        readable = true;
                        if (!result.Add(binding)) break;
                    }
                }
                catch { result.UnknownPlugins++; result.Truncated = true; continue; }
                if (!readable) result.UnknownPlugins++;
                if (result.Truncated) break;
            }
            result.Analyze();
            Current = result;
        }
    }
}
