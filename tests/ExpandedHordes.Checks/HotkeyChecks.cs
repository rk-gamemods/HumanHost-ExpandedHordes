using System;
using System.Linq;
using ExpandedHordes;
using UnityEngine;
using BepInEx.Configuration;
using BepInEx.Bootstrap;

internal static class HotkeyChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var scan = new HotkeyConflicts();
        scan.Add(new HotkeyBinding(ModIdentity.Guid, "marker", KeyCode.F9));
        scan.Add(new HotkeyBinding("minimap", "tuning", KeyCode.F9));
        scan.Add(new HotkeyBinding("lamp", "panel", KeyCode.F11));
        scan.Add(new HotkeyBinding(ModIdentity.Guid, "folder", KeyCode.F11));
        scan.Add(new HotkeyBinding("disabled", "none", KeyCode.None));
        scan.Analyze();
        check(scan.PairCount == 2, "Minimap F9 and lamp F11 fixture overlaps are both reported");
        check(scan.Bindings.Count == 4 && scan.Findings.Any(f => f.Contains("minimap [tuning] F9")),
            "Unbound entries are ignored; reports identify owner, setting and key");
        scan.Analyze();
        check(scan.PairCount == 2, "Repeated analysis does not accumulate findings");
        scan.Bindings.RemoveAt(1); scan.Analyze();
        check(scan.PairCount == 1, "Rescan removes resolved overlaps");

        var chords = new HotkeyConflicts();
        chords.Add(new HotkeyBinding("a", "chord", KeyCode.F8, new[] { KeyCode.LeftAlt, KeyCode.LeftControl }));
        chords.Add(new HotkeyBinding("b", "same", KeyCode.F8, new[] { KeyCode.LeftControl, KeyCode.LeftAlt }));
        chords.Add(new HotkeyBinding(ModIdentity.Guid, "raw", KeyCode.F8));
        chords.Analyze();
        check(chords.PairCount == 3 && chords.Findings.Count(f => f.StartsWith("hotkey_configured_overlap")) == 1,
            "Modifier order is normalized; raw-versus-chord overlaps stay possible");
        chords.Add(new HotkeyBinding(ModIdentity.Guid, "raw modifier", KeyCode.LeftAlt)); chords.Analyze();
        check(chords.PairCount == 5, "Raw polling also conflicts with another shortcut modifier");

        var many = new HotkeyConflicts();
        for (int i = 0; i < HotkeyConflicts.MaxBindings + 1; i++) many.Add(new HotkeyBinding("owner" + i, "key", KeyCode.F8));
        many.Analyze();
        check(many.Truncated && many.Bindings.Count == 512 && many.Findings.Count == 128,
            "Discovery and findings remain bounded");
        var report = new TelemetryMetadata(null, null); many.Append(report);
        check(report.ToString().Contains("scan_truncated=True") && report.ToString().Contains("findings_truncated=True"),
            "Both scan and finding truncation are explicit in the report");

        Chainloader.PluginInfos.Clear();
        var own = new TestPluginInfo(ModIdentity.Guid);
        own.Instance.Config.Add("Diagnostics", "Marker", KeyCode.F9);
        var mini = new TestPluginInfo("minimap"); mini.Instance.Config.Add("Hotkeys", "Tuning", new KeyboardShortcut(KeyCode.F9));
        var unknown = new TestPluginInfo("unknown"); unknown.Instance.Config.Add("General", "Custom", "F8");
        Chainloader.PluginInfos.Add("own", own); Chainloader.PluginInfos.Add("mini", mini); Chainloader.PluginInfos.Add("unknown", unknown);
        HotkeyInventory.Scan();
        check(HotkeyInventory.Current.PairCount == 1 && HotkeyInventory.Current.UnknownPlugins == 1,
            "Production adapter reads loaded config values, supports both types, and discloses unsupported coverage");
        var adapterReport = new TelemetryMetadata(null, null); HotkeyInventory.Current.Append(adapterReport);
        check(adapterReport.ToString().Contains("minimap [Hotkeys :: Tuning] F9") && adapterReport.ToString().Contains("Diagnostics :: Marker"),
            "Complete adapter-to-report path preserves owning plugin, section and setting names");
        mini.Instance.Config.Set("Hotkeys", "Tuning", new KeyboardShortcut(KeyCode.None));
        check(HotkeyInventory.Dirty, "Live configuration changes invalidate the cached conflict scan");
        HotkeyInventory.Scan();
        check(!HotkeyInventory.Dirty && HotkeyInventory.Current.PairCount == 0, "Adapter rescan reflects changed current values");
        HotkeyInventory.Stop(); mini.Instance.Config.Set("Hotkeys", "Tuning", new KeyboardShortcut(KeyCode.F9));
        check(!HotkeyInventory.Dirty, "Shutdown releases config subscriptions");
        Chainloader.PluginInfos.Clear();
    }
}
