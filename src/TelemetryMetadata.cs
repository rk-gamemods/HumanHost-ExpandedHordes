using System;
using System.Globalization;
using System.Text;

namespace ExpandedHordes
{
    // Used only for explicit environment captures, never event/frame bookkeeping.
    internal sealed class TelemetryMetadata
    {
        internal const int MaxCharacters = 16384, MaxValueCharacters = 256;
        private const string Footer = "metadata_truncated=true\nmetadata_values_truncated=true\n";
        private readonly StringBuilder text = new StringBuilder(MaxCharacters, MaxCharacters);
        private readonly char[] valueBuffer = new char[MaxValueCharacters];
        private readonly string user, machine;
        private bool truncated, valuesTruncated;
        internal bool Full => truncated;
        internal TelemetryMetadata(string user, string machine) { this.user = user; this.machine = machine; }
        internal void AppendLine(string line)
        {
            if (truncated) return;
            // Reserve the complete footer and retain complete lines only.
            if (line.Length + 1 > MaxCharacters - Footer.Length - text.Length) { truncated = true; return; }
            text.Append(line).Append('\n');
        }
        internal string Safe(string value)
        {
            if (value == null) return "unavailable";
            if (value.IndexOf('\\') >= 0 || value.IndexOf('/') >= 0 || value.IndexOf('@') >= 0)
                return "[path-or-identifier omitted]";
            if ((!string.IsNullOrEmpty(user) && value.IndexOf(user, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(machine) && value.IndexOf(machine, StringComparison.OrdinalIgnoreCase) >= 0))
                return "[personal metadata omitted]";
            // Account-like runs are not requested metadata. Plugin GUIDs remain
            // useful, but never emit a Steam account identifier from an error/name.
            int digits = 0;
            for (int i = 0; i < value.Length; i++)
            {
                digits = value[i] >= '0' && value[i] <= '9' ? digits + 1 : 0;
                if (digits >= 17) return "[account-like identifier omitted]";
            }
            int length = Math.Min(value.Length, MaxValueCharacters);
            for (int i = 0; i < length; i++)
            {
                char ch = value[i];
                valueBuffer[i] = char.IsControl(ch) || char.GetUnicodeCategory(ch) == UnicodeCategory.Format ? ' ' : ch;
            }
            if (length < value.Length)
            {
                valuesTruncated = true;
                valueBuffer[length - 3] = valueBuffer[length - 2] = valueBuffer[length - 1] = '.';
            }
            return new string(valueBuffer, 0, length);
        }
        internal string Ordering(string[] owners)
        {
            if (owners == null || owners.Length == 0) return "none";
            var result = new StringBuilder(MaxValueCharacters, MaxValueCharacters);
            foreach (string owner in owners)
            {
                string value = Safe(owner);
                if (result.Length + value.Length + 1 > MaxValueCharacters - 3)
                { valuesTruncated = true; result.Append("..."); break; }
                if (result.Length != 0) result.Append('|');
                result.Append(value);
            }
            return result.ToString();
        }
        public override string ToString()
        {
            // Do not mutate the builder: repeated snapshots have identical output.
            return text.ToString() + (truncated ? "metadata_truncated=true\n" : "") +
                (valuesTruncated ? "metadata_values_truncated=true\n" : "");
        }
    }
}
