// Only the external BepInEx config/chainloader boundary is substituted.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BepInEx.Configuration
{
    internal readonly struct KeyboardShortcut
    {
        internal KeyCode MainKey { get; }
        internal KeyCode[] Modifiers { get; }
        internal KeyboardShortcut(KeyCode key, params KeyCode[] modifiers) { MainKey = key; Modifiers = modifiers; }
    }
    internal sealed class SettingChangedEventArgs : EventArgs { }
    internal sealed class ConfigDefinition
    {
        internal string Section, Key;
        internal ConfigDefinition(string section, string key) { Section = section; Key = key; }
    }
    internal sealed class ConfigEntryBase { internal object BoxedValue; }
    internal sealed class ConfigFile : Dictionary<ConfigDefinition, ConfigEntryBase>
    {
        internal event EventHandler<SettingChangedEventArgs> SettingChanged;
        internal void Add(string section, string key, object value) => Add(new ConfigDefinition(section, key), new ConfigEntryBase { BoxedValue = value });
        internal void Set(string section, string key, object value)
        {
            foreach (var pair in this) if (pair.Key.Section == section && pair.Key.Key == key) pair.Value.BoxedValue = value;
            SettingChanged?.Invoke(this, new SettingChangedEventArgs());
        }
    }
}
namespace BepInEx.Bootstrap
{
    internal sealed class TestPlugin { internal Configuration.ConfigFile Config = new Configuration.ConfigFile(); }
    internal sealed class TestMetadata { internal string GUID; }
    internal sealed class TestPluginInfo
    {
        internal TestPlugin Instance = new TestPlugin(); internal TestMetadata Metadata = new TestMetadata();
        internal TestPluginInfo(string guid) { Metadata.GUID = guid; }
    }
    internal static class Chainloader { internal static readonly Dictionary<string, TestPluginInfo> PluginInfos = new Dictionary<string, TestPluginInfo>(); }
}
