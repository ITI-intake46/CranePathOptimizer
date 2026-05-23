using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace CraneOptimizer.Application.Optimization.Utilities
{
    /// <summary>
    /// Stateless, centralized thread-safe accumulator profiler for the GA pipeline.
    /// Uses Stopwatch ticks for ultra-high-resolution timing.
    /// </summary>
    public static class GAProfiler
    {
        public static bool Enabled { get; set; } = false;

        public static long CapacityCacheHits;
        public static long CapacityCacheMisses;

        private static readonly ConcurrentDictionary<string, long> Accumulators = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentDictionary<string, long> HitCounts = new ConcurrentDictionary<string, long>();

        /// <summary>
        /// Resets all accumulated profiling timings and hit counts.
        /// </summary>
        public static void Reset()
        {
            Accumulators.Clear();
            HitCounts.Clear();
            CapacityCacheHits = 0;
            CapacityCacheMisses = 0;
        }

        /// <summary>
        /// Accumulates ticks for a specific named pipeline component.
        /// </summary>
        public static void Accumulate(string key, long ticks)
        {
            if (!Enabled) return;

            Accumulators.AddOrUpdate(key, ticks, (_, val) => val + ticks);
            HitCounts.AddOrUpdate(key, 1, (_, val) => val + 1);
        }

        /// <summary>
        /// Formats and logs the cumulative performance profile to Debug output.
        /// </summary>
        public static string GetSummaryReport()
        {
            if (!Enabled) return "GAProfiler is disabled.";

            var sb = new StringBuilder();
            sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                           GA PIPELINE PERFORMANCE PROFILE                             ║");
            sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║ Component                          │ Hit Count  │ Total Time (ms) │ Avg Time (ms)     ║");
            sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════════════╣");

            double tickFrequency = (double)Stopwatch.Frequency;

            foreach (var kvp in Accumulators)
            {
                string key = kvp.Key;
                long ticks = kvp.Value;
                long hits = HitCounts.TryGetValue(key, out long h) ? h : 0;

                double totalMs = (ticks / tickFrequency) * 1000.0;
                double avgMs = hits > 0 ? totalMs / hits : 0;

                sb.AppendLine($"║ {key.PadRight(34)} │ {hits.ToString().PadLeft(10)} │ {totalMs.ToString("F3").PadLeft(15)} │ {avgMs.ToString("F5").PadLeft(17)} ║");
            }

            sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════════════╣");
            long totalLookups = CapacityCacheHits + CapacityCacheMisses;
            double hitRatio = totalLookups > 0 ? (double)CapacityCacheHits / totalLookups * 100.0 : 0.0;
            sb.AppendLine($"║ CraneCapacity Cache Hits: {CapacityCacheHits.ToString().PadRight(10)} │ Misses: {CapacityCacheMisses.ToString().PadRight(10)} │ Hit Ratio: {hitRatio.ToString("F2").PadLeft(6)}%          ║");
            sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════════════╝");
            return sb.ToString();
        }

        /// <summary>
        /// Helper class using IDisposable pattern to automatically measure timings.
        /// </summary>
        public struct ProfileScope : IDisposable
        {
            private readonly string _key;
            private readonly Stopwatch _sw;

            public ProfileScope(string key)
            {
                _key = key;
                _sw = Enabled ? Stopwatch.StartNew() : null!;
            }

            public void Dispose()
            {
                if (Enabled && _sw != null)
                {
                    _sw.Stop();
                    Accumulate(_key, _sw.ElapsedTicks);
                }
            }
        }
    }
}
