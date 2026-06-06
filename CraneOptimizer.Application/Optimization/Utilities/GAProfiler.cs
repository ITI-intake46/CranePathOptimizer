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

        public static string? CurrentRunId { get; set; }

        // Benchmark Metadata
        public static string? ProjectName { get; set; }
        public static string? ProjectFolderPath { get; set; }
        public static string? IfcFileName { get; set; }
        public static string? CraneNameOrId { get; set; }
        public static int TotalTargetElementsCount { get; set; }
        public static int SelectedFilteredElementsCount { get; set; }
        public static int PopulationSize { get; set; }
        public static int ConfiguredGenerationCount { get; set; }
        public static int ActualGenerationLoopCount;
        public static int CandidatePointsGenerated;
        public static int ValidPointsGenerated;
        public static bool StoppedByConvergence;
        public static bool StoppedByMaxGenerations;
        public static int EmptyChromosomeCount;
        public static int GreedySetCoverIterationCount { get; set; }
        public static int FinalCraneSetupCount { get; set; }
        public static int LiftedElementCount { get; set; }
        public static double CoverageRatio { get; set; }
        public static string? ProfilingLogPath { get; set; }

        // Approximate Managed Memory & GC Metrics
        public static long MemoryBeforeBytes { get; set; }
        public static long MemoryAfterBytes { get; set; }
        public static int GcCount0Before { get; set; }
        public static int GcCount1Before { get; set; }
        public static int GcCount2Before { get; set; }
        public static int GcCount0After { get; set; }
        public static int GcCount1After { get; set; }
        public static int GcCount2After { get; set; }

        public class GenerationLogEntry
        {
            public string RunId { get; set; } = "";
            public int StageIndex { get; set; }
            public int GenerationIndex { get; set; }
            public double BestFitness { get; set; }
            public double AverageFitness { get; set; }
            public double BestCoverageRatio { get; set; }
            public double AverageCoverageRatio { get; set; }
            public int BestPositionCount { get; set; }
            public int Count1Position { get; set; }
            public int Count2Positions { get; set; }
            public int Count3Positions { get; set; }
            public double MutationSuccessRate { get; set; }
            public int InvalidMutationCount { get; set; }
            public int StagnationCounter { get; set; }
        }

        public class StageLogEntry
        {
            public string RunId { get; set; } = "";
            public int StageIndex { get; set; }
            public int MaxPositions { get; set; }
            public double RelocationWeight { get; set; }
            public double BestCoverageRatio { get; set; }
            public double BestCoveragePercent { get; set; }
            public int BestLiftedCount { get; set; }
            public double BestFitnessScore { get; set; }
            public int RawPositionCount { get; set; }
            public int FinalPositionCount { get; set; }
            public string UsedZoneIds { get; set; } = "";
            public double RuntimeMs { get; set; }
            public bool Escalated { get; set; }
            public string TerminationReason { get; set; } = "";
        }

        public static readonly System.Collections.Generic.List<GenerationLogEntry> GenerationLogs = new System.Collections.Generic.List<GenerationLogEntry>();
        public static readonly System.Collections.Generic.List<StageLogEntry> StageLogs = new System.Collections.Generic.List<StageLogEntry>();

        private static readonly ConcurrentDictionary<string, long> Accumulators = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentDictionary<string, long> HitCounts = new ConcurrentDictionary<string, long>();

        /// <summary>
        /// Resets all accumulated profiling timings, hit counts, metadata, and memory metrics.
        /// </summary>
        public static void Reset()
        {
            Accumulators.Clear();
            HitCounts.Clear();
            CapacityCacheHits = 0;
            CapacityCacheMisses = 0;
            GenerationLogs.Clear();
            StageLogs.Clear();
            CurrentRunId = null;

            ProjectName = null;
            ProjectFolderPath = null;
            IfcFileName = null;
            CraneNameOrId = null;
            TotalTargetElementsCount = 0;
            SelectedFilteredElementsCount = 0;
            PopulationSize = 0;
            ConfiguredGenerationCount = 0;
            ActualGenerationLoopCount = 0;
            CandidatePointsGenerated = 0;
            ValidPointsGenerated = 0;
            StoppedByConvergence = false;
            StoppedByMaxGenerations = false;
            GreedySetCoverIterationCount = 0;
            FinalCraneSetupCount = 0;
            LiftedElementCount = 0;
            CoverageRatio = 0;
            ProfilingLogPath = null;
            EmptyChromosomeCount = 0;

            MemoryBeforeBytes = 0;
            MemoryAfterBytes = 0;
            GcCount0Before = 0;
            GcCount1Before = 0;
            GcCount2Before = 0;
            GcCount0After = 0;
            GcCount1After = 0;
            GcCount2After = 0;
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
        /// Gets accumulated milliseconds for a specific profiling key.
        /// </summary>
        public static double GetElapsedTimeMs(string key)
        {
            if (Accumulators.TryGetValue(key, out long ticks))
            {
                return (ticks / (double)Stopwatch.Frequency) * 1000.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Formats and logs the cumulative performance profile to Debug output.
        /// </summary>
        public static string GetSummaryReport()
        {
            if (!Enabled) return "GAProfiler is disabled.";

            var sb = new StringBuilder();
            sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                        GA BENCHMARK SCENARIO METADATA                                 ║");
            sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════════════╣");
            sb.AppendLine($"║ Timestamp:            {DateTime.Now:yyyy-MM-dd HH:mm:ss}".PadRight(87) + "║");
            sb.AppendLine($"║ Project Name:         {ProjectName ?? "N/A"}".PadRight(87) + "║");
            sb.AppendLine($"║ Project Folder:       {ProjectFolderPath ?? "N/A"}".PadRight(87) + "║");
            sb.AppendLine($"║ IFC File Name:        {IfcFileName ?? "N/A"}".PadRight(87) + "║");
            sb.AppendLine($"║ Crane Name/ID:        {CraneNameOrId ?? "N/A"}".PadRight(87) + "║");
            sb.AppendLine($"║ Target Elements:      {TotalTargetElementsCount}".PadRight(87) + "║");
            sb.AppendLine($"║ Selected Elements:    {SelectedFilteredElementsCount}".PadRight(87) + "║");
            sb.AppendLine($"║ Population Size:      {PopulationSize}".PadRight(87) + "║");
            sb.AppendLine($"║ Configured Gens:      {ConfiguredGenerationCount}".PadRight(87) + "║");
            sb.AppendLine($"║ Actual Gen Loop Count:{ActualGenerationLoopCount}".PadRight(87) + "║");
            sb.AppendLine($"║ Greedy Iterations:    {GreedySetCoverIterationCount}".PadRight(87) + "║");
            sb.AppendLine($"║ Final Setup Count:    {FinalCraneSetupCount}".PadRight(87) + "║");
            sb.AppendLine($"║ Lifted Element Count: {LiftedElementCount}".PadRight(87) + "║");
            sb.AppendLine($"║ Coverage Ratio:       {(CoverageRatio * 100.0):F2}%".PadRight(87) + "║");
            sb.AppendLine($"║ Profiling Enabled:    {Enabled}".PadRight(87) + "║");

            string logPathStr = $"║ Profiling Log Path:   {ProfilingLogPath ?? "N/A"}";
            if (logPathStr.Length < 87)
                sb.AppendLine(logPathStr.PadRight(87) + "║");
            else
                sb.AppendLine(logPathStr + " ║");

            sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║                APPROXIMATE MANAGED MEMORY METRICS (NO FORCED GC)                      ║");
            sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════════════╣");
            double beforeMb = MemoryBeforeBytes / 1024.0 / 1024.0;
            double afterMb = MemoryAfterBytes / 1024.0 / 1024.0;
            double deltaMb = (MemoryAfterBytes - MemoryBeforeBytes) / 1024.0 / 1024.0;
            sb.AppendLine($"║ Managed Memory Before:  {beforeMb:F3} MB".PadRight(87) + "║");
            sb.AppendLine($"║ Managed Memory After:   {afterMb:F3} MB".PadRight(87) + "║");
            sb.AppendLine($"║ Managed Memory Delta:   {deltaMb:F3} MB".PadRight(87) + "║");
            sb.AppendLine($"║ GC Gen 0 Collections:   {GcCount0Before} -> {GcCount0After} (Delta: {GcCount0After - GcCount0Before})".PadRight(87) + "║");
            sb.AppendLine($"║ GC Gen 1 Collections:   {GcCount1Before} -> {GcCount1After} (Delta: {GcCount1After - GcCount1Before})".PadRight(87) + "║");
            sb.AppendLine($"║ GC Gen 2 Collections:   {GcCount2Before} -> {GcCount2After} (Delta: {GcCount2After - GcCount2Before})".PadRight(87) + "║");
            sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║                               OPTIMIZATION DIAGNOSTICS                                ║");
            sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════════════╣");
            sb.AppendLine($"║ Empty Chromosomes Encountered: {EmptyChromosomeCount}".PadRight(87) + "║");
            sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════════════╣");
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
