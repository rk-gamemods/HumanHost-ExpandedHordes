using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;

namespace ExpandedHordes
{
    internal static class AttractionSettings
    {
        private static readonly ConfigDefinition Range = new ConfigDefinition("Attraction", "Attraction Range");
        private static readonly ConfigDefinition LegacyRadius = new ConfigDefinition("Attraction", "Hearing Radius");
        private static readonly ConfigDefinition LegacyHeight = new ConfigDefinition("Attraction", "Elevated Sound Height");
        // BepInEx 5.4.23.5 retains not-yet-bound and obsolete settings here.
        private static readonly PropertyInfo OrphanedEntries = typeof(ConfigFile).GetProperty("OrphanedEntries",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        internal static ConfigEntry<float> Bind(ConfigFile config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var orphans = OrphanedEntries?.GetValue(config) as IDictionary<ConfigDefinition, string>;
            if (orphans == null) throw new InvalidOperationException("BepInEx orphaned settings are unavailable.");
            bool saveOnChange = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                // An explicit new setting takes precedence over either legacy form.
                if (!config.ContainsKey(Range) && !orphans.ContainsKey(Range))
                {
                    if (config.ContainsKey(LegacyRadius)) orphans[Range] = config[LegacyRadius].GetSerializedValue();
                    else if (orphans.TryGetValue(LegacyRadius, out string oldValue)) orphans[Range] = oldValue;
                }
                var range = config.Bind(Range, 250f, new ConfigDescription(
                    "How far the horde-start lure reaches, in metres. Draws nearby zombies toward you, including from indoors or underground.",
                    new AcceptableValueRange<float>(1f, 250f)));
                config.Remove(LegacyRadius); config.Remove(LegacyHeight);
                orphans.Remove(LegacyRadius); orphans.Remove(LegacyHeight);
                return range;
            }
            finally { config.SaveOnConfigSet = saveOnChange; }
        }
    }
}
