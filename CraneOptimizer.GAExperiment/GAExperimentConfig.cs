using System.Collections.Generic;

namespace CraneOptimizer.GAExperiment
{
    public class GAExperimentConfig
    {
        public string CatalogDatabasePath { get; set; } = string.Empty;
        public List<string> ProjectFiles { get; set; } = new();
        public List<string> Cranes { get; set; } = new();
        public string ExperimentResultsCsvPath { get; set; } = string.Empty;
        public string DiagnosticsDirectoryPath { get; set; } = string.Empty;
        public string BenchmarkVersion { get; set; } = "Benchmark_v1";
        public string SeedMode { get; set; } = "Fixed"; // "Fixed", "Random"
        public List<int> Seeds { get; set; } = new();
        public int RandomSeedCount { get; set; } = 5;
        public int MaxRuns { get; set; } = 10000;
        public bool ResumeEnabled { get; set; } = true;
        public bool UseHungarianAssignment { get; set; }
        public bool EnableReachabilitySeeding { get; set; }
        public MatrixSettings Matrix { get; set; } = new();
    }

    public class MatrixSettings
    {
        public List<int> PopulationSize { get; set; } = new();
        public List<int> MaxGenerations { get; set; } = new();
        public List<double> MutationRate { get; set; } = new();
        public List<double> CrossoverRate { get; set; } = new();
        public List<int> MaxCranePositions { get; set; } = new();
        public List<double> MinimumRelocationDistance { get; set; } = new();
        public List<double> MutationSigma { get; set; } = new();
        public List<double> GridStep { get; set; } = new();
    }
}
