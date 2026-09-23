using BepInEx.Configuration;

namespace ExpandedHordes
{
    // One binding definition for the plugin and pre-launch local installation.
    internal static class ModSettings
    {
        internal static ConfigEntry<int> Total, Living, Allowance, LargeBegin, BossBegin, LargeLimit, BossLimit, RunSpeedPercent;
        internal static ConfigEntry<float> AttractionHeight, AttractionRadius, LargePercent, BossPercent;
        internal static ConfigEntry<int> RegularResistance, LargeResistance, BossResistance, CorpseLimit;
        internal static ConfigEntry<bool> DebugMode, Profiling, DetailedTimings;
        internal static ConfigEntry<float> HudScale;
        internal static ConfigEntry<int> HudX, HudY, ReportFileMiB;
        internal static ConfigEntry<KeyboardShortcut> StartHordeHotkey;

        internal static void Bind(ConfigFile config)
        {
            Living = BindInt(config, "Population", "Living Horde Target", 75, 1, 500,
                "Zombies alive at the same time. Keep Shared AI Allowance at least this high. Higher counts come with heavy performance cost.");
            Total = BindInt(config, "Population", "Total Spawn Budget", 1000, 1, 50000,
                "Total spawn limit for one horde, including replacements. Multiplied by the game's Horde Quantity setting. Spawning stops at dawn.");
            Allowance = BindInt(config, "Population", "Shared AI Allowance", 90, 1, 1000,
                "Limits how many horde and nearby zombies react to noise. Keep this at least as high as Living Horde Target.");

            LargeLimit = BindInt(config, "Composition", "Large Zombie Living Limit", 5, 0, 500,
                "Stop extra large spawns at this count. Counts large horde zombies from vanilla spawns and loaded saves. 0 = unlimited. Vanilla spawns ignore this limit.");
            LargePercent = BindPercent(config, "Extra Large Zombie Percentage", 20f,
                "Chance to add a large non-boss on each spawn. Uses the biome and living limits. Accepts 0.01% steps; 0 turns off extras.");
            LargeBegin = BindInt(config, "Composition", "Extra Large Zombie Begin Biome", 11, 1, 1000000,
                "Biome number where extra large zombies start. Counts outward from spawn: 1 = first biome, 11 = start of the second cycle.");
            BossLimit = BindInt(config, "Composition", "Boss Zombie Living Limit", 1, 0, 500,
                "Stop extra boss spawns at this count. Counts horde bosses from vanilla spawns and loaded saves. 0 = unlimited. Vanilla spawns ignore this limit.");
            BossPercent = BindPercent(config, "Extra Boss Zombie Percentage", 5f,
                "Chance to add a boss on each spawn. Uses the biome and living limits. Accepts 0.01% steps; 0 turns off extras.");
            BossBegin = BindInt(config, "Composition", "Extra Boss Zombie Begin Biome", 11, 1, 1000000,
                "Biome number where extra bosses start. Counts outward from spawn: 1 = first biome, 11 = start of the second cycle.");

            RunSpeedPercent = BindInt(config, "Movement", "Horde Run Speed Percentage", 100, 25, 150,
                "100% = vanilla run speed. Only changes running while the horde is still spawning at night. Does not speed up walking or attacks.");

            RegularResistance = BindResistance(config, "Regular Zombie Resistance", 0);
            LargeResistance = BindResistance(config, "Large Zombie Resistance", 0);
            BossResistance = BindResistance(config, "Boss Resistance", 0);

            CorpseLimit = BindInt(config, "Corpses", "Retained Corpse Limit", 300, 1, 5000,
                "Dead bodies kept nearby. More bodies use more memory and lower FPS. Older bodies are removed to make room.");

            AttractionRadius = config.Bind("Attraction", "Hearing Radius", 250f,
                new ConfigDescription("How far zombies hear the horde-start lure, in metres. Only attracts zombies already nearby.", new AcceptableValueRange<float>(1f, 250f)));
            AttractionHeight = config.Bind("Attraction", "Elevated Sound Height", 100f,
                new ConfigDescription("Height of the extra lure above you, in metres. A higher lure reaches less ground.", new AcceptableValueRange<float>(0f, 100f)));

            DebugMode = config.Bind("Diagnostics", "Debug Mode", false,
                "Show the HUD and save test logs. Enables the Start Horde hotkey. Restart after turning this on.");
            Profiling = config.Bind("Diagnostics", "Performance Profiling", false,
                "Record FPS, frame times and memory use in the logs. Uses extra CPU time. Restart required.");
            DetailedTimings = config.Bind("Diagnostics", "Detailed Method Timings", false, "Extra timing data for finding slow code. Requires Performance Profiling and uses more CPU time. Restart required.");
            StartHordeHotkey = config.Bind("Diagnostics", "Start Horde Hotkey",
                new KeyboardShortcut(UnityEngine.KeyCode.Pause, UnityEngine.KeyCode.LeftControl, UnityEngine.KeyCode.LeftShift),
                "Start a horde now. Requires Debug Mode. Skips daytime to 19:00 and removes survivors from the last horde. None disables the key.");
            HudScale = config.Bind("Diagnostics", "HUD Scale", 1f, new ConfigDescription("Size of the debug HUD.", new AcceptableValueRange<float>(0.5f, 2f)));
            HudX = BindInt(config, "Diagnostics", "HUD X", 20, 0, 7680, "Distance from the left edge of the screen, in pixels.");
            HudY = BindInt(config, "Diagnostics", "HUD Y", 20, 0, 4320, "Distance from the top of the screen, in pixels.");
            ReportFileMiB = BindInt(config, "Diagnostics", "Report File MiB", 5, 1, 64,
                "Start a new log file at this size, in MiB. Keeps the latest two files per log. Restart required.");
        }

        private static ConfigEntry<int> BindInt(ConfigFile config, string section, string key, int value, int min, int max, string description) =>
            config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));

        private static ConfigEntry<float> BindPercent(ConfigFile config, string key, float value, string description) =>
            config.Bind("Composition", key, value, new ConfigDescription(description, new AcceptableValueRange<float>(0f, 40f)));

        private static ConfigEntry<int> BindResistance(ConfigFile config, string key, int value) =>
            BindInt(config, "Resistance", key, value, 0, 95,
                "Damage reduction for these horde zombies. 0% = vanilla; 50% = half damage. Stays after dawn and loading a save. Does not affect non-horde zombies.");

    }
}
