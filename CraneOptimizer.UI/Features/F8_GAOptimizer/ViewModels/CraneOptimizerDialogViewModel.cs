using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.Domain.Diagnostics;

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

    /// <summary>
    /// A display wrapper for a <see cref="GAModePreset"/> option shown in the ComboBox.
    /// </summary>
    public sealed record GAModePresetOption(
        GAModePreset Mode,
        string DisplayName,
        string Description);

    public sealed class LoadChartOperatingSetupOption
    {
        public static LoadChartOperatingSetupOption Any { get; } = new(
            "Any available line count / condition",
            "Uses every eligible imported capacity value. This preserves the previous optimizer behavior.",
            null,
            null,
            true);

        public LoadChartOperatingSetupOption(
            string displayText,
            string description,
            int? numberOfLinesConstraint,
            string? operatingConditionConstraint,
            bool isDefault = false)
        {
            DisplayText = displayText;
            Description = description;
            NumberOfLinesConstraint = numberOfLinesConstraint;
            OperatingConditionConstraint = operatingConditionConstraint;
            IsDefault = isDefault;
        }

        public string DisplayText { get; }
        public string Description { get; }
        public int? NumberOfLinesConstraint { get; }
        public string? OperatingConditionConstraint { get; }
        public bool IsDefault { get; }
    }

    public class CraneOptimizerDialogViewModel : ViewModelBase
    {
        // -----------------------------------------------------------------------
        // Model Import / Run Readiness Summary (Phase 4)
        // -----------------------------------------------------------------------
        private ImportReadinessSummary? _readinessSummary;
        public ImportReadinessSummary? ReadinessSummary
        {
            get => _readinessSummary;
            set
            {
                _readinessSummary = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReadinessStatusText));
                OnPropertyChanged(nameof(ReadinessStatusBrush));
                OnPropertyChanged(nameof(ReadinessWarningsVisibility));
            }
        }

        private bool _isElementCheckerEnabled = true;
        public bool IsElementCheckerEnabled
        {
            get => _isElementCheckerEnabled;
            set
            {
                if (_isElementCheckerEnabled != value)
                {
                    _isElementCheckerEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsElementCheckerDisabled));
                    OnPropertyChanged(nameof(ReadinessStatusText));
                    OnPropertyChanged(nameof(ReadinessStatusBrush));
                    OnPropertyChanged(nameof(ReadinessWarningsVisibility));
                }
            }
        }

        public bool IsElementCheckerDisabled => !IsElementCheckerEnabled;

        public string ReadinessStatusText
        {
            get
            {
                if (!IsElementCheckerEnabled)
                    return "Not Checked";
                if (ReadinessSummary == null)
                    return "Not Checked";
                return ReadinessSummary.OverallStatus.ToString();
            }
        }

        public System.Windows.Media.Brush ReadinessStatusBrush
        {
            get
            {
                if (!IsElementCheckerEnabled || ReadinessSummary == null)
                {
                    return System.Windows.Application.Current?.Resources["TextSecondaryBrush"] as System.Windows.Media.Brush 
                           ?? System.Windows.Media.Brushes.Gray;
                }

                string brushKey = ReadinessSummary.OverallStatus switch
                {
                    ValidationStatus.Valid => "SuccessBrush",
                    ValidationStatus.Warning => "WarningBrush",
                    ValidationStatus.Error => "DestructiveBrush",
                    _ => "TextSecondaryBrush"
                };

                return System.Windows.Application.Current?.Resources[brushKey] as System.Windows.Media.Brush 
                       ?? System.Windows.Media.Brushes.Gray;
            }
        }

        public System.Windows.Visibility ReadinessWarningsVisibility
        {
            get
            {
                return IsElementCheckerEnabled ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }

        // -----------------------------------------------------------------------
        // GA Mode Preset selector
        // -----------------------------------------------------------------------

        /// <summary>
        /// The four available GA Mode presets shown in the ComboBox.
        /// Candidate V1 — calibrated on auditmodel / STC800 + STC1300C.
        /// </summary>
        public static IReadOnlyList<GAModePresetOption> GaModePresetOptions { get; } =
            new List<GAModePresetOption>
            {
                new(GAModePreset.None,
                    GAModePresetFactory.GetDisplayName(GAModePreset.None),
                    GAModePresetFactory.GetDescription(GAModePreset.None)),
                new(GAModePreset.BalancedCoverage,
                    GAModePresetFactory.GetDisplayName(GAModePreset.BalancedCoverage),
                    GAModePresetFactory.GetDescription(GAModePreset.BalancedCoverage)),
                new(GAModePreset.MinimumRelocation,
                    GAModePresetFactory.GetDisplayName(GAModePreset.MinimumRelocation),
                    GAModePresetFactory.GetDescription(GAModePreset.MinimumRelocation)),
                new(GAModePreset.CraneFriendly,
                    GAModePresetFactory.GetDisplayName(GAModePreset.CraneFriendly),
                    GAModePresetFactory.GetDescription(GAModePreset.CraneFriendly)),
            };

        private GAModePresetOption _selectedGaModePresetOption = GaModePresetOptions[0]; // Default: None / Custom
        /// <summary>
        /// The currently selected GA Mode preset option.
        /// Defaults to <see cref="GAModePreset.None"/> (None / Custom).
        /// </summary>
        public GAModePresetOption SelectedGaModePresetOption
        {
            get => _selectedGaModePresetOption;
            set
            {
                if (!ReferenceEquals(_selectedGaModePresetOption, value))
                {
                    _selectedGaModePresetOption = value ?? GaModePresetOptions[0];
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedGaModePresetDescription));
                    ApplySelectedPresetToEditableFields(_selectedGaModePresetOption.Mode);
                }
            }
        }

        private bool _isApplyingPreset = false;

        private void OnManualFieldEdit()
        {
            if (_isApplyingPreset)
                return;

            if (SelectedGaModePresetOption != GaModePresetOptions[0])
            {
                SelectedGaModePresetOption = GaModePresetOptions[0];
            }
        }

        private void ApplySelectedPresetToEditableFields(GAModePreset mode)
        {
            if (mode == GAModePreset.None)
                return;

            _isApplyingPreset = true;
            try
            {
                // Retrieve preset values from GAModePresetFactory by applying to default settings
                var baseSettings = new GASettings();
                var presetSettings = GAModePresetFactory.Apply(mode, baseSettings);

                PopulationSize = presetSettings.PopulationSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
                MaxGenerations = presetSettings.MaxGenerations.ToString(System.Globalization.CultureInfo.InvariantCulture);
                MutationRate = presetSettings.MutationRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
                MutationSigma = presetSettings.MutationSigma.ToString(System.Globalization.CultureInfo.InvariantCulture);
                CrossoverRate = presetSettings.CrossoverRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
                MaxCranePositions = presetSettings.MaxCranePositions.ToString(System.Globalization.CultureInfo.InvariantCulture);
                EarlyStoppingGenerations = presetSettings.EarlyStoppingGenerations.ToString(System.Globalization.CultureInfo.InvariantCulture);
                ConvergenceThresholdPercent = presetSettings.ConvergenceThresholdPercent.ToString(System.Globalization.CultureInfo.InvariantCulture);

                CoverageWeight = presetSettings.CoverageWeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
                RelocationWeight = presetSettings.RelocationWeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
                RadiusPenaltyWeight = presetSettings.RadiusPenaltyWeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
                RadiusQualityPenaltyWeight = presetSettings.RadiusQualityPenaltyWeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
                BalancePenaltyWeight = presetSettings.BalancePenaltyWeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
                RadiusStdDevPenaltyWeight = presetSettings.RadiusStdDevPenaltyWeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
                SimilarPositionPenaltyWeight = presetSettings.SimilarPositionPenaltyWeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
                RelocationDistancePenaltyWeight = presetSettings.RelocationDistancePenaltyWeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
                OverweightPenaltyWeight = presetSettings.OverweightPenaltyWeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
                HeightPenaltyWeight = presetSettings.HeightPenaltyWeight.ToString(System.Globalization.CultureInfo.InvariantCulture);

                MutationExplorationMultiplier = presetSettings.MutationExplorationMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture);
                PositionSamplingAttempts = presetSettings.PositionSamplingAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture);
                EnableAdaptiveSigma = presetSettings.EnableAdaptiveSigma;
                SigmaCoolingExponent = presetSettings.SigmaCoolingExponent.ToString(System.Globalization.CultureInfo.InvariantCulture);
                MinimumMutationSigma = presetSettings.MinimumMutationSigma.ToString(System.Globalization.CultureInfo.InvariantCulture);
                EnableConcurrentOptimization = presetSettings.EnableConcurrentOptimization;

                // Also update the already-bound fields
                EnableMultiStageDiscovery = presetSettings.EnableMultiStageDiscovery;
                MultiStageCoverageTarget = presetSettings.MultiStageCoverageTarget;
                AdaptiveRelocationScaleFactor = presetSettings.AdaptiveRelocationScaleFactor;
                UseHungarianAssignment = presetSettings.UseHungarianAssignment;
            }
            finally
            {
                _isApplyingPreset = false;
            }
        }

        private string _populationSize = "30";
        public string PopulationSize
        {
            get => _populationSize;
            set { if (_populationSize != value) { _populationSize = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _maxGenerations = "30";
        public string MaxGenerations
        {
            get => _maxGenerations;
            set { if (_maxGenerations != value) { _maxGenerations = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _mutationRate = "0.10";
        public string MutationRate
        {
            get => _mutationRate;
            set { if (_mutationRate != value) { _mutationRate = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _mutationSigma = "0.05";
        public string MutationSigma
        {
            get => _mutationSigma;
            set { if (_mutationSigma != value) { _mutationSigma = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _crossoverRate = "0.80";
        public string CrossoverRate
        {
            get => _crossoverRate;
            set { if (_crossoverRate != value) { _crossoverRate = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _maxCranePositions = "3";
        public string MaxCranePositions
        {
            get => _maxCranePositions;
            set { if (_maxCranePositions != value) { _maxCranePositions = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _earlyStoppingGenerations = "20";
        public string EarlyStoppingGenerations
        {
            get => _earlyStoppingGenerations;
            set { if (_earlyStoppingGenerations != value) { _earlyStoppingGenerations = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _convergenceThresholdPercent = "0";
        public string ConvergenceThresholdPercent
        {
            get => _convergenceThresholdPercent;
            set { if (_convergenceThresholdPercent != value) { _convergenceThresholdPercent = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _coverageWeight = "10";
        public string CoverageWeight
        {
            get => _coverageWeight;
            set { if (_coverageWeight != value) { _coverageWeight = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _relocationWeight = "5";
        public string RelocationWeight
        {
            get => _relocationWeight;
            set { if (_relocationWeight != value) { _relocationWeight = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _radiusPenaltyWeight = "3";
        public string RadiusPenaltyWeight
        {
            get => _radiusPenaltyWeight;
            set { if (_radiusPenaltyWeight != value) { _radiusPenaltyWeight = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _radiusQualityPenaltyWeight = "1.0";
        public string RadiusQualityPenaltyWeight
        {
            get => _radiusQualityPenaltyWeight;
            set { if (_radiusQualityPenaltyWeight != value) { _radiusQualityPenaltyWeight = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _balancePenaltyWeight = "2";
        public string BalancePenaltyWeight
        {
            get => _balancePenaltyWeight;
            set { if (_balancePenaltyWeight != value) { _balancePenaltyWeight = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _radiusStdDevPenaltyWeight = "2";
        public string RadiusStdDevPenaltyWeight
        {
            get => _radiusStdDevPenaltyWeight;
            set { if (_radiusStdDevPenaltyWeight != value) { _radiusStdDevPenaltyWeight = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _similarPositionPenaltyWeight = "8";
        public string SimilarPositionPenaltyWeight
        {
            get => _similarPositionPenaltyWeight;
            set { if (_similarPositionPenaltyWeight != value) { _similarPositionPenaltyWeight = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _relocationDistancePenaltyWeight = "1.5";
        public string RelocationDistancePenaltyWeight
        {
            get => _relocationDistancePenaltyWeight;
            set { if (_relocationDistancePenaltyWeight != value) { _relocationDistancePenaltyWeight = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _overweightPenaltyWeight = "10";
        public string OverweightPenaltyWeight
        {
            get => _overweightPenaltyWeight;
            set { if (_overweightPenaltyWeight != value) { _overweightPenaltyWeight = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _heightPenaltyWeight = "5";
        public string HeightPenaltyWeight
        {
            get => _heightPenaltyWeight;
            set { if (_heightPenaltyWeight != value) { _heightPenaltyWeight = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _randomSeed = "42";
        public string RandomSeed
        {
            get => _randomSeed;
            set { if (_randomSeed != value) { _randomSeed = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _mutationExplorationMultiplier = "2.0";
        public string MutationExplorationMultiplier
        {
            get => _mutationExplorationMultiplier;
            set { if (_mutationExplorationMultiplier != value) { _mutationExplorationMultiplier = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _positionSamplingAttempts = "80";
        public string PositionSamplingAttempts
        {
            get => _positionSamplingAttempts;
            set { if (_positionSamplingAttempts != value) { _positionSamplingAttempts = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private bool _enableAdaptiveSigma = false;
        public bool EnableAdaptiveSigma
        {
            get => _enableAdaptiveSigma;
            set { if (_enableAdaptiveSigma != value) { _enableAdaptiveSigma = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _sigmaCoolingExponent = "1.5";
        public string SigmaCoolingExponent
        {
            get => _sigmaCoolingExponent;
            set { if (_sigmaCoolingExponent != value) { _sigmaCoolingExponent = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private string _minimumMutationSigma = "0.5";
        public string MinimumMutationSigma
        {
            get => _minimumMutationSigma;
            set { if (_minimumMutationSigma != value) { _minimumMutationSigma = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private bool _enableConcurrentOptimization = true;
        public bool EnableConcurrentOptimization
        {
            get => _enableConcurrentOptimization;
            set { if (_enableConcurrentOptimization != value) { _enableConcurrentOptimization = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        private bool _enableGroupedLifts = true;
        public bool EnableGroupedLifts
        {
            get => _enableGroupedLifts;
            set { if (_enableGroupedLifts != value) { _enableGroupedLifts = value; OnPropertyChanged(); OnManualFieldEdit(); } }
        }

        /// <summary>
        /// Description text for the currently selected preset, shown below the ComboBox.
        /// </summary>
        public string SelectedGaModePresetDescription => _selectedGaModePresetOption.Description;

        // -----------------------------------------------------------------------
        // Search text for parameters filtering
        // -----------------------------------------------------------------------

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

        private IReadOnlyList<LoadChartOperatingSetupOption> _loadChartOperatingSetupOptions = new[] { LoadChartOperatingSetupOption.Any };
        public IReadOnlyList<LoadChartOperatingSetupOption> LoadChartOperatingSetupOptions
        {
            get => _loadChartOperatingSetupOptions;
            private set
            {
                if (!ReferenceEquals(_loadChartOperatingSetupOptions, value))
                {
                    _loadChartOperatingSetupOptions = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsLoadChartOperatingSetupSelectorEnabled));
                    OnPropertyChanged(nameof(OperatingSetupSliderMaximum));
                    OnPropertyChanged(nameof(OperatingSetupScaleStops));
                    OnPropertyChanged(nameof(OperatingSetupScaleStopLabels));
                    OnPropertyChanged(nameof(OperatingSetupScaleEndLabel));
                    SyncSelectedLoadChartOperatingSetupSliderValue();
                    RaiseLoadChartOperatingSetupIntuitionChanged();
                }
            }
        }

        private LoadChartOperatingSetupOption _selectedLoadChartOperatingSetupOption = LoadChartOperatingSetupOption.Any;
        public LoadChartOperatingSetupOption SelectedLoadChartOperatingSetupOption
        {
            get => _selectedLoadChartOperatingSetupOption;
            set
            {
                if (!ReferenceEquals(_selectedLoadChartOperatingSetupOption, value))
                {
                    _selectedLoadChartOperatingSetupOption = value ?? LoadChartOperatingSetupOption.Any;
                    OnPropertyChanged();
                    SyncSelectedLoadChartOperatingSetupSliderValue();
                    RaiseLoadChartOperatingSetupIntuitionChanged();
                }
            }
        }

        private string _loadChartOperatingSetupStatus = "Select a crane to review imported load-chart line metadata.";
        public string LoadChartOperatingSetupStatus
        {
            get => _loadChartOperatingSetupStatus;
            private set
            {
                if (_loadChartOperatingSetupStatus != value)
                {
                    _loadChartOperatingSetupStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isLoadChartOperatingSetupExpanded;
        public bool IsLoadChartOperatingSetupExpanded
        {
            get => _isLoadChartOperatingSetupExpanded;
            set
            {
                if (_isLoadChartOperatingSetupExpanded != value)
                {
                    _isLoadChartOperatingSetupExpanded = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LoadChartOperatingSetupDetailsVisibility));
                    OnPropertyChanged(nameof(LoadChartOperatingSetupDisclosureText));
                    OnPropertyChanged(nameof(LoadChartOperatingSetupDisclosureGlyph));
                }
            }
        }

        public Visibility LoadChartOperatingSetupDetailsVisibility => IsLoadChartOperatingSetupExpanded
            ? Visibility.Visible
            : Visibility.Collapsed;

        public string LoadChartOperatingSetupDisclosureText => IsLoadChartOperatingSetupExpanded
            ? "Hide details"
            : "Show details";

        public string LoadChartOperatingSetupDisclosureGlyph => IsLoadChartOperatingSetupExpanded
            ? "v"
            : ">";

        public string OperatingSetupConstraintBadgeText => SelectedLoadChartOperatingSetupOption.IsDefault
            ? "Automatic"
            : "Constrained";

        public string OperatingSetupCollapsedSummary => SelectedLoadChartOperatingSetupOption.IsDefault
            ? "Automatic: any eligible imported capacity value."
            : $"Constrained: {SelectedLoadChartOperatingSetupOption.DisplayText}.";

        public string OperatingSetupCollapsedSubtext => SelectedLoadChartOperatingSetupOption.Description;
        public string SelectedLoadChartOperatingSetupDescription => SelectedLoadChartOperatingSetupOption.Description;

        public bool IsLoadChartOperatingSetupSelectorEnabled => LoadChartOperatingSetupOptions.Count > 1;

        public double OperatingSetupSliderMaximum => Math.Max(0, LoadChartOperatingSetupOptions.Count - 1);

        public string OperatingSetupScaleStartLabel => "Any setup";

        public string OperatingSetupScaleEndLabel => MaximumAvailableNumberOfLines > 0
            ? "More lines / slower hook"
            : "Specific chart setup";

        public IReadOnlyList<string> OperatingSetupScaleStopLabels =>
            LoadChartOperatingSetupOptions
                .Select(FormatOperatingSetupScaleStop)
                .ToList();

        public string OperatingSetupScaleStops => string.Join(
            "   ",
            OperatingSetupScaleStopLabels);

        private double _selectedLoadChartOperatingSetupSliderValue;
        public double SelectedLoadChartOperatingSetupSliderValue
        {
            get => _selectedLoadChartOperatingSetupSliderValue;
            set
            {
                int index = ClampOperatingSetupIndex((int)Math.Round(value));
                double normalizedValue = index;

                if (Math.Abs(_selectedLoadChartOperatingSetupSliderValue - normalizedValue) > 0.001d)
                {
                    _selectedLoadChartOperatingSetupSliderValue = normalizedValue;
                    OnPropertyChanged();
                }

                var option = LoadChartOperatingSetupOptions.ElementAtOrDefault(index) ?? LoadChartOperatingSetupOption.Any;
                if (!ReferenceEquals(SelectedLoadChartOperatingSetupOption, option))
                    SelectedLoadChartOperatingSetupOption = option;
            }
        }

        public string OperatingSetupSelectedTitle => SelectedLoadChartOperatingSetupOption.IsDefault
            ? "Any eligible capacity value"
            : SelectedLoadChartOperatingSetupOption.DisplayText;

        public string OperatingSetupSelectedTone
        {
            get
            {
                int? lines = SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint;
                if (!lines.HasValue)
                    return "No line-count constraint; the GA may use every eligible imported capacity value.";

                if (lines.Value <= 2)
                    return "Lighter / faster setup";

                if (lines.Value <= 4)
                    return "Balanced operating setup";

                return "Heavier / slower setup";
            }
        }

        public string LineSupportIndexExplanation => HasSelectedLoadChartNumberOfLines
            ? "Selected maximum available parts of line. Usable imported capacity values requiring fewer or equal lines remain eligible; certified capacity still comes from the imported load chart."
            : "No fixed line count selected; imported chart eligibility remains unconstrained.";

        public string HookSpeedIndexExplanation => HasSelectedLoadChartNumberOfLines
            ? "Approximate inverse-speed intuition: more supporting lines mean slower hook travel for the same hoist rope speed."
            : "No fixed hook-speed intuition is shown while Any is selected.";

        public bool HasSelectedLoadChartNumberOfLines => SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint.HasValue;

        public string OperatingSetupIntuitionSummary
        {
            get
            {
                int? lines = SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint;
                if (!lines.HasValue)
                    return "Any setup selected: no line-count constraint is applied. The optimizer may use any eligible imported capacity value.";

                string physicalFeel = lines.Value <= 2
                    ? "lighter/faster"
                    : lines.Value <= 4
                        ? "balanced"
                        : "heavier/slower";

                return $"{FormatNumberOfLines(lines.Value)} available: capacity lookup may use usable imported capacity values requiring up to this line count. More parts of line generally increase hoist-line mechanical advantage and reduce hook speed. This feels like a {physicalFeel} operating setup.";
            }
        }

        public string CapacityTendencyLabel => "Line-support index";

        public string HookSpeedTendencyLabel => "Hook-speed tendency";

        public string CapacityTendencyValue
        {
            get
            {
                int? lines = SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint;
                return lines.HasValue
                    ? $"up to {lines.Value:0}x line-support index"
                    : "No fixed line count";
            }
        }

        public string HookSpeedTendencyValue
        {
            get
            {
                int? lines = SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint;
                return lines.HasValue
                    ? $"~{100d / Math.Max(1, lines.Value):0.#}% of single-line speed"
                    : "No fixed line count";
            }
        }

        public double CapacityTendencyPercent
        {
            get
            {
                int? lines = SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint;
                int maximum = MaximumAvailableNumberOfLines;

                if (!lines.HasValue || maximum <= 0)
                    return 0d;

                return Math.Min(100d, Math.Max(0d, lines.Value * 100d / maximum));
            }
        }

        public double HookSpeedTendencyPercent
        {
            get
            {
                int? lines = SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint;
                if (!lines.HasValue)
                    return 0d;

                return Math.Min(100d, Math.Max(0d, 100d / Math.Max(1, lines.Value)));
            }
        }

        public string OperatingSetupChartCoverageSummary => SelectedLoadChartOperatingSetupOption.Description;

        public string OperatingSetupSafetyNote =>
            "Engineering intuition only. The optimizer still uses imported manufacturer load-chart capacities; line count is a maximum setup eligibility constraint, not a standalone certified capacity calculation.";

        private int MaximumAvailableNumberOfLines => Math.Max(
            SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint ?? 0,
            LoadChartOperatingSetupOptions
                .Select(option => option.NumberOfLinesConstraint ?? 0)
                .DefaultIfEmpty(0)
                .Max());

        private void RaiseLoadChartOperatingSetupIntuitionChanged()
        {
            OnPropertyChanged(nameof(SelectedLoadChartOperatingSetupDescription));
            OnPropertyChanged(nameof(HasSelectedLoadChartNumberOfLines));
            OnPropertyChanged(nameof(OperatingSetupIntuitionSummary));
            OnPropertyChanged(nameof(CapacityTendencyLabel));
            OnPropertyChanged(nameof(HookSpeedTendencyLabel));
            OnPropertyChanged(nameof(CapacityTendencyValue));
            OnPropertyChanged(nameof(HookSpeedTendencyValue));
            OnPropertyChanged(nameof(CapacityTendencyPercent));
            OnPropertyChanged(nameof(HookSpeedTendencyPercent));
            OnPropertyChanged(nameof(OperatingSetupChartCoverageSummary));
            OnPropertyChanged(nameof(OperatingSetupSafetyNote));
            OnPropertyChanged(nameof(OperatingSetupSelectedTitle));
            OnPropertyChanged(nameof(OperatingSetupSelectedTone));
            OnPropertyChanged(nameof(LineSupportIndexExplanation));
            OnPropertyChanged(nameof(HookSpeedIndexExplanation));
            OnPropertyChanged(nameof(OperatingSetupConstraintBadgeText));
            OnPropertyChanged(nameof(OperatingSetupCollapsedSummary));
            OnPropertyChanged(nameof(OperatingSetupCollapsedSubtext));
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
        public ParameterViewModel ParamEnableGroupedLifts { get; } = new("Enable Grouped Lifts");

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
                ParamUseHungarianAssignment, ParamEnableGroupedLifts
            });

            SectionDiagnostics = new SectionViewModel("6. Diagnostics", new List<ParameterViewModel>
            {
                ParamEnableProfiling
            });

            Sections = new List<SectionViewModel>
            {
                SectionCoreCalibration, SectionFitnessStrategy, SectionLayoutConstraints,
                SectionEngineeringModel, SectionAdvancedGA, SectionDiagnostics
            };

            FilterParameters();
        }

        public void RefreshLoadChartOperatingSetupOptions(Crane? crane)
        {
            var entries = crane?.Specifications?
                .SelectMany(specification => specification.LoadChart ?? Enumerable.Empty<TargetPoint>())
                .Where(point => point != null)
                .ToList() ?? new List<TargetPoint>();

            var usableCapacityValues = entries
                .Where(IsUsableCapacityValue)
                .ToList();

            int usableVisualColumnCount = usableCapacityValues
                .Select(BuildVisualColumnIdentity)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var options = new List<LoadChartOperatingSetupOption>
            {
                CreateAnyOperatingSetupOption(usableCapacityValues.Count, usableVisualColumnCount)
            };

            var lineGroups = entries
                .Where(point => point.NumberOfLines.HasValue && point.NumberOfLines.Value > 0)
                .GroupBy(point => point.NumberOfLines!.Value)
                .OrderBy(group => group.Key)
                .ToList();

            foreach (var group in lineGroups)
            {
                var eligibleForSelectedLineCount = entries
                    .Where(point =>
                        point.NumberOfLines.HasValue &&
                        point.NumberOfLines.Value > 0 &&
                        point.NumberOfLines.Value <= group.Key)
                    .ToList();

                var usableEligibleCapacityValues = eligibleForSelectedLineCount
                    .Where(IsUsableCapacityValue)
                    .ToList();

                int usableLineVisualColumnCount = usableEligibleCapacityValues
                    .Select(BuildVisualColumnIdentity)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                string lineText = FormatNumberOfLines(group.Key);

                options.Add(new LoadChartOperatingSetupOption(
                    $"Up to {lineText}",
                    $"Allows capacity lookup to use {FormatUsableCapacityValueCount(usableEligibleCapacityValues.Count)} requiring up to {lineText} across {FormatVisualColumnCount(usableLineVisualColumnCount)}. Blank/zero preserved chart cells remain non-liftable.",
                    group.Key,
                    null));
            }

            var conditionGroups = entries
                .Select(point => new { Point = point, Label = GetActionableOperatingConditionLabel(point) })
                .Where(item => !string.IsNullOrWhiteSpace(item.Label))
                .GroupBy(item => item.Label!, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var group in conditionGroups)
            {
                var usableConditionCapacityValues = group
                    .Select(item => item.Point)
                    .Where(IsUsableCapacityValue)
                    .ToList();

                int usableConditionVisualColumnCount = usableConditionCapacityValues
                    .Select(BuildVisualColumnIdentity)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                options.Add(new LoadChartOperatingSetupOption(
                    $"Condition: {group.Key}",
                    $"Restricts capacity lookup to {FormatUsableCapacityValueCount(usableConditionCapacityValues.Count)} with this operating condition label across {FormatVisualColumnCount(usableConditionVisualColumnCount)}. Blank/zero preserved chart cells remain non-liftable.",
                    null,
                    group.Key));
            }

            LoadChartOperatingSetupOptions = options;
            SelectedLoadChartOperatingSetupOption = options[0];
            LoadChartOperatingSetupStatus = BuildLoadChartOperatingSetupStatus(entries.Count, lineGroups.Count, conditionGroups.Count);
        }

        private static LoadChartOperatingSetupOption CreateAnyOperatingSetupOption(
            int usableCapacityValueCount,
            int usableVisualColumnCount)
        {
            string description = usableCapacityValueCount > 0
                ? $"Uses {FormatUsableCapacityValueCount(usableCapacityValueCount)} across {FormatVisualColumnCount(usableVisualColumnCount)}. No line-count or operating-condition constraint is applied."
                : "No usable imported capacity values were found. The optimizer will use its existing fallback behavior.";

            return new LoadChartOperatingSetupOption(
                "Any available line count / condition",
                description,
                null,
                null,
                true);
        }
        private static string? GetActionableOperatingConditionLabel(TargetPoint point)
        {
            string? rawLabel = point.OperatingConditionLabel?.Trim();
            if (string.IsNullOrWhiteSpace(rawLabel))
                return null;

            string label = CollapseWhitespace(rawLabel);
            return IsSyntheticPdfVisualOperatingConditionLabel(point, label)
                ? null
                : label;
        }

        private static bool IsSyntheticPdfVisualOperatingConditionLabel(TargetPoint point, string label)
        {
            var segments = System.Text.RegularExpressions.Regex
                .Split(label, @"\s*/\s*")
                .Select(CollapseWhitespace)
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToList();

            if (segments.Count == 0)
                return false;

            if (segments.Count == 1)
                return IsLineCountOnlyToken(point, segments[0]);

            bool hasLineCountToken = segments.Any(segment => IsLineCountOnlyToken(point, segment));
            if (!hasLineCountToken)
                return false;

            return segments.All(segment =>
                IsLineCountOnlyToken(point, segment) ||
                IsGeneratedBoomHeaderToken(point, segment));
        }

        private static bool IsLineCountOnlyToken(TargetPoint point, string token)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                token,
                @"^(?<count>\d+)\s*(?:line(?:s|\(s\))?|falls?|parts?)(?:\s+of\s+line)?$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

            if (!match.Success)
                return false;

            if (!int.TryParse(
                    match.Groups["count"].Value,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int count))
                return false;

            return !point.NumberOfLines.HasValue || point.NumberOfLines.Value == count;
        }

        private static bool IsGeneratedBoomHeaderToken(TargetPoint point, string token)
        {
            if (!TryParseBoomHeaderToken(token, out double tokenValue))
                return false;

            string? visualHeader = point.VisualColumnHeader?.Trim();
            if (!string.IsNullOrWhiteSpace(visualHeader) &&
                TryParseBoomHeaderToken(visualHeader, out double visualHeaderValue) &&
                AreApproximatelyEqual(tokenValue, visualHeaderValue))
            {
                return true;
            }

            return point.BoomLength > 0d && AreApproximatelyEqual(tokenValue, point.BoomLength);
        }

        private static bool TryParseBoomHeaderToken(string token, out double value)
        {
            value = 0d;

            string candidate = CollapseWhitespace(token);
            candidate = System.Text.RegularExpressions.Regex.Replace(candidate, @"\s*\(\d+\)\s*$", string.Empty);
            candidate = System.Text.RegularExpressions.Regex.Replace(
                candidate,
                @"\s*m\s*$",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            candidate = candidate.Trim().Replace(',', '.');

            if (!System.Text.RegularExpressions.Regex.IsMatch(candidate, @"^[+-]?\d+(?:\.\d+)?$"))
                return false;

            return double.TryParse(
                candidate,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }

        private static bool AreApproximatelyEqual(double left, double right)
        {
            return Math.Abs(left - right) <= 0.0001d;
        }

        private static string CollapseWhitespace(string value)
        {
            return System.Text.RegularExpressions.Regex.Replace(value.Trim(), @"\s+", " ");
        }
        private static bool IsUsableCapacityValue(TargetPoint point)
        {
            return point.Capacity > 0d
                && !double.IsNaN(point.Capacity)
                && !double.IsInfinity(point.Capacity);
        }
        private static string FormatUsableCapacityValueCount(int count)
        {
            return FormatCount(count, "usable imported capacity value", "usable imported capacity values");
        }

        private static string FormatVisualColumnCount(int count)
        {
            return FormatCount(count, "visual column", "visual columns");
        }

        private static string FormatCount(int count, string singular, string plural)
        {
            return $"{count} {(count == 1 ? singular : plural)}";
        }
        private static string BuildVisualColumnIdentity(TargetPoint point)
        {
            if (point.VisualColumnIndex.HasValue)
                return $"visual:{point.VisualColumnIndex.Value}";

            if (!string.IsNullOrWhiteSpace(point.VisualColumnHeader))
                return $"header:{point.VisualColumnHeader.Trim()}";

            return $"boom:{point.BoomLength:0.###}";
        }

        private static string FormatNumberOfLines(int numberOfLines)
        {
            return numberOfLines == 1
                ? "1 line"
                : $"{numberOfLines} lines";
        }

        private static string BuildLoadChartOperatingSetupStatus(
            int chartCellCount,
            int lineOptionCount,
            int conditionOptionCount)
        {
            if (chartCellCount == 0)
                return "The selected crane has no load-chart entries. The optimizer will use its existing fallback behavior.";

            if (lineOptionCount == 0 && conditionOptionCount == 0)
                return "No imported line-count or operating-condition metadata was found. The optimizer will use any available chart entry.";

            var parts = new List<string>();
            if (lineOptionCount > 0)
                parts.Add($"{lineOptionCount} line-count option(s)");
            if (conditionOptionCount > 0)
                parts.Add($"{conditionOptionCount} operating-condition option(s)");

            return $"Imported load chart metadata found: {string.Join(", ", parts)}. Choose Any to preserve current behavior.";
        }

        private int ClampOperatingSetupIndex(int index)
        {
            int maximum = Math.Max(0, LoadChartOperatingSetupOptions.Count - 1);
            return Math.Max(0, Math.Min(index, maximum));
        }

        private void SyncSelectedLoadChartOperatingSetupSliderValue()
        {
            int index = LoadChartOperatingSetupOptions
                .Select((option, optionIndex) => new { option, optionIndex })
                .FirstOrDefault(item => ReferenceEquals(item.option, SelectedLoadChartOperatingSetupOption))?.optionIndex ?? 0;

            double normalizedValue = ClampOperatingSetupIndex(index);
            if (Math.Abs(_selectedLoadChartOperatingSetupSliderValue - normalizedValue) > 0.001d)
            {
                _selectedLoadChartOperatingSetupSliderValue = normalizedValue;
                OnPropertyChanged(nameof(SelectedLoadChartOperatingSetupSliderValue));
            }
        }

        private static string FormatOperatingSetupScaleStop(LoadChartOperatingSetupOption option)
        {
            if (option.IsDefault)
                return "Any";

            if (option.NumberOfLinesConstraint.HasValue)
                return option.NumberOfLinesConstraint.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(option.OperatingConditionConstraint))
                return option.DisplayText.Replace("Condition:", string.Empty).Trim();

            return option.DisplayText;
        }

        private bool IsDeveloperParameter(string label)
        {
            return label switch
            {
                "Radius Penalty Weight" or "Radius Quality Weight" or "Overweight Weight" or "Height Penalty Weight" or
                "Balance Penalty Weight" or "Radius StdDev Weight" or "Similar Position Weight" or "Relocation Dist Weight" or
                "Enable Adaptive Sigma" or "Sigma Cooling Exponent" or "Min Mutation Sigma (m)" or "Mutation Explore Mult" or
                "Sampling Attempts" or "Concurrent Opt" or "Enable Multi-Stage Discovery" or "Coverage Target" or
                "Adaptive Relocation Scale" or "Use Hungarian Assignment" or "Random Seed" or "Enable Profiling" => true,
                _ => false
            };
        }

        private void FilterParameters()
        {
            bool showDev = CraneOptimizer.UI.Core.Configuration.AppFeatureFlags.ShowGADeveloperParameters;

            foreach (var section in Sections)
            {
                bool anyVisible = false;
                foreach (var param in section.Parameters)
                {
                    if (IsDeveloperParameter(param.Label) && !showDev)
                    {
                        param.Visibility = Visibility.Collapsed;
                        continue;
                    }

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
