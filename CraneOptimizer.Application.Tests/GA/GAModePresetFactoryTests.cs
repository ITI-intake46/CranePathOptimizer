using System.Collections.Generic;
using System.Linq;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Workflows;
using Xunit;

namespace CraneOptimizer.Application.Tests.GA
{
    /// <summary>
    /// Verifies that <see cref="GAModePresetFactory"/> produces settings that
    /// exactly match the values recorded in ga_audit_settings_snapshot.csv from
    /// the Phase 5 calibration sweep (STC800 + STC1300C, 54 clean runs).
    /// </summary>
    public class GAModePresetFactoryTests
    {
        // -------------------------------------------------------------------
        // Helper: a representative base settings with non-zero physical fields
        // to verify those are never changed by any preset.
        // -------------------------------------------------------------------
        private static GASettings MakeBaseSettings() => new GASettings
        {
            // Physical / safety — must always be preserved
            WireLength                        = 3.5,
            AdditionalWt                      = 0.75,
            DeratingRatio                     = 0.85,
            SafetyFactor                      = 0.92,
            GridStep                          = 2.0,
            MinimumRelocationDistance         = 15.0,
            DuplicatePositionDistance         = 4.0,
            MaxRadiusForNormalisation         = 60.0,
            RandomSeed                        = 99,
            CraneFootprintRadius              = 5.0,
            SlewOffsetX                       = 1.1,
            SlewOffsetY                       = 2.2,
            SlewOffsetZ                       = 0.3,
            BoomHingeOffsetX                  = 0.5,
            BoomHingeOffsetY                  = 0.6,
            BoomHingeOffsetZ                  = 0.7,
            LoadChartNumberOfLinesConstraint  = 4,
            LoadChartOperatingConditionConstraint = "SomeCondition",
            TargetElementTypes               = new List<string> { "IfcBeam", "IfcColumn" },
            // Diagnostics / metadata — must be preserved
            EnableProfiling                   = true,
            DiagnosticsLevel                  = DiagnosticsLevel.Research,
            BatchModeEnabled                  = false,
            ExperimentBatchId                 = "test-batch",
            ParameterSetId                    = "MANUAL",
            OptimizationRunId                 = "run-001",
            // GA tuning — these will be overridden by the preset
            PopulationSize                    = 10,
            MaxGenerations                    = 10,
            MutationSigma                     = 0.99,
            MutationRate                      = 0.99,
            MaxCranePositions                 = 9,
            UseHungarianAssignment            = false
        };

        // -------------------------------------------------------------------
        // 1. None preset: returns a clone with no tuning changes
        // -------------------------------------------------------------------
        [Fact]
        public void Apply_None_ReturnsCopyWithUnchangedGATuning()
        {
            var baseSettings = MakeBaseSettings();
            var result = GAModePresetFactory.Apply(GAModePreset.None, baseSettings);

            // GA tuning fields should remain unchanged
            Assert.Equal(baseSettings.PopulationSize, result.PopulationSize);
            Assert.Equal(baseSettings.MaxGenerations, result.MaxGenerations);
            Assert.Equal(baseSettings.MaxCranePositions, result.MaxCranePositions);
            Assert.Equal(baseSettings.MutationSigma, result.MutationSigma);
            Assert.Equal(baseSettings.UseHungarianAssignment, result.UseHungarianAssignment);
        }

        // -------------------------------------------------------------------
        // 2. Balanced Coverage (B02) — from CSV
        // -------------------------------------------------------------------
        [Fact]
        public void Apply_BalancedCoverage_SetsExpectedCSVValues()
        {
            var result = GAModePresetFactory.Apply(GAModePreset.BalancedCoverage, MakeBaseSettings());

            // Per-mode fields (only 3 differ between presets)
            Assert.Equal(60, result.PopulationSize);
            Assert.Equal(80, result.MaxGenerations);
            Assert.Equal(2,  result.MaxCranePositions);

            // Shared fields that differ from UI defaults — must also be set
            Assert.Equal(0.005, result.MutationSigma);
            Assert.Equal(0.10,  result.MutationRate);
            Assert.Equal(0.80,  result.CrossoverRate);
            Assert.True(result.UseHungarianAssignment);
            Assert.True(result.EnableMultiStageDiscovery);

            // Shared penalty weights — all at calibrated values
            Assert.Equal(5.0,  result.RelocationWeight);
            Assert.Equal(3.0,  result.RadiusPenaltyWeight);
            Assert.Equal(10.0, result.OverweightPenaltyWeight);
            Assert.Equal(5.0,  result.HeightPenaltyWeight);
            Assert.Equal(8.0,  result.SimilarPositionPenaltyWeight);
            Assert.Equal(1.5,  result.RelocationDistancePenaltyWeight);
            Assert.Equal(2.0,  result.RadiusStdDevPenaltyWeight);
            Assert.Equal(2.0,  result.BalancePenaltyWeight);
        }

        // -------------------------------------------------------------------
        // 3. Minimum Relocation (R01) — from CSV
        // -------------------------------------------------------------------
        [Fact]
        public void Apply_MinimumRelocation_SetsExpectedCSVValues()
        {
            var result = GAModePresetFactory.Apply(GAModePreset.MinimumRelocation, MakeBaseSettings());

            // Per-mode fields
            Assert.Equal(30, result.PopulationSize);
            Assert.Equal(40, result.MaxGenerations);
            Assert.Equal(1,  result.MaxCranePositions);

            // Shared fields that differ from UI defaults
            Assert.Equal(0.005, result.MutationSigma);
            Assert.Equal(0.10,  result.MutationRate);
            Assert.True(result.UseHungarianAssignment);
            Assert.True(result.EnableMultiStageDiscovery);

            // CRITICAL: RelocationWeight = 5.0, NOT 20 or 25
            // Minimum relocation is achieved via MaxCranePositions=1, not RelocationWeight.
            Assert.Equal(5.0, result.RelocationWeight);

            // Penalty weights identical to B02
            Assert.Equal(3.0,  result.RadiusPenaltyWeight);
            Assert.Equal(10.0, result.OverweightPenaltyWeight);
            Assert.Equal(5.0,  result.HeightPenaltyWeight);
        }

        // -------------------------------------------------------------------
        // 4. Crane-Friendly (C02) — from CSV
        // -------------------------------------------------------------------
        [Fact]
        public void Apply_CraneFriendly_SetsExpectedCSVValues()
        {
            var result = GAModePresetFactory.Apply(GAModePreset.CraneFriendly, MakeBaseSettings());

            // Per-mode fields
            Assert.Equal(60, result.PopulationSize);
            Assert.Equal(80, result.MaxGenerations);
            Assert.Equal(3,  result.MaxCranePositions);   // 3 positions = shorter radius per lift

            // Shared fields
            Assert.Equal(0.005, result.MutationSigma);
            Assert.Equal(0.10,  result.MutationRate);
            Assert.True(result.UseHungarianAssignment);
            Assert.True(result.EnableMultiStageDiscovery);

            // CRITICAL: Penalty weights are IDENTICAL to B02/R01 — not reduced.
            // Crane-friendly behavior comes from allowing up to 3 setup positions.
            Assert.Equal(5.0,  result.RelocationWeight);
            Assert.Equal(3.0,  result.RadiusPenaltyWeight);
            Assert.Equal(10.0, result.OverweightPenaltyWeight);
            Assert.Equal(5.0,  result.HeightPenaltyWeight);
            Assert.Equal(8.0,  result.SimilarPositionPenaltyWeight);
            Assert.Equal(1.5,  result.RelocationDistancePenaltyWeight);
            Assert.Equal(2.0,  result.RadiusStdDevPenaltyWeight);
            Assert.Equal(2.0,  result.BalancePenaltyWeight);
        }

        // -------------------------------------------------------------------
        // 5. Physical / safety / metadata fields are preserved across all modes
        // -------------------------------------------------------------------
        [Theory]
        [InlineData(GAModePreset.None)]
        [InlineData(GAModePreset.BalancedCoverage)]
        [InlineData(GAModePreset.MinimumRelocation)]
        [InlineData(GAModePreset.CraneFriendly)]
        public void Apply_AllModes_PreservePhysicalAndSafetyFields(GAModePreset mode)
        {
            var baseSettings = MakeBaseSettings();
            var result = GAModePresetFactory.Apply(mode, baseSettings);

            Assert.Equal(3.5,  result.WireLength);
            Assert.Equal(0.75, result.AdditionalWt);
            Assert.Equal(0.85, result.DeratingRatio);
            Assert.Equal(0.92, result.SafetyFactor);
            Assert.Equal(2.0,  result.GridStep);
            Assert.Equal(15.0, result.MinimumRelocationDistance);
            Assert.Equal(4.0,  result.DuplicatePositionDistance);
            Assert.Equal(60.0, result.MaxRadiusForNormalisation);
            Assert.Equal(99,   result.RandomSeed);
            Assert.Equal(5.0,  result.CraneFootprintRadius);
            Assert.Equal(1.1,  result.SlewOffsetX);
            Assert.Equal(2.2,  result.SlewOffsetY);
            Assert.Equal(0.3,  result.SlewOffsetZ);
            Assert.Equal(0.5,  result.BoomHingeOffsetX);
            Assert.Equal(0.6,  result.BoomHingeOffsetY);
            Assert.Equal(0.7,  result.BoomHingeOffsetZ);
            Assert.Equal(4,    result.LoadChartNumberOfLinesConstraint);
            Assert.Equal("SomeCondition", result.LoadChartOperatingConditionConstraint);
            Assert.Equal("test-batch",    result.ExperimentBatchId);
            Assert.Equal("MANUAL",        result.ParameterSetId);
            Assert.Equal("run-001",       result.OptimizationRunId);
        }

        // -------------------------------------------------------------------
        // 6. Apply does not mutate the base settings object
        // -------------------------------------------------------------------
        [Theory]
        [InlineData(GAModePreset.BalancedCoverage)]
        [InlineData(GAModePreset.MinimumRelocation)]
        [InlineData(GAModePreset.CraneFriendly)]
        public void Apply_DoesNotMutateBaseSettings(GAModePreset mode)
        {
            var baseSettings = MakeBaseSettings();
            int originalPop      = baseSettings.PopulationSize;
            int originalGen      = baseSettings.MaxGenerations;
            int originalPos      = baseSettings.MaxCranePositions;
            double originalSigma = baseSettings.MutationSigma;
            bool originalHung    = baseSettings.UseHungarianAssignment;

            _ = GAModePresetFactory.Apply(mode, baseSettings);

            Assert.Equal(originalPop,   baseSettings.PopulationSize);
            Assert.Equal(originalGen,   baseSettings.MaxGenerations);
            Assert.Equal(originalPos,   baseSettings.MaxCranePositions);
            Assert.Equal(originalSigma, baseSettings.MutationSigma);
            Assert.Equal(originalHung,  baseSettings.UseHungarianAssignment);
        }

        // -------------------------------------------------------------------
        // 7. All three active presets share calibrated shared tuning fields
        // -------------------------------------------------------------------
        [Fact]
        public void Apply_AllActivePresets_HaveIdenticalSharedTuningFields()
        {
            var balanced  = GAModePresetFactory.Apply(GAModePreset.BalancedCoverage,  MakeBaseSettings());
            var minReloc  = GAModePresetFactory.Apply(GAModePreset.MinimumRelocation,  MakeBaseSettings());
            var friendly  = GAModePresetFactory.Apply(GAModePreset.CraneFriendly,      MakeBaseSettings());

            // All shared tuning fields must be identical
            Assert.Equal(balanced.MutationRate,                   minReloc.MutationRate);
            Assert.Equal(balanced.MutationRate,                   friendly.MutationRate);

            Assert.Equal(balanced.MutationSigma,                  minReloc.MutationSigma);
            Assert.Equal(balanced.MutationSigma,                  friendly.MutationSigma);

            Assert.Equal(balanced.CrossoverRate,                   minReloc.CrossoverRate);
            Assert.Equal(balanced.CrossoverRate,                   friendly.CrossoverRate);

            Assert.Equal(balanced.UseHungarianAssignment,         minReloc.UseHungarianAssignment);
            Assert.Equal(balanced.UseHungarianAssignment,         friendly.UseHungarianAssignment);

            Assert.Equal(balanced.EnableMultiStageDiscovery,      minReloc.EnableMultiStageDiscovery);
            Assert.Equal(balanced.EnableMultiStageDiscovery,      friendly.EnableMultiStageDiscovery);

            Assert.Equal(balanced.RelocationWeight,               minReloc.RelocationWeight);
            Assert.Equal(balanced.RelocationWeight,               friendly.RelocationWeight);

            Assert.Equal(balanced.RadiusPenaltyWeight,            minReloc.RadiusPenaltyWeight);
            Assert.Equal(balanced.RadiusPenaltyWeight,            friendly.RadiusPenaltyWeight);

            Assert.Equal(balanced.OverweightPenaltyWeight,        minReloc.OverweightPenaltyWeight);
            Assert.Equal(balanced.OverweightPenaltyWeight,        friendly.OverweightPenaltyWeight);

            Assert.Equal(balanced.HeightPenaltyWeight,            minReloc.HeightPenaltyWeight);
            Assert.Equal(balanced.HeightPenaltyWeight,            friendly.HeightPenaltyWeight);

            Assert.Equal(balanced.SimilarPositionPenaltyWeight,   minReloc.SimilarPositionPenaltyWeight);
            Assert.Equal(balanced.SimilarPositionPenaltyWeight,   friendly.SimilarPositionPenaltyWeight);

            Assert.Equal(balanced.RelocationDistancePenaltyWeight, minReloc.RelocationDistancePenaltyWeight);
            Assert.Equal(balanced.RelocationDistancePenaltyWeight, friendly.RelocationDistancePenaltyWeight);
        }

        // -------------------------------------------------------------------
        // 8. ViewModel Selection Updates Fields immediately
        // -------------------------------------------------------------------
        [Fact]
        public void ViewModel_SelectingBalancedCoverage_UpdatesGAFields()
        {
            var vm = new CraneOptimizerDialogViewModel();
            vm.SelectedGaModePresetOption = CraneOptimizerDialogViewModel.GaModePresetOptions.First(o => o.Mode == GAModePreset.BalancedCoverage);

            Assert.Equal("60", vm.PopulationSize);
            Assert.Equal("80", vm.MaxGenerations);
            Assert.Equal("2", vm.MaxCranePositions);
            Assert.Equal("0.005", vm.MutationSigma);
            Assert.True(vm.UseHungarianAssignment);
            Assert.True(vm.EnableMultiStageDiscovery);
        }

        [Fact]
        public void ViewModel_SelectingMinimumRelocation_UpdatesGAFields()
        {
            var vm = new CraneOptimizerDialogViewModel();
            vm.SelectedGaModePresetOption = CraneOptimizerDialogViewModel.GaModePresetOptions.First(o => o.Mode == GAModePreset.MinimumRelocation);

            Assert.Equal("30", vm.PopulationSize);
            Assert.Equal("40", vm.MaxGenerations);
            Assert.Equal("1", vm.MaxCranePositions);
            Assert.Equal("0.005", vm.MutationSigma);
            Assert.Equal("5", vm.RelocationWeight);
            Assert.True(vm.UseHungarianAssignment);
            Assert.True(vm.EnableMultiStageDiscovery);
        }

        [Fact]
        public void ViewModel_SelectingCraneFriendly_UpdatesGAFields()
        {
            var vm = new CraneOptimizerDialogViewModel();
            vm.SelectedGaModePresetOption = CraneOptimizerDialogViewModel.GaModePresetOptions.First(o => o.Mode == GAModePreset.CraneFriendly);

            Assert.Equal("60", vm.PopulationSize);
            Assert.Equal("80", vm.MaxGenerations);
            Assert.Equal("3", vm.MaxCranePositions);
            Assert.Equal("0.005", vm.MutationSigma);
            Assert.True(vm.UseHungarianAssignment);
            Assert.True(vm.EnableMultiStageDiscovery);
        }

        // -------------------------------------------------------------------
        // 9. Manual Field Edits reset SelectedPreset to None
        // -------------------------------------------------------------------
        [Fact]
        public void ViewModel_ManualFieldEdit_ResetsSelectedPresetToNone()
        {
            var vm = new CraneOptimizerDialogViewModel();
            vm.SelectedGaModePresetOption = CraneOptimizerDialogViewModel.GaModePresetOptions.First(o => o.Mode == GAModePreset.BalancedCoverage);
            Assert.Equal(GAModePreset.BalancedCoverage, vm.SelectedGaModePresetOption.Mode);

            // Simulate manual edit
            vm.PopulationSize = "100";

            Assert.Equal(GAModePreset.None, vm.SelectedGaModePresetOption.Mode);
        }

        // -------------------------------------------------------------------
        // 10. Workflow BuildSettings uses visible values (no hidden override)
        // -------------------------------------------------------------------
        [Fact]
        public void Workflow_BuildSettings_UsesVisibleValuesFromOptions()
        {
            var vm = new CraneOptimizerDialogViewModel();
            vm.SelectedGaModePresetOption = CraneOptimizerDialogViewModel.GaModePresetOptions.First(o => o.Mode == GAModePreset.BalancedCoverage);

            // Construct options using the visible values in ViewModel
            var options = new CraneOptimizerDialogOptions(
                vm.CoverageWeight,
                vm.RelocationWeight,
                "3.5", // WireLength
                "0.75", // AdditionalWt
                "0.85", // DeratingRatio
                "0.92", // SafetyFactor
                vm.MaxCranePositions,
                vm.EarlyStoppingGenerations,
                "2.0", // GridStep
                vm.RadiusPenaltyWeight,
                vm.RadiusQualityPenaltyWeight,
                vm.BalancePenaltyWeight,
                vm.OverweightPenaltyWeight,
                vm.HeightPenaltyWeight,
                vm.RadiusStdDevPenaltyWeight,
                vm.SimilarPositionPenaltyWeight,
                vm.RelocationDistancePenaltyWeight,
                "15.0", // MinimumRelocationDistance
                "4.0", // DuplicatePositionDistance
                vm.MutationExplorationMultiplier,
                vm.PositionSamplingAttempts,
                "60.0", // MaxRadiusForNormalisation
                vm.PopulationSize,
                vm.MaxGenerations,
                vm.MutationRate,
                vm.MutationSigma,
                vm.CrossoverRate,
                vm.RandomSeed,
                vm.ConvergenceThresholdPercent,
                new List<string> { "IfcBeam" },
                vm.EnableAdaptiveSigma ? "true" : "false",
                vm.SigmaCoolingExponent,
                vm.MinimumMutationSigma,
                vm.EnableConcurrentOptimization ? "true" : "false",
                "1.1", "2.2", "0.3", // Slew offsets
                "0.5", "0.6", "0.7", // Boom hinge offsets
                "5.0", // CraneFootprintRadius
                "false", "", // profiling
                vm.EnableMultiStageDiscovery ? "true" : "false",
                vm.MultiStageCoverageTarget.ToString(System.Globalization.CultureInfo.InvariantCulture),
                vm.AdaptiveRelocationScaleFactor.ToString(System.Globalization.CultureInfo.InvariantCulture),
                vm.UseHungarianAssignment ? "true" : "false",
                4, "SomeCondition",
                vm.SelectedGaModePresetOption.Mode);

            var workflow = new CraneOptimizerDialogWorkflow(null, null);
            var settings = workflow.BuildSettings(options);

            // Assert that the constructed settings match the options visible in the VM (no hidden factory override applied at runtime)
            Assert.Equal(60, settings.PopulationSize);
            Assert.Equal(80, settings.MaxGenerations);
            Assert.Equal(2, settings.MaxCranePositions);
            Assert.Equal(0.005, settings.MutationSigma);
            Assert.True(settings.UseHungarianAssignment);
            Assert.True(settings.EnableMultiStageDiscovery);

            // Physical and safety parameters remain intact
            Assert.Equal(3.5, settings.WireLength);
            Assert.Equal(0.75, settings.AdditionalWt);
            Assert.Equal(0.85, settings.DeratingRatio);
            Assert.Equal(0.92, settings.SafetyFactor);
        }
    }
}
