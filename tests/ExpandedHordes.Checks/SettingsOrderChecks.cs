using System;
using System.Linq;
using ExpandedHordes;

internal static class SettingsOrderChecks
{
    internal static void Run(Action<bool, string> check)
    {
        const string header = "## Settings file was created by plugin Expanded Hordes v0.2.0\n## Plugin GUID: fixture\n\n";
        string source = header +
            "[Future First]\n# future first metadata\nZ = a=b\n\n" +
            "[Diagnostics]\n\n# type bool\nPerformance Profiling = true\n\n## debug description\n# Default value: false\nDebug Mode = false\n\n" +
            "[Attraction]\n# height comment\nElevated Sound Height = 35.25\n\n; radius comment\nHearing Radius = 249.5\n\n" +
            "[Population]\n## Unknown A\nUnknown A = keep\n\n## total description\n# Acceptable value range: From 1 to 50000\nTotal Spawn Budget = 1234\n\n" +
            "## allowance description\nShared AI Allowance = 200\n\n## living description\nLiving Horde Target = 123\n\n## Unknown B\nUnknown B = also keep\n# section footer\n\n" +
            "[Corpses]\nRetained Corpse Limit = 300\n\n" +
            "[Resistance]\nBoss Resistance = 33\nLarge Zombie Resistance = 22\nRegular Zombie Resistance = 11\n\n" +
            "[Movement]\nHorde Run Speed Percentage = 100\n\n" +
            "[Composition]\nExtra Boss Zombie Begin Biome = 17\nExtra Boss Zombie Percentage = 0.25\nBoss Zombie Living Limit = 2\n" +
            "Extra Large Zombie Begin Biome = 11\nExtra Large Zombie Percentage = 0.5\nLarge Zombie Living Limit = 5\n\n" +
            "[Future Second]\nSecond = retained\n";
        string sorted = SettingsOrder.Reorder(source);
        check(sorted.StartsWith(header, StringComparison.Ordinal), "Saved config preserves the full plugin identity header");
        Ordered(check, sorted, "Sections follow gameplay-to-diagnostics order, with unknown sections stable at end",
            "[Population]", "[Composition]", "[Movement]", "[Resistance]", "[Corpses]", "[Attraction]", "[Diagnostics]", "[Future First]", "[Future Second]");
        Ordered(check, sorted, "Population values follow target, budget, allowance, then stable unknown entries",
            "Living Horde Target = 123", "Total Spawn Budget = 1234", "Shared AI Allowance = 200", "Unknown A = keep", "Unknown B = also keep");
        Ordered(check, sorted, "Composition keeps large and boss settings together",
            "Large Zombie Living Limit = 5", "Extra Large Zombie Percentage = 0.5", "Extra Large Zombie Begin Biome = 11",
            "Boss Zombie Living Limit = 2", "Extra Boss Zombie Percentage = 0.25", "Extra Boss Zombie Begin Biome = 17");
        Ordered(check, sorted, "Resistance follows regular, large, boss",
            "Regular Zombie Resistance = 11", "Large Zombie Resistance = 22", "Boss Resistance = 33");
        Ordered(check, sorted, "Attraction shows radius before height", "Hearing Radius = 249.5", "Elevated Sound Height = 35.25");
        Ordered(check, sorted, "Diagnostics starts with debug before profiling", "Debug Mode = false", "Performance Profiling = true");
        check(sorted.Contains("## total description\n# Acceptable value range: From 1 to 50000\nTotal Spawn Budget = 1234") &&
            sorted.Contains("## living description\nLiving Horde Target = 123") &&
            sorted.Contains("## debug description\n# Default value: false\nDebug Mode = false") &&
            sorted.Contains("; radius comment\nHearing Radius = 249.5"), "Descriptions, type/range metadata and user comments move with their exact values");
        check(sorted.Contains("Unknown B = also keep\n# section footer\n\n[Composition]") &&
            sorted.Contains("[Future First]\n# future first metadata\nZ = a=b\n\n[Future Second]"),
            "Unknown section contents, embedded equals and trailing section comments are preserved");
        check(source.Split('\n').OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(sorted.Split('\n').OrderBy(s => s, StringComparer.Ordinal)),
            "Reordering preserves every original line exactly once");
        check(SettingsOrder.Reorder(sorted) == sorted, "Config ordering is idempotent across repeated saves");
        string windows = source.Replace("\n", "\r\n");
        check(SettingsOrder.Reorder(windows) == sorted.Replace("\n", "\r\n"), "CRLF terminators are preserved exactly");
        string withoutFinalNewline = source.TrimEnd('\n');
        string sortedWithoutFinal = SettingsOrder.Reorder(withoutFinalNewline);
        check(!sortedWithoutFinal.EndsWith("\n", StringComparison.Ordinal) && SettingsOrder.Reorder(sortedWithoutFinal) == sortedWithoutFinal &&
            sortedWithoutFinal.Contains("Second = retained"), "A file without a terminal newline remains valid and idempotent");
        string tailMoves = "[Diagnostics]\nDebug Mode = true\n[Population]\nLiving Horde Target = 75";
        check(SettingsOrder.Reorder(tailMoves) == "[Population]\nLiving Horde Target = 75\n[Diagnostics]\nDebug Mode = true",
            "Moving an unterminated final section cannot join its value onto the next header");
        string allDiagnostics = "[Diagnostics]\nReport File MiB = 5\nHUD Y = 20\nHUD X = 20\nHUD Scale = 1\nStart Horde Hotkey = K\nDetailed Method Timings = false\nPerformance Profiling = false\nDebug Mode = true\n";
        Ordered(check, SettingsOrder.Reorder(allDiagnostics), "All diagnostic options have a stable practical order",
            "Debug Mode", "Performance Profiling", "Detailed Method Timings", "Start Horde Hotkey", "HUD Scale", "HUD X", "HUD Y", "Report File MiB");
        foreach (string malformed in new[]
        {
            "[Population]\nLiving Horde Target = 75\nLiving Horde Target = 76\n",
            "[Population]\nA = 1\n[Population]\nB = 2\n",
            "[Population]\nnot an assignment\n",
            "[Population\nA = 1\n",
            "[]\nA = 1\n",
            "before section = value\n[Population]\nA = 1\n",
            "[Population]\r\nA = 1\n",
            "[Population]\nA = 1\r"
        }) check(SettingsOrder.Reorder(malformed) == malformed, "Unexpected or ambiguous config structure is retained without rewriting");
        check(SettingsOrder.Reorder(null) == null && SettingsOrder.Reorder("") == "" &&
            SettingsOrder.Reorder("# header only\n") == "# header only\n", "Empty and comment-only files remain unchanged");
    }

    private static void Ordered(Action<bool, string> check, string text, string message, params string[] values)
    {
        int previous = -1;
        bool ordered = true;
        foreach (string value in values)
        {
            int at = text.IndexOf(value, StringComparison.Ordinal);
            ordered &= at > previous;
            previous = at;
        }
        check(ordered, message);
    }
}
