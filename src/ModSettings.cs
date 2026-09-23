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
                "Horde zombies alive at once. Keep Shared AI Allowance at least this high. Higher counts can cause stutter.");
            Total = BindInt(config, "Population", "Total Spawn Budget", 1000, 1, 50000,
                "Total spawns per horde, including replacements. Multiplied by the game's Horde Quantity percentage. Dawn stops spawning.");
            Allowance = BindInt(config, "Population", "Shared AI Allowance", 90, 1, 1000,
                "Shared limit for horde and nearby ordinary zombies responding to sounds. Keep at least as high as Living Horde Target.");

            LargeLimit = BindInt(config, "Composition", "Large Zombie Living Limit", 5, 0, 500,
                "Pause extra large non-boss spawns at this living count. Counts vanilla and restored horde members; vanilla spawns may exceed it. 0 = unlimited.");
            LargePercent = BindPercent(config, "Extra Large Zombie Percentage", 20f,
                "Chance per new spawn to add a large non-boss, subject to its biome gate and living limit. Supports 0.01% steps; 0 disables extras.");
            LargeBegin = BindInt(config, "Composition", "Extra Large Zombie Begin Biome", 11, 1, 1000000,
                "First outward biome region allowing extra large zombies. 1 = starting region; 11 = second biome cycle. Vanilla spawns are unaffected.");
            BossLimit = BindInt(config, "Composition", "Boss Zombie Living Limit", 1, 0, 500,
                "Pause extra boss spawns at this living count. Counts vanilla and restored horde members; vanilla spawns may exceed it. 0 = unlimited.");
            BossPercent = BindPercent(config, "Extra Boss Zombie Percentage", 5f,
                "Chance per new spawn to add a boss, subject to its biome gate and living limit. Supports 0.01% steps; 0 disables extras.");
            BossBegin = BindInt(config, "Composition", "Extra Boss Zombie Begin Biome", 11, 1, 1000000,
                "First outward biome region allowing extra bosses. 1 = starting region; 11 = second biome cycle. Vanilla spawns are unaffected.");

            RunSpeedPercent = BindInt(config, "Movement", "Horde Run Speed Percentage", 100, 25, 150,
                "100% = vanilla running speed. Applies while the horde is spawning at night; walking and attacks are unchanged.");

            RegularResistance = BindResistance(config, "Regular Zombie Resistance", 0);
            LargeResistance = BindResistance(config, "Large Zombie Resistance", 0);
            BossResistance = BindResistance(config, "Boss Resistance", 0);

            CorpseLimit = BindInt(config, "Corpses", "Retained Corpse Limit", 300, 1, 5000,
                "Settled bodies to retain, including ordinary zombies. More bodies can reduce FPS and increase memory use; normal cleanup still applies.");

            AttractionRadius = config.Bind("Attraction", "Hearing Radius", 250f,
                new ConfigDescription("Horde-start lure radius in metres. Attracts existing nearby zombies; does not spawn more. Height counts toward the distance.", new AcceptableValueRange<float>(1f, 250f)));
            AttractionHeight = config.Bind("Attraction", "Elevated Sound Height", 100f,
                new ConfigDescription("Height above you of the additional horde-start lure, in metres. More height reduces its horizontal reach.", new AcceptableValueRange<float>(0f, 100f)));

            DebugMode = config.Bind("Diagnostics", "Debug Mode", false,
                "Show the debug HUD, enable the horde-start shortcut, and record diagnostic reports. Restart after enabling.");
            Profiling = config.Bind("Diagnostics", "Performance Profiling", false,
                "Record FPS, frame times and memory use for later analysis. Adds overhead. Restart required.");
            DetailedTimings = config.Bind("Diagnostics", "Detailed Method Timings", false, "Sample method execution times. Requires Performance Profiling; adds overhead. Restart required.");
            StartHordeHotkey = config.Bind("Diagnostics", "Start Horde Hotkey",
                new KeyboardShortcut(UnityEngine.KeyCode.Pause, UnityEngine.KeyCode.LeftControl, UnityEngine.KeyCode.LeftShift),
                "Debug only: start the next horde now; daytime advances to 19:00. Clears previous horde survivors; unavailable during spawning. None disables.");
            HudScale = config.Bind("Diagnostics", "HUD Scale", 1f, new ConfigDescription("Overlay scale.", new AcceptableValueRange<float>(0.5f, 2f)));
            HudX = BindInt(config, "Diagnostics", "HUD X", 20, 0, 7680, "Overlay left position in pixels.");
            HudY = BindInt(config, "Diagnostics", "HUD Y", 20, 0, 4320, "Overlay top position in pixels.");
            ReportFileMiB = BindInt(config, "Diagnostics", "Report File MiB", 5, 1, 64,
                "Report size before rotation, in MiB. Keeps current and previous files; discards older history. Restart required.");
        }

        private static ConfigEntry<int> BindInt(ConfigFile config, string section, string key, int value, int min, int max, string description) =>
            config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));

        private static ConfigEntry<float> BindPercent(ConfigFile config, string key, float value, string description) =>
            config.Bind("Composition", key, value, new ConfigDescription(description, new AcceptableValueRange<float>(0f, 40f)));

        private static ConfigEntry<int> BindResistance(ConfigFile config, string key, int value) =>
            BindInt(config, "Resistance", key, value, 0, 95,
                "Damage prevented for this horde category. 0% = vanilla; 50% halves damage. Persists after dawn and reload; ordinary zombies are unaffected.");

    }
}
