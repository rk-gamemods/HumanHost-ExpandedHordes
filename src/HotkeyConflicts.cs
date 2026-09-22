using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExpandedHordes
{
    internal sealed class HotkeyBinding
    {
        internal readonly string Owner, Setting;
        internal readonly KeyCode Main;
        internal readonly KeyCode[] Modifiers;
        internal HotkeyBinding(string owner, string setting, KeyCode main, IEnumerable<KeyCode> modifiers = null)
        {
            Owner = owner; Setting = setting; Main = main;
            Modifiers = (modifiers ?? Array.Empty<KeyCode>()).Where(k => k != KeyCode.None).Distinct().OrderBy(k => k).ToArray();
        }
        internal string Keys => Main + (Modifiers.Length == 0 ? "" : " + " + string.Join(" + ", Modifiers));
    }

    // Configured overlap is evidence, not proof that both actions run in the same context.
    internal sealed class HotkeyConflicts
    {
        internal const int MaxBindings = 512, MaxPairs = 128;
        internal readonly List<HotkeyBinding> Bindings = new List<HotkeyBinding>();
        internal readonly List<string> Findings = new List<string>();
        internal int PairCount, UnknownPlugins;
        internal bool Truncated;
        internal bool Add(HotkeyBinding binding)
        {
            if (binding.Main == KeyCode.None) return true;
            if (Bindings.Count == MaxBindings) { Truncated = true; return false; }
            Bindings.Add(binding); return true;
        }
        internal void Analyze()
        {
            Findings.Clear(); PairCount = 0;
            var safe = new TelemetryMetadata(Environment.UserName, Environment.MachineName);
            for (int i = 0; i < Bindings.Count; i++)
                for (int j = i + 1; j < Bindings.Count; j++)
                {
                    var a = Bindings[i]; var b = Bindings[j];
                    // Raw KeyCode polling can also fire when that key is used as a modifier.
                    bool sameMain = a.Main == b.Main;
                    bool aRawOverlap = a.Modifiers.Length == 0 && b.Modifiers.Contains(a.Main);
                    bool bRawOverlap = b.Modifiers.Length == 0 && a.Modifiers.Contains(b.Main);
                    if (!sameMain && !aRawOverlap && !bRawOverlap) continue;
                    PairCount++;
                    if (Findings.Count == MaxPairs) continue;
                    string kind = sameMain && a.Modifiers.SequenceEqual(b.Modifiers) ? "configured_overlap" : "possible_overlap";
                    Findings.Add("hotkey_" + kind + " first=" + safe.Safe(a.Owner) + " [" + safe.Safe(a.Setting) + "] " + a.Keys +
                        " second=" + safe.Safe(b.Owner) + " [" + safe.Safe(b.Setting) + "] " + b.Keys);
                }
        }
        internal string Summary => "Hotkeys: " + PairCount + " configured/possible overlaps; " + UnknownPlugins +
            " plugins without readable bindings. Coverage partial" + (Truncated ? "; scan incomplete" : ".") +
            " Hardcoded, native and external bindings unknown.";
        internal void Append(TelemetryMetadata report)
        {
            report.AppendLine("hotkey_coverage=loaded BepInEx KeyCode and KeyboardShortcut config entries only; hardcoded/native/external bindings and activation conditions unknown");
            report.AppendLine("hotkey_bindings=" + Bindings.Count + " overlaps=" + PairCount + " unknown_plugins=" + UnknownPlugins +
                " scan_truncated=" + Truncated + " findings_truncated=" + (PairCount > Findings.Count));
            foreach (string line in Findings) { if (report.Full) break; report.AppendLine(line); }
        }
    }
}
