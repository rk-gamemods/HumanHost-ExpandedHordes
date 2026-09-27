using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ExpandedHordes;

internal static class ModMenuChecks
{
    internal static void Run(string root, Action<bool, string> check)
    {
        string path = Path.Combine(root, "modmenu.cfg");
        var config = new ConfigFile(path, false);
        ModSettings.Bind(config);
        var entries = ModSettings.MenuEntries();
        check(entries.Count == 23 && entries.All(e => e != null) &&
            entries.Select(e => e.Definition).Distinct().Count() == entries.Count,
            "Menu registers every bound setting exactly once");
        check(string.Join(",", entries.Select(e => e.Definition.Section).Distinct()) ==
            "Population,Composition,Movement,Resistance,Corpses,Attraction,Diagnostics",
            "Menu follows documented section order");
        config.Save();
        var savedKeys = SettingsOrder.Reorder(File.ReadAllText(path)).Split('\n')
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("#") && !line.StartsWith(";") && line.Contains(" = "))
            .Select(line => line.Substring(0, line.IndexOf(" = ", StringComparison.Ordinal)));
        check(entries.Select(e => e.Definition.Key).SequenceEqual(savedKeys),
            "Menu setting order matches the canonical saved config order");

        ModMenuIntegration.RegisterFromAssembly(null, typeof(ModMenu.ModMenuApi).Assembly,
            "Expanded Hordes", entries);
        check(ModMenu.ModMenuApi.RegisterCount == 1 && ModMenu.ModMenuApi.PageName == "Expanded Hordes" &&
            ModMenu.ModMenuApi.Items.Select(x => x.Entry).SequenceEqual(entries),
            "Menu receives original entries in order, with one page");
        check(ModMenu.ModMenuApi.Items.All(x => x.Description.Contains("Restart the game")) &&
            ModMenu.ModMenuApi.Items.Single(x => x.Entry == ModSettings.StartHordeHotkey)
                .Description.Contains("read-only"),
            "Menu descriptions state restart and shortcut limitations");
        ModMenuIntegration.RegisterFromAssembly(null, typeof(ModMenu.ModMenuApi).Assembly,
            "Expanded Hordes", entries);
        check(ModMenu.ModMenuApi.RegisterCount == 2 && ModMenu.ModMenuApi.Items.Length == entries.Count,
            "Repeated registration replaces the same page without extra rows");

        ModMenu.ModMenuApi.Items[0].Entry.BoxedValue = 87;
        config.Save();
        var reloaded = new ConfigFile(path, false);
        check(reloaded.Bind("Population", "Living Horde Target", 75).Value == 87,
            "Menu edits use the original BepInEx config entry and persist");

        ModMenu.ModMenuApi.ThrowAfterRegister = true;
        try
        {
            ModMenuIntegration.RegisterFromAssembly(null, typeof(ModMenu.ModMenuApi).Assembly,
                "Expanded Hordes", entries);
            throw new Exception("Expected ModMenu registration failure.");
        }
        catch (System.Reflection.TargetInvocationException) { }
        finally { ModMenu.ModMenuApi.ThrowAfterRegister = false; }
        check(ModMenu.ModMenuApi.Items == null && ModMenu.ModMenuApi.UnregisterCount == 1,
            "A partially registered page is removed after API failure");

        ModMenuIntegration.RegisterFromAssembly(null, typeof(ModMenu.ModMenuApi).Assembly,
            "Expanded Hordes", entries);
        ModMenuIntegration.Unregister(null, new BepInEx.Logging.ManualLogSource("ModMenu checks"));
        check(ModMenu.ModMenuApi.Items == null && ModMenu.ModMenuApi.UnregisterCount == 2,
            "Page is unregistered on plugin teardown");
        ModMenuIntegration.Unregister(null, new BepInEx.Logging.ManualLogSource("ModMenu checks"));
        check(ModMenu.ModMenuApi.UnregisterCount == 2,
            "Repeated teardown does not unregister twice");
    }
}

namespace ModMenu
{
    // Only the public API shape used by Expanded Hordes is mirrored here.
    public sealed class ModMenuSetting
    {
        public ConfigEntryBase Entry { get; }
        public string Description { get; }
        public ModMenuSetting(ConfigEntryBase entry, string label, string section, string description)
        {
            Entry = entry;
            Description = description;
        }
    }

    public static class ModMenuApi
    {
        public static int RegisterCount, UnregisterCount;
        public static bool ThrowAfterRegister;
        public static string PageName;
        public static ModMenuSetting[] Items;
        public static void Register(BaseUnityPlugin owner, string displayName, IEnumerable<ModMenuSetting> items)
        {
            RegisterCount++;
            PageName = displayName;
            Items = items.ToArray();
            if (ThrowAfterRegister) throw new InvalidOperationException("Simulated partial registration.");
        }
        public static void Unregister(BaseUnityPlugin owner)
        {
            UnregisterCount++;
            Items = null;
        }
    }
}
