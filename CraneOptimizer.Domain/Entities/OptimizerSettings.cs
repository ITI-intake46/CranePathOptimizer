namespace WPF_IFC_test1.Analysis
{
    /// <summary>
    /// All tunable parameters for the crane-position GA optimizer.
    /// Plain-old-data class — safe to clone with <c>with</c> syntax or MemberwiseClone.
    /// </summary>
    public class OptimizerSettings
    {
        // ── GA hyper-parameters ─────────────────────────────────────────────

        /// <summary>Candidate crane positions per generation. Range 10–100. Default 30.</summary>
        public int PopulationSize { get; set; } = 30;

        /// <summary>Maximum GA iterations. Range 5–200. Default 30.</summary>
        public int MaxGenerations { get; set; } = 30;

        /// <summary>Probability [0–1] that a child coordinate is mutated. Default 0.10.</summary>
        public double MutationRate { get; set; } = 0.10;

        /// <summary>Gaussian noise std-deviation in normalised UV space. Default 0.05.</summary>
        public double MutationSigma { get; set; } = 0.05;

        /// <summary>Candidate grid spacing across the allowed zone in metres. Default 2.0 m.</summary>
        public double GridStepM { get; set; } = 2.0;

        /// <summary>Early stop if best score does not improve by more than this % for 5 consecutive
        /// generations. Default 0.5 %.</summary>
        public double ConvergenceThresholdPct { get; set; } = 0.5;

        // ── Penalty weights ─────────────────────────────────────────────────

        /// <summary>Score added per Non-Liftable element. Default 20.</summary>
        public int PenaltyNonLiftable { get; set; } = 20;

        /// <summary>Score added per Unreachable element. Default 100.</summary>
        public int PenaltyUnreachable { get; set; } = 100;

        // ── Lift check settings ─────────────────────────────────────────────

        /// <summary>Minimum clearance in metres the hook must have above the element CG. Default 0.5 m.</summary>
        public double MinClearanceM { get; set; } = 0.5;

        // ── Derived ─────────────────────────────────────────────────────────

        /// <summary>Maximum possible score: penaltyUnreachable × totalElementCount.
        /// Used as the penalty for positions that violate zone constraints.</summary>
        public int MaxPenalty(int totalElements) => PenaltyUnreachable * totalElements;
    }
}
