using System;
using System.Collections.Generic;

namespace ExpandedHordes
{
    // Only holds references while a death callback is on the current thread's stack.
    // Storage is reused; no scene scan, delayed work, or persistent object-ID cache.
    internal static class DeathRecycleGuard<T> where T : class
    {
        private struct Entry { internal T Target; internal bool Returned; internal long Token; }
        [ThreadStatic] private static List<Entry> pending;
        [ThreadStatic] private static long nextToken;
        internal static int Depth => pending?.Count ?? 0;

        internal static long Begin(T target)
        {
            if (pending == null) pending = new List<Entry>(4);
            long token = ++nextToken;
            pending.Add(new Entry { Target = target, Token = token });
            return token;
        }

        internal static bool Contains(T target)
        {
            if (pending != null)
                for (int i = pending.Count - 1; i >= 0; i--)
                    if (ReferenceEquals(pending[i].Target, target)) return true;
            return false;
        }

        internal static void Returned(T target)
        {
            if (pending == null) return;
            // Mark all matching nested calls; a reentrant death must not lose its outer proof.
            for (int i = pending.Count - 1; i >= 0; i--)
                if (ReferenceEquals(pending[i].Target, target))
                { var entry = pending[i]; entry.Returned = true; pending[i] = entry; }
        }

        internal static bool End(T target, long token)
        {
            if (token == 0 || pending == null) return false;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].Token != token || !ReferenceEquals(pending[i].Target, target)) continue;
                // Release even an out-of-order scope, but never act on ambiguous proof.
                bool returned = i == pending.Count - 1 && pending[i].Returned;
                pending.RemoveAt(i);
                return returned;
            }
            return false;
        }
    }
}
