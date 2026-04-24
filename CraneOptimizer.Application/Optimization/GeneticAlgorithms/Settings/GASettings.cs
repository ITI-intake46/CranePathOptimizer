namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings
{
    /// <summary>
    /// Configuration parameters of the Genetic Algorithm.
    /// </summary>
    public class GASettings
    {
        /// <summary>
        /// Number of individuals per population.
        /// </summary>
        public int PopulationSize { get; set; }

        /// <summary>
        /// Maximum number of generations.
        /// </summary>
        public int MaxGenerations { get; set; }

        /// <summary>
        /// Probability of mutation.
        /// </summary>
        public double MutationRate { get; set; }

        /// <summary>
        /// Probability of crossover.
        /// </summary>
        public double CrossoverRate { get; set; }

        /// <summary>
        /// Additional rigging length used during lifting calculations.
        /// Default value is typically around 2 meters.
        /// </summary>
        public double RiggingLength { get; set; } = 2.0;

        /// <summary>
        /// Safety factor applied to crane lifting capacity.
        /// </summary>
        public double SafetyFactor { get; set; } = 0.9;

        /// <summary>
        /// Maximum number of crane setup positions allowed in a chromosome.
        /// </summary>
        public int MaxCranePositions { get; set; } = 3;

        /// <summary>
        /// Random seed for deterministic runs (optional).
        /// if null -> fully random behaviour.
        /// </summary>
        public int? RandomSeed { get; set; } = 42;

        // ------------------------------
        // Fitness function weights
        // ------------------------------

        /// <summary>
        /// Weight assigned to element coverage ratio.
        /// Higher value prioritizes lifting more elements.
        /// </summary>
        public double CoverageWeight { get; set; } = 10;

        /// <summary>
        /// Weight applied to crane relocation penalty.
        /// Higher value discourages multiple crane setups.
        /// </summary>
        public double RelocationWeight { get; set; } = 5;

        /// <summary>
        /// Weight applied to large working radius penalty.
        /// </summary>
        public double RadiusPenaltyWeight { get; set; } = 3;

        /// <summary>
        /// Weight applied when elements exceed crane capacity.
        /// </summary>
        public double OverweightPenaltyWeight { get; set; } = 10;

        /// <summary>
        /// Weight applied when required lifting height is invalid.
        /// </summary>
        public double HeightPenaltyWeight { get; set; } = 5;

        /// <summary>
        /// Normalisation factor for working radius penalty.
        /// Converts raw radius values into a [0, 1] scale for consistent fitness evaluation.
        /// </summary>
        public double MaxRadiusForNormalisation { get; set; } = 50;

        /// <summary>
        /// Number of consecutive generations without fitness improvement
        /// </summary>
        public int EarlyStoppingGenerations { get; set; } = 20;

        public double DuplicationPenaltyWeight { get; set; } = 8.0;
    }
}