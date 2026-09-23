using System;
using System.Collections.Generic;
using System.Linq;

namespace ExpandedHordes
{
    // Reorder BepInEx's saved text, moving each value with its description and
    // metadata. Unknown entries remain intact after the known entries.
    internal static class SettingsOrder
    {
        private static readonly string[] Sections =
            { "Population", "Composition", "Movement", "Resistance", "Corpses", "Attraction", "Diagnostics" };
        private static readonly string[][] Keys =
        {
            new[] { "Living Horde Target", "Total Spawn Budget", "Shared AI Allowance" },
            new[] { "Large Zombie Living Limit", "Extra Large Zombie Percentage", "Extra Large Zombie Begin Biome",
                "Boss Zombie Living Limit", "Extra Boss Zombie Percentage", "Extra Boss Zombie Begin Biome" },
            new[] { "Horde Run Speed Percentage" },
            new[] { "Regular Zombie Resistance", "Large Zombie Resistance", "Boss Resistance" },
            new[] { "Retained Corpse Limit" },
            new[] { "Hearing Radius", "Elevated Sound Height" },
            new[] { "Debug Mode", "Performance Profiling", "Detailed Method Timings", "Start Horde Hotkey",
                "HUD Scale", "HUD X", "HUD Y", "Report File MiB" }
        };

        internal static string Reorder(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            string newline = source.Contains("\r\n") ? "\r\n" : "\n";
            string normalized = source.Replace(newline, "\n");
            // Mixed terminators or an unsupported bare CR are left untouched.
            if (normalized.Contains('\r') || (newline == "\r\n" && source.Replace("\r\n", "").Contains('\n'))) return source;
            bool finalNewline = source.EndsWith(newline, StringComparison.Ordinal);
            var lines = normalized.Split('\n').ToList();
            if (finalNewline) lines.RemoveAt(lines.Count - 1);
            var prefix = new List<string>();
            var sections = new List<Section>();
            var sectionNames = new HashSet<string>(StringComparer.Ordinal);
            Section current = null;
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    if (!trimmed.EndsWith("]", StringComparison.Ordinal) || trimmed.Length < 3) return source;
                    string name = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    if (name.Length == 0 || name.IndexOfAny(new[] { '[', ']' }) >= 0 || !sectionNames.Add(name)) return source;
                    current = new Section(name, line);
                    sections.Add(current);
                }
                else if (current == null)
                {
                    if (!CommentOrBlank(trimmed)) return source;
                    prefix.Add(line);
                }
                else current.Lines.Add(line);
            }
            if (sections.Count == 0) return source;
            foreach (var section in sections) if (!section.Parse()) return source;

            var result = new List<string>(prefix);
            foreach (var section in sections.OrderBy(s => Rank(Sections, s.Name)))
            {
                result.Add(section.Header);
                int index = Array.IndexOf(Sections, section.Name);
                IEnumerable<Entry> ordered = index < 0 ? section.Entries : section.Entries.OrderBy(e => Rank(Keys[index], e.Key));
                foreach (var entry in ordered) result.AddRange(entry.Lines);
                result.AddRange(section.Tail);
            }
            return string.Join(newline, result) + (finalNewline ? newline : "");
        }

        private static int Rank(string[] names, string value)
        {
            int index = Array.IndexOf(names, value);
            return index < 0 ? int.MaxValue : index;
        }
        private static bool CommentOrBlank(string line) => line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) ||
            line.StartsWith(";", StringComparison.Ordinal);

        private sealed class Entry
        {
            internal string Key;
            internal List<string> Lines;
        }
        private sealed class Section
        {
            internal readonly string Name, Header;
            internal readonly List<string> Lines = new List<string>();
            internal readonly List<Entry> Entries = new List<Entry>();
            internal List<string> Tail;
            internal Section(string name, string header) { Name = name; Header = header; }
            internal bool Parse()
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var pending = new List<string>();
                foreach (string line in Lines)
                {
                    pending.Add(line);
                    string trimmed = line.Trim();
                    if (CommentOrBlank(trimmed)) continue;
                    int equals = line.IndexOf('=');
                    if (equals < 1) return false;
                    string key = line.Substring(0, equals).Trim();
                    if (key.Length == 0 || !seen.Add(key)) return false;
                    Entries.Add(new Entry { Key = key, Lines = pending });
                    pending = new List<string>();
                }
                Tail = pending;
                return true;
            }
        }
    }
}
