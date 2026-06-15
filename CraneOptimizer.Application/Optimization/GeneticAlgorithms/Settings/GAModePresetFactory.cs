namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings
{
    /// <summary>
    /// User-facing GA optimization mode presets (Candidate V1).
    /// Calibrated on the <c>auditmodel</c> project using STC800 and STC1300C cranes
    /// across 54 clean runs (9 parameter sets × 3 seeds × 2 cranes).
    /// See <c>docs/ga/ga-mode-presets-v1.md</c> for the full calibration trail.
    /// </summary>
    public enum GAModePreset
    {
        /// <summary>
        /// No preset applied. The GA runs with whatever values are currently set
        /// in the UI text boxes. Preserves full manual-tuning workflow.
        /// </summary>
        None,

        /// <summary>
        /// Balanced Coverage (B02).
        /// Pop=60, Gen=80, MaxPositions=2.
        /// Optimises for maximum element coverage with 2 crane setup positions.
        /// Cross-crane: STC800 ~64.4%, STC1300C ~63.4%.
        /// </summary>
        BalancedCoverage,

        /// <summary>
        /// Minimum Relocation (R01).
        /// Pop=30, Gen=40, MaxPositions=1.
        /// Restricts the GA to a single crane setup position, minimising site
        /// mobilisation. Coverage is reduced (~53–54%) relative to Balanced.
        /// </summary>
        MinimumRelocation,

        /// <summary>
        /// Crane-Friendly (C02).
        /// Pop=60, Gen=80, MaxPositions=3.
        /// Allows up to 3 setup positions so the GA can find shorter working radii
        /// per position, reducing peak crane stress per lift.
        /// Coverage: STC800 ~70.3%, STC1300C ~62.4%.
        /// </summary>
        CraneFriendly
    }

    /// <summary>
    /// Applies a calibrated <see cref="GAModePreset"/> on top of a base
    /// <see cref="GASettings"/> instance.
    /// Physical, safety, crane-geometry, and project-metadata fields are always
    /// preserved from the base settings — only the GA tuning parameters are overridden.
    /// </summary>
    /// <remarks>
    /// All preset values are sourced directly from
    /// <c>ga_audit_settings_snapshot.csv</c> produced by the Phase 5 calibration
    /// sweep (mode_calibration_20260615_175130 / mode_calibration_20260615_181020).
    /// The three winning presets (B02, R01, C02) share identical values for every
    /// GA tuning parameter except PopulationSize, MaxGenerations, and MaxCranePositions.
    /// </remarks>
    public static class GAModePresetFactory
    {
        // -----------------------------------------------------------------------
        // Shared calibrated values — identical across B02, R01, and C02 in CSV
        // -----------------------------------------------------------------------
        private const double SharedMutationRate                  = 0.10;
        private const double SharedMutationSigma                 = 0.005;   // CSV: 0.0050 — differs from UI default 0.05
        private const double SharedCrossoverRate                 = 0.80;
        private const int    SharedEarlyStoppingGenerations      = 20;
        private const double SharedConvergenceThresholdPercent   = 0.0;
        private const int    SharedPositionSamplingAttempts      = 80;
        private const bool   SharedEnableAdaptiveSigma           = false;
        private const double SharedSigmaCoolingExponent          = 1.5;
        private const double SharedMinimumMutationSigma          = 0.5;
        private const double SharedMutationExplorationMultiplier = 2.0;
        private const bool   SharedEnableConcurrentOptimization  = true;
        private const bool   SharedEnableReachabilitySeeding     = true;
        private const double SharedHotSpotSeedingRatio           = 0.30;
        private const bool   SharedEnableMultiStageDiscovery     = true;
        private const double SharedMultiStageCoverageTarget      = 0.95;
        private const double SharedAdaptiveRelocationScaleFactor = 0.40;
        private const bool   SharedUseHungarianAssignment        = true;    // CSV: true — differs from UI default false

        // Fitness weights — all identical across B02/R01/C02 in CSV
        private const double SharedCoverageWeight                 = 10.0;
        private const double SharedCoverageDominanceMultiplier    = 100.0;
        private const double SharedRelocationWeight               = 5.0;
        private const double SharedRadiusPenaltyWeight            = 3.0;
        private const double SharedRadiusQualityPenaltyWeight     = 1.0;
        private const double SharedOverweightPenaltyWeight        = 10.0;
        private const double SharedHeightPenaltyWeight            = 5.0;
        private const double SharedRadiusStdDevPenaltyWeight      = 2.0;
        private const double SharedBalancePenaltyWeight           = 2.0;
        private const double SharedSimilarPositionPenaltyWeight   = 8.0;
        private const double SharedRelocationDistancePenaltyWeight = 1.5;

        // -----------------------------------------------------------------------
        // Per-mode values — the only 3 fields that differ between B02/R01/C02
        // -----------------------------------------------------------------------
        // B02 — Balanced Coverage
        private const int B02_PopulationSize    = 60;
        private const int B02_MaxGenerations    = 80;
        private const int B02_MaxCranePositions = 2;

        // R01 — Minimum Relocation
        private const int R01_PopulationSize    = 30;
        private const int R01_MaxGenerations    = 40;
        private const int R01_MaxCranePositions = 1;

        // C02 — Crane-Friendly
        private const int C02_PopulationSize    = 60;
        private const int C02_MaxGenerations    = 80;
        private const int C02_MaxCranePositions = 3;

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Applies the specified <paramref name="mode"/> to a clone of
        /// <paramref name="baseSettings"/> and returns the result.
        /// The <paramref name="baseSettings"/> object is never mutated.
        /// When <paramref name="mode"/> is <see cref="GAModePreset.None"/>,
        /// an unmodified clone of <paramref name="baseSettings"/> is returned.
        /// </summary>
        /// <param name="mode">The preset to apply.</param>
        /// <param name="baseSettings">
        /// The base settings to clone. Physical, safety, crane-geometry, and
        /// project-metadata fields are always preserved from this instance.
        /// </param>
        public static GASettings Apply(GAModePreset mode, GASettings baseSettings)
        {
            var settings = baseSettings.Clone();

            if (mode == GAModePreset.None)
                return settings;

            // Apply shared calibrated tuning parameters (identical for all modes)
            ApplySharedTuning(settings);

            // Apply the three per-mode differentiating parameters
            switch (mode)
            {
                case GAModePreset.BalancedCoverage:
                    settings.PopulationSize    = B02_PopulationSize;
                    settings.MaxGenerations    = B02_MaxGenerations;
                    settings.MaxCranePositions = B02_MaxCranePositions;
                    break;

                case GAModePreset.MinimumRelocation:
                    settings.PopulationSize    = R01_PopulationSize;
                    settings.MaxGenerations    = R01_MaxGenerations;
                    settings.MaxCranePositions = R01_MaxCranePositions;
                    break;

                case GAModePreset.CraneFriendly:
                    settings.PopulationSize    = C02_PopulationSize;
                    settings.MaxGenerations    = C02_MaxGenerations;
                    settings.MaxCranePositions = C02_MaxCranePositions;
                    break;
            }

            return settings;
        }

        /// <summary>
        /// Returns a user-friendly display name for the given preset,
        /// suitable for ComboBox display text.
        /// </summary>
        public static string GetDisplayName(GAModePreset mode) => mode switch
        {
            GAModePreset.None              => "None / Custom",
            GAModePreset.BalancedCoverage  => "Balanced Coverage",
            GAModePreset.MinimumRelocation => "Minimum Relocation",
            GAModePreset.CraneFriendly     => "Crane-Friendly",
            _                              => mode.ToString()
        };

        /// <summary>
        /// Returns a short description for the given preset,
        /// suitable for display below the ComboBox.
        /// </summary>
        public static string GetDescription(GAModePreset mode) => mode switch
        {
            GAModePreset.None =>
                "Use the values currently shown in Advanced Options.",
            GAModePreset.BalancedCoverage =>
                "Balanced optimization for strong coverage with practical setup count.",
            GAModePreset.MinimumRelocation =>
                "Prioritizes using the fewest crane setup positions.",
            GAModePreset.CraneFriendly =>
                "Allows more setup flexibility to reduce working radius and crane stress.",
            _ => string.Empty
        };

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private static void ApplySharedTuning(GASettings s)
        {
            s.MutationRate                   = SharedMutationRate;
            s.MutationSigma                  = SharedMutationSigma;
            s.CrossoverRate                  = SharedCrossoverRate;
            s.EarlyStoppingGenerations       = SharedEarlyStoppingGenerations;
            s.ConvergenceThresholdPercent    = SharedConvergenceThresholdPercent;
            s.PositionSamplingAttempts       = SharedPositionSamplingAttempts;
            s.EnableAdaptiveSigma            = SharedEnableAdaptiveSigma;
            s.SigmaCoolingExponent           = SharedSigmaCoolingExponent;
            s.MinimumMutationSigma           = SharedMinimumMutationSigma;
            s.MutationExplorationMultiplier  = SharedMutationExplorationMultiplier;
            s.EnableConcurrentOptimization   = SharedEnableConcurrentOptimization;
            s.EnableReachabilitySeeding      = SharedEnableReachabilitySeeding;
            s.HotSpotSeedingRatio            = SharedHotSpotSeedingRatio;
            s.EnableMultiStageDiscovery      = SharedEnableMultiStageDiscovery;
            s.MultiStageCoverageTarget       = SharedMultiStageCoverageTarget;
            s.AdaptiveRelocationScaleFactor  = SharedAdaptiveRelocationScaleFactor;
            s.UseHungarianAssignment         = SharedUseHungarianAssignment;
            s.CoverageWeight                 = SharedCoverageWeight;
            s.CoverageDominanceMultiplier    = SharedCoverageDominanceMultiplier;
            s.RelocationWeight               = SharedRelocationWeight;
            s.RadiusPenaltyWeight            = SharedRadiusPenaltyWeight;
            s.RadiusQualityPenaltyWeight     = SharedRadiusQualityPenaltyWeight;
            s.OverweightPenaltyWeight        = SharedOverweightPenaltyWeight;
            s.HeightPenaltyWeight            = SharedHeightPenaltyWeight;
            s.RadiusStdDevPenaltyWeight      = SharedRadiusStdDevPenaltyWeight;
            s.BalancePenaltyWeight           = SharedBalancePenaltyWeight;
            s.SimilarPositionPenaltyWeight   = SharedSimilarPositionPenaltyWeight;
            s.RelocationDistancePenaltyWeight = SharedRelocationDistancePenaltyWeight;
        }
    }
}
