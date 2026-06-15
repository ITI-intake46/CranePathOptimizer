using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using CraneOptimizer.Domain.Entities;
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
