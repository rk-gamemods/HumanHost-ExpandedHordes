using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace ExpandedHordes
{
    // ModMenu is optional. Resolve its public API from the loaded plugin so
    // Expanded Hordes has no runtime or build dependency on a local ModMenu DLL.
    internal static class ModMenuIntegration
    {
        private const string MenuGuid = "humanhost.modmenu";
        private static MethodInfo unregister;
        private static bool registered;

        internal static void Register(BaseUnityPlugin owner, IReadOnlyList<ConfigEntryBase> entries, ManualLogSource log)
        {
            if (!Chainloader.PluginInfos.TryGetValue(MenuGuid, out var menu)) return;
            try
            {
                if (menu.Instance == null) throw new InvalidOperationException("ModMenu instance is unavailable.");
                RegisterFromAssembly(owner, menu.Instance.GetType().Assembly, owner.Info.Metadata.Name, entries);
                log.LogInfo("Registered Expanded Hordes settings with Human Host Mod Menu.");
            }
            catch (Exception ex)
            {
                log.LogWarning("ModMenu settings page unavailable; use HHMM or the BepInEx config file. " +
                    ex.GetBaseException().GetType().Name);
            }
        }

        // Separate from Chainloader discovery so the reflection contract can be
        // checked with an in-process fake menu and no third-party DLL in source.
        internal static void RegisterFromAssembly(BaseUnityPlugin owner, Assembly assembly,
            string displayName, IReadOnlyList<ConfigEntryBase> entries)
        {
            Type api = assembly.GetType("ModMenu.ModMenuApi", true);
            Type setting = assembly.GetType("ModMenu.ModMenuSetting", true);
            Type settings = typeof(IEnumerable<>).MakeGenericType(setting);
            ConstructorInfo create = setting.GetConstructor(new[]
                { typeof(ConfigEntryBase), typeof(string), typeof(string), typeof(string) });
            MethodInfo register = api.GetMethod("Register", new[] { typeof(BaseUnityPlugin), typeof(string), settings });
            MethodInfo remove = api.GetMethod("Unregister", new[] { typeof(BaseUnityPlugin) });
            if (create == null || register == null || remove == null)
                throw new MissingMethodException("Compatible ModMenu registration API is unavailable.");

            Array items = Array.CreateInstance(setting, entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                ConfigEntryBase entry = entries[i] ?? throw new InvalidOperationException("Unbound menu setting.");
                string description = entry.Description.Description + " Restart the game after changing settings.";
                if (entry.SettingType == typeof(KeyboardShortcut))
                    description += " If this shortcut is read-only here, change it in HHMM or the config file.";
                items.SetValue(create.Invoke(new object[] { entry, null, null, description }), i);
            }

            try { register.Invoke(null, new object[] { owner, displayName, items }); }
            catch
            {
                // An API variant could expose a page before throwing. Remove only
                // our own page before falling back to ordinary BepInEx settings.
                try { remove.Invoke(null, new object[] { owner }); } catch (Exception) { }
                throw;
            }
            unregister = remove;
            registered = true;
        }

        internal static void Unregister(BaseUnityPlugin owner, ManualLogSource log)
        {
            if (!registered) return;
            try { unregister.Invoke(null, new object[] { owner }); }
            catch (Exception ex) { log.LogWarning("Could not unregister ModMenu settings page: " + ex.GetBaseException().GetType().Name); }
            finally { registered = false; unregister = null; }
        }
    }
}
