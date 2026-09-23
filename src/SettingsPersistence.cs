using System;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;

namespace ExpandedHordes
{
    // HHMM renders file order; BepInEx otherwise alphabetizes sections on every save.
    [HarmonyPatch(typeof(ConfigFile), nameof(ConfigFile.Save))]
    internal static class SettingsPersistence
    {
        private static ConfigFile owned;
        private static AccessTools.FieldRef<ConfigFile, object> ioLock;

        internal static void Initialize(ConfigFile config)
        {
            try
            {
                ioLock = AccessTools.FieldRefAccess<ConfigFile, object>("_ioLock");
                owned = config;
                FeatureRuntime.Install(Feature.Settings, typeof(SettingsPersistence));
                config.Save();
            }
            catch (Exception ex)
            {
                FeatureRuntime.WarnOnce("settings-order-initialization", "Could not refresh settings display order: " + ex.GetType().Name);
            }
        }

        private static void Postfix(ConfigFile __instance)
        {
            if (!ReferenceEquals(__instance, owned)) return;
            lock (ioLock(__instance)) ReorderFile(__instance.ConfigFilePath);
        }

        private static void ReorderFile(string path)
        {
            string temporary = null;
            try
            {
                string original = File.ReadAllText(path);
                string ordered = SettingsOrder.Reorder(original);
                if (ordered == original) return;
                temporary = path + ".order-" + System.Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporary, ordered, new UTF8Encoding(false));
                // A separate settings editor does not share BepInEx's lock.
                if (File.ReadAllText(path) != original) return;
                File.Replace(temporary, path, null);
            }
            catch (Exception ex)
            {
                FeatureRuntime.WarnOnce("settings-order", "Could not preserve settings display order: " + ex.GetType().Name);
            }
            finally
            {
                if (temporary != null && File.Exists(temporary))
                    try { File.Delete(temporary); } catch (Exception) { }
            }
        }
    }
}
