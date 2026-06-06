using System;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs
{
    public class GARunHistoryDto
    {
        public string Timestamp { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectFolderPath { get; set; } = string.Empty;
        public string IfcFileName { get; set; } = string.Empty;
        public string CraneNameOrId { get; set; } = string.Empty;
        public int TotalTargetElements { get; set; }
        public int SelectedElements { get; set; }
        public int LiftedElements { get; set; }
        public int FailedElements { get; set; }
        public string CoverageRatio { get; set; } = string.Empty;
        public string HealthLabel { get; set; } = string.Empty;
        public int FinalCraneSetupCount { get; set; }
        public int GreedyIterations { get; set; }
        public int PopulationSize { get; set; }
        public int ConfiguredGenerations { get; set; }
        public int ActualGenerationLoopCount { get; set; }
        public string TotalGATimeMs { get; set; } = string.Empty;
        public string ElementAssignmentTimeMs { get; set; } = string.Empty;
        public string CraneCapacityLookupCount { get; set; } = string.Empty;
        public string CraneCapacityCacheHits { get; set; } = string.Empty;
        public string CraneCapacityCacheMisses { get; set; } = string.Empty;
        public string CraneCapacityCacheHitRatio { get; set; } = string.Empty;
        public string MemoryBeforeMB { get; set; } = string.Empty;
        public string MemoryAfterMB { get; set; } = string.Empty;
        public string MemoryDeltaMB { get; set; } = string.Empty;
        public string GCGen0Delta { get; set; } = string.Empty;
        public string GCGen1Delta { get; set; } = string.Empty;
        public string GCGen2Delta { get; set; } = string.Empty;
        public string ProfilingLogPath { get; set; } = string.Empty;
        public string DetailedDiagnosticsCsvPath { get; set; } = string.Empty;
        public string FailureBreakdownCsvPath { get; set; } = string.Empty;
    }
}
