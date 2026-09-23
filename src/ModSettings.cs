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

        internal static void Bind(ConfigFile config)
        {
            DetailedTimings = config.Bind("Diagnostics", "Detailed Method Timings", false, "Advanced, requires Performance Profiling. Installs hot-method hooks and samples about 1/32 calls with at most 98 timed calls per bucket. Adds dispatch overhead even for skipped calls. Restart required.");
            HudScale = config.Bind("Diagnostics", "HUD Scale", 1f, new ConfigDescription("Overlay scale.", new AcceptableValueRange<float>(0.5f, 2f)));
            HudX = BindInt(config, "Diagnostics", "HUD X", 20, 0, 7680, "Overlay left position in pixels.");
            HudY = BindInt(config, "Diagnostics", "HUD Y", 20, 0, 4320, "Overlay top position in pixels.");
            ReportFileMiB = BindInt(config, "Diagnostics", "Report File MiB", 5, 1, 64,
                "Rotate each CSV/log after a batch reaches this many MiB. Keep current and previous files per stream; older history is discarded. One completed batch may exceed the threshold. Restart required.");
            RegularResistance = BindResistance(config, "Regular Zombie Resistance", 0);
            LargeResistance = BindResistance(config, "Large Zombie Resistance", 0);
            BossResistance = BindResistance(config, "Boss Resistance", 0);
            CorpseLimit = BindInt(config, "Corpses", "Retained Corpse Limit", 300, 1, 5000,
                "How many nearby settled corpses the game's normal cleanup tries to retain. Default: 300, matching the vanilla menu's maximum. A higher limit keeps more bodies visible; it can reduce FPS and increase memory/save size. The higher of this setting and the game's current limit is used, so another mod's higher limit is respected. This affects the shared corpse pool, including ordinary zombies. Old corpses are removed to make room; corpses do not block new living horde spawns. Native expiry and distance cleanup still apply. This does not keep physical ragdolls active forever or guarantee stacked collision piles. The upper setting of 5,000 is untested.");
            DebugMode = config.Bind("Diagnostics", "Debug Mode", false,
                "Enable the debug HUD, numeric event/placement diagnostics, background reports and debug messages. The HUD indicates Debug Mode and has no separate toggle or hotkey. Default off. Errors remain in the BepInEx log. Restart after enabling diagnostics from all-off.");
            Profiling = config.Bind("Diagnostics", "Performance Profiling", false,
                "Collect nominal 100 ms buckets, frame histograms, optional delayed CPU/GPU samples and 1 Hz managed memory/GC. Background writer flushes every 15 seconds into the diagnostics folder beside the DLL. Detailed Method Timings is separate and off by default. Missing measurements stay unavailable. Runtime overhead has not been certified. Restart required.");
            Total = BindInt(config, "Population", "Total Spawn Budget", 1000, 1, 50000,
                "Base number of zombies one horde can create, including replacements after kills. The game's Horde Quantity percentage multiplies this number: a base of 1,000 allows 200 at 20%, 1,000 at 100%, or 8,000 at 800%. The 50,000 maximum applies BEFORE the multiplier, allowing a total budget of 400,000 at 800%. The final total is rounded to a whole number. Dawn stops new arrivals even if the total has not been reached. Vanilla starts at 15 at 100% or 120 at 800%, then adds 2 for each previous horde. This mod instead uses your chosen base times the game's percentage, without per-horde growth. Default base: 1,000. Allowed base: 1-50,000. Enter an exact value in the number box beside the slider. Very small bases with low percentages can round to zero. Horde frequency stays controlled by the game.");
            Living = BindInt(config, "Population", "Living Horde Target", 75, 1, 500,
                "Living horde zombies at once. Default: 75; maximum: 500. Keep Shared AI Allowance at least this high. On a high-end Ryzen 7 9800X3D, RTX 4070 Ti and 64 GB RAM system, 200 alive with bosses and large types felt like a full horde: about 65 FPS, playable, with stutter beginning. Results vary by system and settings.");
            Allowance = BindInt(config, "Population", "Shared AI Allowance", 90, 1, 1000,
                "How many loaded zombies the game allows to focus on targets when reacting to sounds. Vanilla: 60. Mod default: 90. Horde zombies and ordinary nearby zombies share this limit, even between hordes. Keep this at least as high as Living Horde Target; otherwise it also lowers that target. Raising it can make more zombies chase and fight, which can reduce FPS. No safe performance maximum has been measured.");
            RunSpeedPercent = BindInt(config, "Movement", "Horde Run Speed Percentage", 100, 25, 150,
                "Horde running speed while spawning at night. Default: 100% (vanilla); range: 25-150%. 150% already felt extremely fast in play tests. Normal speed returns when spawning stops or dawn arrives. Walking, attacks and ordinary zombies are unaffected.");
            LargeBegin = BindInt(config, "Composition", "Extra Large Zombie Begin Biome", 11, 1, 1000000,
                "First biome region where this mod can add large zombies to a horde. Regions count outward from the starting area, including repeats: 11 begins the second cycle of ten biomes. Set 1 to allow extras from the starting region. Returning to an earlier region stops these extra choices. Large zombies that vanilla already includes are unaffected.");
            BossBegin = BindInt(config, "Composition", "Extra Boss Zombie Begin Biome", 11, 1, 1000000,
                "First biome region where this mod can add bosses to a horde. Regions count outward from the starting area, including repeats: 11 begins the second cycle of ten biomes. Set 1 to allow extras from the starting region. Returning to an earlier region stops these extra choices. Bosses that vanilla already includes are unaffected.");
            LargePercent = BindPercent(config, "Extra Large Zombie Percentage", 20f,
                "Chance per new horde spawn, including replacements, to add a large non-boss once its begin biome is reached. Range: 0-40% in 0.01% steps, including 0.5 and 0.25. Scripted additions pause at Large Zombie Living Limit. Vanilla choices and restored zombies are unchanged.");
            BossPercent = BindPercent(config, "Extra Boss Zombie Percentage", 5f,
                "Chance per new horde spawn, including replacements, to add a boss once its begin biome is reached. Range: 0-40% in 0.01% steps, including 0.5 and 0.25. Scripted additions pause at Boss Zombie Living Limit. Vanilla choices and restored zombies are unchanged.");
            LargeLimit = BindInt(config, "Composition", "Large Zombie Living Limit", 5, 0, 500,
                "Stop scripted large non-boss additions while this many large horde zombies are alive. Default: 5. 0 = unlimited. Vanilla and restored horde zombies count toward the limit, but vanilla spawns are not altered and may exceed it. Normal random chance resumes below the limit; existing zombies are never removed.");
            BossLimit = BindInt(config, "Composition", "Boss Zombie Living Limit", 1, 0, 500,
                "Stop scripted boss additions while this many horde bosses are alive. Default: 1. 0 = unlimited. Vanilla and restored horde bosses count toward the limit, but vanilla spawns are not altered and may exceed it. Normal random chance resumes below the limit; existing bosses are never removed.");
            AttractionHeight = config.Bind("Attraction", "Elevated Sound Height", 100f,
                new ConfigDescription("At horde start, the mod tries to lure nearby zombies toward you as if you made a noise. It does this once at your head and once this many metres above it, to help the lure reach over walls and roofs. You hear no sound, and zombies still try to reach you on the ground. Vanilla has no such horde-start lure. Default: 100 metres above your head. Buildings can still block it. Hearing Radius measures distance from each lure, so raising the height uses up part of its reach. Maximum height: 100 metres.", new AcceptableValueRange<float>(0f, 100f)));
            AttractionRadius = config.Bind("Attraction", "Hearing Radius", 250f,
                new ConfigDescription("How far the one-time horde-start lure can reach, in metres from each of its two positions. It only attracts zombies already nearby; it never creates distant zombies or loads more of the map. Vanilla normally creates ordinary zombies 50-100 metres from you and removes them beyond 150 metres. The maximum here is 250 metres to cover those nearby zombies plus the lure up to 100 metres overhead. A million-metre radius would not attract zombies from an unloaded world. Height counts toward the distance. Walls, roofs and the shared zombie limit can still stop a response. Attracting more existing zombies can reduce FPS through chasing and fighting; the cost has not been measured.", new AcceptableValueRange<float>(1f, 250f)));
        }

        private static ConfigEntry<int> BindInt(ConfigFile config, string section, string key, int value, int min, int max, string description) =>
            config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));

        private static ConfigEntry<float> BindPercent(ConfigFile config, string key, float value, string description) =>
            config.Bind("Composition", key, value, new ConfigDescription(description, new AcceptableValueRange<float>(0f, 40f)));

        private static ConfigEntry<int> BindResistance(ConfigFile config, string key, int value) =>
            BindInt(config, "Resistance", key, value, 0, 95,
                "Incoming damage prevented for this horde category. Default: 0% (vanilla, no added reduction). Raise it for tougher enemies: 50% halves damage; maximum: 95%. Maximum health and kill XP are unchanged. Applies to surviving horde zombies after dawn and save/load; ordinary zombies are unaffected. Direct forced-death effects can bypass this reduction.");

    }
}
