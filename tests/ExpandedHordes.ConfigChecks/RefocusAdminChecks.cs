using System;
using System.IO;
using BepInEx.Configuration;
using ExpandedHordes;

internal static class RefocusAdminChecks
{
    internal static void Run(string root, Action<bool, string> check)
    {
        ConfigFile NewConfig(string name)
        {
            string path = Path.Combine(root, name + ".cfg");
            File.WriteAllText(path, "");
            return new ConfigFile(path, false) { SaveOnConfigSet = false };
        }

        var missing = new RefocusAdminSetting();
        check(!missing.IsOn(null) && missing.Resolved, "Absent Admin Panel resolves once as off");
        var config = NewConfig("admin-player");
        config.Bind("Other", "ZombiesIgnoreYou", false);
        var entry = config.Bind("Player", "ZombiesIgnoreYou", true);
        var setting = new RefocusAdminSetting();
        check(setting.IsOn(config), "Admin Panel Player setting is read through the real BepInEx API");
        entry.Value = false;
        check(!setting.IsOn(null), "Cached Admin Panel entry observes a live switch off");
        entry.Value = true;
        check(setting.IsOn(null), "Cached Admin Panel entry observes a live switch on");
        check(!missing.IsOn(config), "Absent plugin is not repeatedly resolved");

        var fallback = NewConfig("admin-other-section");
        var fallbackEntry = fallback.Bind("Changed Section", "ZombiesIgnoreYou", true);
        check(new RefocusAdminSetting().IsOn(fallback), "Admin Panel key is found when its section differs");
        fallbackEntry.Value = false;
        check(setting.IsOn(fallback), "Cached resolution keeps the original config entry");

        var absent = NewConfig("admin-no-entry");
        absent.Bind("Player", "Other", true);
        var absentSetting = new RefocusAdminSetting();
        check(!absentSetting.IsOn(absent), "Missing ZombiesIgnoreYou entry is off");
        absent.Bind("Player", "ZombiesIgnoreYou", true);
        check(!absentSetting.IsOn(absent), "Missing entry is resolved only once");

        var wrongType = NewConfig("admin-wrong-type");
        wrongType.Bind("Player", "ZombiesIgnoreYou", "true");
        check(!new RefocusAdminSetting().IsOn(wrongType), "Non-Boolean entry is not treated as enabled");
        wrongType.Bind("Changed Section", "ZombiesIgnoreYou", true);
        check(new RefocusAdminSetting().IsOn(wrongType), "Key fallback selects an existing Boolean entry");
        check(File.ReadAllText(config.ConfigFilePath) == "" && File.ReadAllText(fallback.ConfigFilePath) == "",
            "Admin Panel observation does not write config files");
    }
}
