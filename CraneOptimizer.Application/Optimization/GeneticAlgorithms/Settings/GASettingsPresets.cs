namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings
{
    /// <summary>
    /// Static utility factory for standard GA optimization presets.
    /// </summary>
    public static class GASettingsPresets
    {
        public static GASettings CoverageMaximizerMode() => new()
        {
            EnableMultiStageDiscovery = true,
            MultiStageCoverageTarget = 0.90,
            PopulationSize = 100,
            MaxGenerations = 200,
            EarlyStoppingGenerations = 20,
            CoverageWeight = 10.0,
            RelocationWeight = 5.0
        };

        public static GASettings MinRelocationMode() => new()
        {
            EnableMultiStageDiscovery = false,
            MaxCranePositions = 1,
            PopulationSize = 100,
            MaxGenerations = 200,
            EarlyStoppingGenerations = 20,
            CoverageWeight = 10.0,
            RelocationWeight = 0.0 // no escalation or penalty needed
        };

        public static GASettings HighEfficiencyMode() => new()
        {
            EnableMultiStageDiscovery = false,
            PopulationSize = 50,
            MaxGenerations = 100,
            EarlyStoppingGenerations = 10,
            CoverageWeight = 10.0,
            RelocationWeight = 5.0
        };

        public static GASettings BalancedLiftingMode() => new()
        {
            EnableMultiStageDiscovery = true,
            MultiStageCoverageTarget = 0.85,
            PopulationSize = 100,
            MaxGenerations = 200,
            EarlyStoppingGenerations = 20,
            CoverageWeight = 10.0,
            RelocationWeight = 5.0
        };

        public static GASettings WeightBalancedMode() => new()
        {
            EnableMultiStageDiscovery = true,
            MultiStageCoverageTarget = 0.85,
            PopulationSize = 100,
            MaxGenerations = 200,
            EarlyStoppingGenerations = 20,
            CoverageWeight = 10.0,
            RelocationWeight = 5.0
        };
    }
}
