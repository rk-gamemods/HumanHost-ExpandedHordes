using System;
using System.IO;
using System.Security.Cryptography;
using BepInEx.Bootstrap;

namespace ExpandedHordes
{
    internal static class AdminPanelCompatibility
    {
        internal static void Install()
        {
            try
            {
                if (!Chainloader.PluginInfos.TryGetValue(AdminPanelMovementGuard.AdminOwner, out var info) ||
                    info.Metadata.Version.ToString() != "1.1.9" || !info.Instance) return;
                var type = info.Instance.GetType();
                string hash;
                using (var file = File.OpenRead(type.Assembly.Location))
                using (var sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
                AdminPanelMovementGuard.Install(type, info.Metadata.Version.ToString(), hash);
            }
            catch (Exception ex)
            { FeatureRuntime.DebugLog("Optional Admin Panel movement guard skipped: " + ex.GetType().Name); }
        }

        internal static void Stop()
        {
            try { AdminPanelMovementGuard.Stop(); }
            catch (Exception ex)
            { FeatureRuntime.DebugLog("Optional Admin Panel guard cleanup: " + ex.GetType().Name); }
        }
    }
}
