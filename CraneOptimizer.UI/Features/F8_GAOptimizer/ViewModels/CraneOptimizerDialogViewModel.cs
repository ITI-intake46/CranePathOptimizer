using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using CraneOptimizer.UI.Core.Base;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels
{
    public class ParameterViewModel : ViewModelBase
    {
        private Visibility _visibility = Visibility.Visible;

        public string Label { get; }

        public Visibility Visibility
        {
            get => _visibility;
            set
            {
                if (_visibility != value)
                {
                    _visibility = value;
                    OnPropertyChanged();
                }
            }
        }

        public ParameterViewModel(string label)
        {
            Label = label;
        }
    }

    public class SectionViewModel : ViewModelBase
    {
        private Visibility _visibility = Visibility.Visible;

        public string Header { get; }
        public List<ParameterViewModel> Parameters { get; }

        public Visibility Visibility
        {
            get => _visibility;
            set
            {
                if (_visibility != value)
                {
                    _visibility = value;
                    OnPropertyChanged();
                }
            }
        }

        public SectionViewModel(string header, List<ParameterViewModel> parameters)
        {
            Header = header;
            Parameters = parameters;
        }
    }

    public class CraneOptimizerDialogViewModel : ViewModelBase
    {
        // Search text for parameters filtering
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    FilterParameters();
                }
            }
        }

        // New Phase 16 properties
        private bool _enableMultiStageDiscovery = true;
        public bool EnableMultiStageDiscovery
        {
            get => _enableMultiStageDiscovery;
            set
            {
                if (_enableMultiStageDiscovery != value)
                {
                    _enableMultiStageDiscovery = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _multiStageCoverageTarget = 0.95;
        public double MultiStageCoverageTarget
        {
            get => _multiStageCoverageTarget;
            set
            {
                if (Math.Abs(_multiStageCoverageTarget - value) > 0.0001)
                {
                    _multiStageCoverageTarget = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _adaptiveRelocationScaleFactor = 0.40;
        public double AdaptiveRelocationScaleFactor
        {
            get => _adaptiveRelocationScaleFactor;
            set
            {
                if (Math.Abs(_adaptiveRelocationScaleFactor - value) > 0.0001)
                {
                    _adaptiveRelocationScaleFactor = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _useHungarianAssignment = false;
        public bool UseHungarianAssignment
        {
            get => _useHungarianAssignment;
            set
            {
                if (_useHungarianAssignment != value)
                {
                    _useHungarianAssignment = value;
                    OnPropertyChanged();
                }
            }
        }

        // Section ViewModels
        public SectionViewModel SectionCoreCalibration { get; }
        public SectionViewModel SectionFitnessStrategy { get; }
        public SectionViewModel SectionLayoutConstraints { get; }
        public SectionViewModel SectionEngineeringModel { get; }
        public SectionViewModel SectionAdvancedGA { get; }
        public SectionViewModel SectionDiagnostics { get; }

        public List<SectionViewModel> Sections { get; }

        // Parameter ViewModels for clean binding in XAML
        public ParameterViewModel ParamPopulationSize { get; } = new("Population Size");
        public ParameterViewModel ParamMaxGenerations { get; } = new("Max Generations");
        public ParameterViewModel ParamMutationRate { get; } = new("Mutation Rate");
        public ParameterViewModel ParamMutationSigma { get; } = new("Mutation Sigma");
        public ParameterViewModel ParamMaxCranePositions { get; } = new("Max Crane Positions");
        public ParameterViewModel ParamEarlyStopGenerations { get; } = new("Early Stop Generations");
        public ParameterViewModel ParamConvergenceThreshold { get; } = new("Convergence Threshold (%)");

        public ParameterViewModel ParamCoverageWeight { get; } = new("Coverage Weight");
        public ParameterViewModel ParamRelocationWeight { get; } = new("Relocation Weight");
        public ParameterViewModel ParamRadiusPenaltyWeight { get; } = new("Radius Penalty Weight");
        public ParameterViewModel ParamRadiusQualityWeight { get; } = new("Radius Quality Weight");
        public ParameterViewModel ParamBalancePenaltyWeight { get; } = new("Balance Penalty Weight");
        public ParameterViewModel ParamRadiusStdDevWeight { get; } = new("Radius StdDev Weight");
        public ParameterViewModel ParamSimilarPositionWeight { get; } = new("Similar Position Weight");
        public ParameterViewModel ParamRelocationDistWeight { get; } = new("Relocation Dist Weight");
        public ParameterViewModel ParamOverweightWeight { get; } = new("Overweight Weight");
        public ParameterViewModel ParamHeightPenaltyWeight { get; } = new("Height Penalty Weight");

        public ParameterViewModel ParamMinRelocationDist { get; } = new("Min Relocation Dist (m)");
        public ParameterViewModel ParamGridStep { get; } = new("Grid Step (m)");
        public ParameterViewModel ParamTargetElementsAllowList { get; } = new("TARGET ELEMENTS ALLOW-LIST");

        public ParameterViewModel ParamWireLength { get; } = new("Wire Length (m)");
        public ParameterViewModel ParamHookWeight { get; } = new("Hook Weight (t)");
        public ParameterViewModel ParamDerating { get; } = new("Derating");
        public ParameterViewModel ParamSafetyFactor { get; } = new("Safety Factor");

        public ParameterViewModel ParamRandomSeed { get; } = new("Random Seed");
        public ParameterViewModel ParamCrossoverRate { get; } = new("Crossover Rate");
        public ParameterViewModel ParamMutationExploreMult { get; } = new("Mutation Explore Mult");
        public ParameterViewModel ParamEnableAdaptiveSigma { get; } = new("Enable Adaptive Sigma");
        public ParameterViewModel ParamSigmaCoolingExponent { get; } = new("Sigma Cooling Exponent");
        public ParameterViewModel ParamMinMutationSigma { get; } = new("Min Mutation Sigma (m)");
        public ParameterViewModel ParamSamplingAttempts { get; } = new("Sampling Attempts");
        public ParameterViewModel ParamConcurrentOpt { get; } = new("Concurrent Opt");

        public ParameterViewModel ParamSlewOffsetX { get; } = new("Offset X (m) slew");
        public ParameterViewModel ParamSlewOffsetY { get; } = new("Offset Y (m) slew");
        public ParameterViewModel ParamSlewOffsetZ { get; } = new("Offset Z (m) slew");
        public ParameterViewModel ParamBoomHingeOffsetX { get; } = new("Offset X (m) boom");
        public ParameterViewModel ParamBoomHingeOffsetY { get; } = new("Offset Y (m) boom");
        public ParameterViewModel ParamBoomHingeOffsetZ { get; } = new("Offset Z (m) boom");
        public ParameterViewModel ParamMaxRadiusNorm { get; } = new("Max Radius Norm (m)");
        public ParameterViewModel ParamDuplicateDist { get; } = new("Duplicate Dist (m)");
        public ParameterViewModel ParamFootprintRadius { get; } = new("Footprint Radius (m)");
        public ParameterViewModel ParamEnableProfiling { get; } = new("Enable Profiling");
        public ParameterViewModel ParamProfilingLogDirectory { get; } = new("Profiling Log Directory");

        // The 4 new Phase 16 parameters in Section 5
        public ParameterViewModel ParamEnableMultiStageDiscovery { get; } = new("Enable Multi-Stage Discovery");
        public ParameterViewModel ParamMultiStageCoverageTarget { get; } = new("Coverage Target");
        public ParameterViewModel ParamAdaptiveRelocationScaleFactor { get; } = new("Adaptive Relocation Scale");
        public ParameterViewModel ParamUseHungarianAssignment { get; } = new("Use Hungarian Assignment");

        public CraneOptimizerDialogViewModel()
        {
            SectionCoreCalibration = new SectionViewModel("1. Core Calibration", new List<ParameterViewModel>
            {
                ParamPopulationSize, ParamMaxGenerations, ParamMutationRate, ParamMutationSigma,
                ParamMaxCranePositions, ParamEarlyStopGenerations, ParamConvergenceThreshold
            });

            SectionFitnessStrategy = new SectionViewModel("2. Fitness Strategy", new List<ParameterViewModel>
            {
                ParamCoverageWeight, ParamRelocationWeight, ParamRadiusPenaltyWeight, ParamRadiusQualityWeight,
                ParamBalancePenaltyWeight, ParamRadiusStdDevWeight, ParamSimilarPositionWeight, ParamRelocationDistWeight,
                ParamOverweightWeight, ParamHeightPenaltyWeight
            });

            SectionLayoutConstraints = new SectionViewModel("3. Layout Constraints", new List<ParameterViewModel>
            {
                ParamMinRelocationDist, ParamGridStep, ParamTargetElementsAllowList
            });

            SectionEngineeringModel = new SectionViewModel("4. Engineering Model", new List<ParameterViewModel>
            {
                ParamWireLength, ParamHookWeight, ParamDerating, ParamSafetyFactor
            });

            SectionAdvancedGA = new SectionViewModel("5. Advanced GA", new List<ParameterViewModel>
            {
                ParamRandomSeed, ParamCrossoverRate, ParamMutationExploreMult, ParamEnableAdaptiveSigma,
                ParamSigmaCoolingExponent, ParamMinMutationSigma, ParamSamplingAttempts, ParamConcurrentOpt,
                ParamEnableMultiStageDiscovery, ParamMultiStageCoverageTarget, ParamAdaptiveRelocationScaleFactor,
                ParamUseHungarianAssignment
            });

            SectionDiagnostics = new SectionViewModel("6. Diagnostics", new List<ParameterViewModel>
            {
                ParamSlewOffsetX, ParamSlewOffsetY, ParamSlewOffsetZ,
                ParamBoomHingeOffsetX, ParamBoomHingeOffsetY, ParamBoomHingeOffsetZ,
                ParamMaxRadiusNorm, ParamDuplicateDist, ParamFootprintRadius, ParamEnableProfiling, ParamProfilingLogDirectory
            });

            Sections = new List<SectionViewModel>
            {
                SectionCoreCalibration, SectionFitnessStrategy, SectionLayoutConstraints,
                SectionEngineeringModel, SectionAdvancedGA, SectionDiagnostics
            };
        }

        private void FilterParameters()
        {
            foreach (var section in Sections)
            {
                bool anyVisible = false;
                foreach (var param in section.Parameters)
                {
                    if (string.IsNullOrWhiteSpace(SearchText) ||
                        param.Label.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    {
                        param.Visibility = Visibility.Visible;
                        anyVisible = true;
                    }
                    else
                    {
                        param.Visibility = Visibility.Collapsed;
                    }
                }
                section.Visibility = anyVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
