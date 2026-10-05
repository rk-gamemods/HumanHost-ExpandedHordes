using BepInEx.Configuration;

namespace ExpandedHordes
{
    // Resolve once through the public config API, then observe the live entry.
    internal sealed class RefocusAdminSetting
    {
        private ConfigEntry<bool> entry;
        internal bool Resolved { get; private set; }

        internal bool IsOn(ConfigFile config)
        {
            if (!Resolved)
            {
                Resolved = true;
                if (config != null)
                {
                    foreach (var pair in config)
                        if (pair.Key.Key == "ZombiesIgnoreYou" && pair.Value is ConfigEntry<bool> candidate)
                        {
                            if (entry == null || pair.Key.Section == "Player") entry = candidate;
                            if (pair.Key.Section == "Player") break;
                        }
                }
            }
            return entry != null && entry.Value;
        }
    }
}
