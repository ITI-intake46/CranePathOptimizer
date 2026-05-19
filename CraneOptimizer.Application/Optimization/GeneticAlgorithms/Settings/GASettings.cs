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
        /// Mutation spread. Values <= 1 are treated as a ratio of MaxRadiusForNormalisation.
        /// </summary>
        public double MutationSigma { get; set; } = 0.05;

        /// <summary>
        /// Probability of crossover.
        /// </summary>
        public double CrossoverRate { get; set; } = 0.8;

        /// <summary>
        /// Additional wire/hoist rope length (m) that increases required hook height.
        /// </summary>
        /// <remarks>Unit: Meters (SI length)</remarks>
        public double WireLength { get; set; } = 2.0;

        /// <summary>
        /// Additional lifting weight (tons) for hook block and accessories.
        /// Added to element weight before capacity validation.
        /// </summary>
        /// <remarks>Unit: Metric Tons (SI mass)</remarks>
        public double AdditionalWt { get; set; } = 0.0;

        /// <summary>
        /// Capacity derating ratio applied to load-chart capacities.
        /// Use values in (0, 1]; lower values reduce effective crane capacity.
        /// </summary>
        public double DeratingRatio { get; set; } = 0.9;

        /// <summary>
        /// User-defined operational safety limit applied after derating.
        /// Use values in [0, 1]; lower values are more conservative.
        /// </summary>
        public double SafetyFactor { get; set; } = 0.95;

        /// <summary>
        /// Backward-compatible alias for legacy configuration payloads.
        /// Prefer WireLength.
        /// </summary>
        [Obsolete("Use WireLength instead.")]
        public double RiggingLength
        {
            get => WireLength;
            set => WireLength = value;
        }

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
        /// Weight applied to the out-of-radius element count penalty.
        /// Higher values more aggressively reject positions that leave many
        /// elements unreachable.
        /// </summary>
        public double RadiusPenaltyWeight { get; set; } = 3;

        /// <summary>
        /// Weight applied to the average working-radius quality penalty.
        /// Independently controls how much the fitness function discourages
        /// solutions that use unnecessarily large working radii.
        /// Kept separate from RadiusPenaltyWeight to avoid double-penalising
        /// the same dimension with the same coefficient.
        /// </summary>
        public double RadiusQualityPenaltyWeight { get; set; } = 1.0;

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
        /// <remarks>Unit: Meters (SI length)</remarks>
        public double MaxRadiusForNormalisation { get; set; } = 50;

        /// <summary>
        /// Optional grid snapping step in meters for seeded and mutated crane positions.
        /// Set to 0 to allow free placement.
        /// </summary>
        public double GridStep { get; set; } = 0;

        /// <summary>
        /// Number of consecutive generations without fitness improvement
        /// </summary>
        public int EarlyStoppingGenerations { get; set; } = 20;

        /// <summary>
        /// Minimum relative fitness improvement percentage required to count as convergence progress.
        /// 0 disables this threshold.
        /// </summary>
        public double ConvergenceThresholdPercent { get; set; } = 0;

        /// <summary>
        /// Weight applied to variation in lift radii among liftable elements.
        /// Higher values prefer more consistent working radii.
        /// </summary>
        public double RadiusStdDevPenaltyWeight { get; set; } = 2.0;

        /// <summary>
        /// Weight applied to uneven element distribution between crane positions.
        /// Higher values prefer balanced crane-position utilization.
        /// </summary>
        public double BalancePenaltyWeight { get; set; } = 2.0;

        /// <summary>
        /// Minimum meaningful distance in meters between crane setup points.
        /// Points closer than this are treated as setup adjustments, not real relocations.
        /// </summary>
        public double MinimumRelocationDistance { get; set; } = 10.0;

        /// <summary>
        /// Distance in meters under which two points are considered near-duplicates.
        /// This threshold supports de-duplication before final results are shown.
        /// </summary>
        public double DuplicatePositionDistance { get; set; } = 3.0;

        /// <summary>
        /// Penalty weight for crane positions that are too close to be operationally distinct.
        /// </summary>
        public double SimilarPositionPenaltyWeight { get; set; } = 8.0;

        /// <summary>
        /// Penalty weight for total movement distance between sequential crane setup points.
        /// This models travel/mobilization effort in addition to setup count.
        /// </summary>
        public double RelocationDistancePenaltyWeight { get; set; } = 1.5;

        /// <summary>
        /// Multiplier applied to mutation scale to avoid overly local search.
        /// Higher values increase spatial exploration while preserving the existing mutation model.
        /// </summary>
        public double MutationExplorationMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Number of attempts used when sampling valid and spatially separated setup points.
        /// </summary>
        public int PositionSamplingAttempts { get; set; } = 80;

        /// <summary>
        /// Approximate crane footprint radius in meters.
        /// This is set by the GA input builder from crane body/outrigger dimensions.
        /// </summary>
        public double CraneFootprintRadius { get; set; }

        /// <summary>
        /// Optional element type allow-list. When populated, only matching IFC element
        /// types are included in the optimization target set.
        /// </summary>
        public IReadOnlyCollection<string>? TargetElementTypes { get; set; }
    }
}
