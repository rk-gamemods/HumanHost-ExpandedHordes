using System;
using System.IO;
using BepInEx.Configuration;
using ExpandedHordes;

internal static class Program
{
    private static int assertions;
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        assertions++; Console.WriteLine("PASS " + name);
    }
    private static void Main()
    {
        string temporary = Path.GetFullPath(Path.GetTempPath());
        string root = Path.GetFullPath(Path.Combine(temporary, "expanded-hordes-config-" + Guid.NewGuid().ToString("N")));
        if (!root.StartsWith(temporary, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Fixture path escaped the temporary directory.");
        try { Run(root); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void Run(string root)
    {
        Directory.CreateDirectory(root);
        string freshPath = Path.Combine(root, "fresh.cfg"); File.WriteAllText(freshPath, "");
        var fresh = new ConfigFile(freshPath, false); fresh.SaveOnConfigSet = true;
        var freshRange = AttractionSettings.Bind(fresh);
        Check(freshRange.Value == 250f && Equals(freshRange.DefaultValue, 250f), "Fresh range value and metadata default are 250");
        var limits = freshRange.Description.AcceptableValues as AcceptableValueRange<float>;
        Check(limits != null && limits.MinValue == 1f && limits.MaxValue == 250f, "Range metadata is 1..250");
        Check(freshRange.Definition.Section == "Attraction" && freshRange.Definition.Key == "Attraction Range" &&
            freshRange.Description.Description == "How far the horde-start lure reaches, in metres. Draws nearby zombies toward you, including from indoors or underground.",
            "New setting uses the requested name and description");
        Check(File.ReadAllText(freshPath) == "" && fresh.SaveOnConfigSet, "Fresh Bind performs no file writes and restores auto-save policy");

        foreach (bool hasNew in new[] { false, true })
        {
            string path = Path.Combine(root, hasNew ? "both.cfg" : "legacy.cfg");
            string original = "[Attraction]\nHearing Radius = 147.25\nElevated Sound Height = 40\n" +
                (hasNew ? "Attraction Range = 88.5\n" : "") + "[Population]\nLiving Horde Target = 123\n[Future]\nUnknown = retained\n";
            File.WriteAllText(path, original);
            var config = new ConfigFile(path, false); config.SaveOnConfigSet = true;
            var range = AttractionSettings.Bind(config);
            Check(range.Value == (hasNew ? 88.5f : 147.25f), hasNew ? "Explicit new orphan key wins over legacy radius" : "Fractional legacy radius migrates without rounding");
            Check(Equals(range.DefaultValue, 250f), "Migration keeps canonical default 250 metadata");
            Check(File.ReadAllText(path) == original && config.SaveOnConfigSet, "Migration writes nothing before caller saves");
            config.Save(); string saved = File.ReadAllText(path);
            Check(!saved.Contains("Hearing Radius") && !saved.Contains("Elevated Sound Height") && saved.Contains("Living Horde Target = 123") && saved.Contains("Unknown = retained"),
                "Saved migration removes both obsolete keys and preserves unrelated orphan values");
            var same = AttractionSettings.Bind(config); config.Save();
            Check(ReferenceEquals(range, same) && File.ReadAllText(path) == saved, "Repeated bind/save is byte-identical");
            var reload = new ConfigFile(path, false); reload.SaveOnConfigSet = false;
            var reloaded = AttractionSettings.Bind(reload); reload.Save();
            Check(reloaded.Value == range.Value && File.ReadAllText(path) == saved && !reload.SaveOnConfigSet,
                "Migration remains idempotent after actual BepInEx reload");
        }

        foreach (bool boundNew in new[] { false, true })
        {
            string path = Path.Combine(root, boundNew ? "bound-new.cfg" : "bound-legacy.cfg");
            File.WriteAllText(path, "");
            var config = new ConfigFile(path, false); config.SaveOnConfigSet = false;
            config.Bind("Attraction", "Hearing Radius", 130.75f);
            config.Bind("Attraction", "Elevated Sound Height", 77f);
            var unrelated = config.Bind("Other", "Keep", "unmodified");
            if (boundNew) config.Bind("Attraction", "Attraction Range", 250f).Value = 91.25f;
            config.Save(); string before = File.ReadAllText(path);
            config.SaveOnConfigSet = true;
            var range = AttractionSettings.Bind(config);
            Check(range.Value == (boundNew ? 91.25f : 130.75f), boundNew ? "Explicit bound new setting wins over bound legacy radius" : "Already-bound legacy radius migrates its current value");
            Check(File.ReadAllText(path) == before && config.SaveOnConfigSet, "Bound-entry migration performs no transient save");
            config.Save(); string saved = File.ReadAllText(path);
            Check(!config.ContainsKey(new ConfigDefinition("Attraction", "Hearing Radius")) &&
                !config.ContainsKey(new ConfigDefinition("Attraction", "Elevated Sound Height")) &&
                !saved.Contains("Hearing Radius") && !saved.Contains("Elevated Sound Height") &&
                unrelated.Value == "unmodified" && saved.Contains("Keep = unmodified"),
                "Both bound legacy entries are removed while unrelated bound value is retained");
        }
        Console.WriteLine("PASS: " + assertions + " actual BepInEx attraction migration assertions. Temporary fixtures are removed after this run.");
    }
}
