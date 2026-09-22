using System;

namespace ExpandedHordes
{
    // Upper-bound quantization, 0.25 ms through 512 ms. All frames are covered.
    internal sealed class FrameWindow
    {
        internal const int BinCount = 2048;
        private readonly long[] bins = new long[BinCount];
        internal long Count { get; private set; }
        internal long SampleCount => Count;
        internal long Overflow { get; private set; }
        internal double Total { get; private set; }
        internal double Maximum { get; private set; }
        internal double Mean => Count == 0 ? 0 : Total / Count;
        internal void Add(double milliseconds)
        {
            if (!(milliseconds > 0) || double.IsInfinity(milliseconds)) return;
            Count++; Total += milliseconds; Maximum = Math.Max(Maximum, milliseconds);
            if (milliseconds > 512) Overflow++;
            else bins[Math.Max(0, (int)Math.Ceiling(milliseconds * 4) - 1)]++;
        }
        internal double Percentile95()
        {
            return Percentile(0.95);
        }
        internal double Percentile(double fraction)
        {
            if (Count == 0) return 0;
            long target = (long)Math.Ceiling(Count * fraction), seen = 0;
            for (int i = 0; i < bins.Length; i++)
                if ((seen += bins[i]) >= target) return (i + 1) * 0.25;
            return double.PositiveInfinity;
        }
        internal void Merge(FrameWindow other)
        {
            Count += other.Count; Total += other.Total; Overflow += other.Overflow;
            Maximum = Math.Max(Maximum, other.Maximum);
            for (int i = 0; i < bins.Length; i++) bins[i] += other.bins[i];
        }
        internal void Reset() { Count = Overflow = 0; Total = Maximum = 0; Array.Clear(bins, 0, bins.Length); }
    }

    internal static class PerformanceRules
    {
        internal static string Hint(double cpu, double gpu, double wait, double frame)
        {
            if (!(cpu > 0) || !(gpu > 0) || double.IsInfinity(cpu) || double.IsInfinity(gpu)) return "unavailable";
            if (wait > 0.5 && cpu < frame * 0.8 && gpu < frame * 0.8) return "presentation_limit_suspected";
            if (cpu > gpu * 1.2) return "CPU_heavier";
            if (gpu > cpu * 1.2) return "GPU_heavier";
            return "similar_CPU_GPU";
        }
    }
}
