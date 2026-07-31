using System.Globalization;
using System.Text.RegularExpressions;
using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services;

public interface IGaLiftSummaryPresentationMapper
{
    Task<GaLiftSummaryPresentationModel> MapAsync(
        GaLiftAnalysisResult analysis,
        CancellationToken cancellationToken = default);

    GaLiftSummaryPresentationModel Map(GaLiftAnalysisResult analysis);
}

public sealed class GaLiftSummaryPresentationMapper : IGaLiftSummaryPresentationMapper
{
    private const string ConsistencyWarningCode = "LIFT_SUMMARY_FAILURE_TOTAL_MISMATCH";
    private const string ApprovedSafetyNotice =
        "This summary is an application-generated advisory assessment based on the completed optimizer result, crane data, load-chart evidence, and configured assumptions. Final crane selection and lift planning require qualified engineering review and manufacturer data verification.";

    public Task<GaLiftSummaryPresentationModel> MapAsync(
        GaLiftAnalysisResult analysis,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        return Task.Run(() => Map(analysis), cancellationToken);
    }

    public GaLiftSummaryPresentationModel Map(GaLiftAnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        GaLiftMetricSummary metrics = analysis.Metrics;
        int completed = Math.Max(0, metrics.SuccessfulLiftCount);
        int unresolved = Math.Max(metrics.FailedLiftCount, metrics.TotalLiftCount - completed);
        int total = Math.Max(metrics.TotalLiftCount, completed + unresolved);
        GaLiftAnalysisItem[] unresolvedOutcomes = analysis.LiftResults
            .Where(item => !item.IsLifted)
            .ToArray();
        FailureAggregation failures = AggregateFailures(unresolvedOutcomes, unresolved);
        if (unresolvedOutcomes.Length > unresolved)
        {
            unresolved = unresolvedOutcomes.Length;
            total = Math.Max(total, completed + unresolved);
            failures = AggregateFailures(unresolvedOutcomes, unresolved);
        }

        bool hasConsistencyWarning =
            failures.Total != unresolved ||
            unresolvedOutcomes.Length != unresolved;
        if (failures.Total < unresolved)
            failures = failures with { Other = failures.Other + unresolved - failures.Total };

        double? highestCapacity = Valid(metrics.CapacityUtilizationMaximum)
            ?? MaximumCompletedCapacity(analysis.LiftResults);
        double? averageCapacity = Valid(metrics.CapacityUtilizationAverage)
            ?? AverageCompletedCapacity(analysis.LiftResults);
        double? smallestReserve = ValidFinite(metrics.MinimumCapacityMarginTon)
            ?? MinimumCompletedReserve(analysis.LiftResults);
        double nearLimitThreshold = Valid(analysis.Thresholds.BorderlineUtilization)
            ?? GaLiftAnalysisRules.BorderlineUtilization;
        int nearLimitCount = Math.Max(0, metrics.NearLimitLiftCount);

        FailureKind dominantKind = failures.Dominant;
        string dominantTitle = DominantTitle(dominantKind, unresolved);
        string dominantExplanation = DominantExplanation(dominantKind, unresolved);
        string status = BuildStatus(failures, unresolved, hasConsistencyWarning);
        string primaryRecommendation = BuildPrimaryRecommendation(failures, unresolved);
        string coverage = Percent(total > 0 ? completed / (double)total : metrics.CoverageRatio);
        GaLiftSummaryCandidateRow[] alternativeRows =
            BuildCandidateRows(analysis, failures).ToArray();
        string alternativeSummary = BuildAlternativeSummary(
            analysis.AlternativeScreeningOutcome,
            failures,
            alternativeRows);
        string alternativesMessage = BuildAlternativesMessage(
            analysis.AlternativeScreeningOutcome,
            failures,
            alternativeRows);
        string runDate = (analysis.RunContext.OptimizerCompletedAtUtc ?? analysis.GeneratedAtUtc)
            .ToLocalTime()
            .ToString("g", CultureInfo.CurrentCulture);
        string craneName = CleanCraneName(analysis.RunContext.CraneName);
        string executiveSummary = BuildExecutiveSummary(
            total,
            completed,
            unresolved,
            coverage,
            dominantExplanation,
            status,
            alternativeSummary,
            primaryRecommendation);
        string[] secondaryRecommendations = BuildSecondaryRecommendations(
                nearLimitCount,
                nearLimitThreshold,
                failures)
            .Take(3)
            .ToArray();
        string operatingMode = HumanizeOperatingMode(analysis.RunContext.OperatingMode);
        bool hasOptimizationTime = analysis.Performance.RunDurationMilliseconds > 0;
        string inputDescription = BuildInputDescription(failures);
        string[] limitations = BuildLimitations(analysis, hasConsistencyWarning).ToArray();

        GaLiftSummaryPresentationModel model = new()
        {
            ProjectName = ValueOrFallback(analysis.RunContext.ProjectName, "Unnamed project"),
            CraneName = craneName,
            RunDateText = runDate,
            RunReferenceText = ShortRunReference(analysis.OptimizationRunId),
            CoverageText = coverage,
            SummaryStatusText = status,
            ExecutiveSummaryText = executiveSummary,
            TotalLiftsText = Number(total),
            CompletedLiftsText = Number(completed),
            UnresolvedLiftsText = Number(unresolved),
            HighestCapacityUseText = Percent(highestCapacity),
            AverageCapacityUseText = Percent(averageCapacity),
            SmallestCapacityReserveText = Tonnes(smallestReserve),
            NearCapacityLimitText = Number(nearLimitCount),
            NearCapacityLimitTooltip =
                $"Completed lifts operating at or above the configured {nearLimitThreshold:P0} capacity-use advisory threshold.",
            CapacityFailuresText = Number(failures.Capacity),
            RadiusFailuresText = Number(failures.Radius),
            BoomHeightFailuresText = Number(failures.BoomHeight),
            LoadChartFailuresText = Number(failures.LoadChart),
            InputDataIssuesText = Number(failures.Input),
            OtherUnresolvedIssuesText = Number(failures.Other),
            HasCapacityFailures = failures.Capacity > 0,
            HasRadiusFailures = failures.Radius > 0,
            HasBoomHeightFailures = failures.BoomHeight > 0,
            HasLoadChartFailures = failures.LoadChart > 0,
            HasInputDataIssues = failures.Input > 0,
            HasOtherUnresolvedIssues = failures.Other > 0,
            InputDataIssueDescription = inputDescription,
            DominantFailureTitleText = dominantTitle,
            DominantFailureExplanationText = dominantExplanation,
            SafetyFactorText = Ratio(analysis.RunContext.SafetyFactor),
            DeratingRatioText = Ratio(analysis.RunContext.DeratingRatio),
            WireLengthText = Meters(analysis.RunContext.WireRopeLengthM),
            HookWeightText = Tonnes(analysis.RunContext.HookRiggingWeightTon),
            AccessoriesWeightText = Tonnes(analysis.RunContext.TotalLiftingAccessoriesWeightTon),
            OperatingModeText = operatingMode,
            HasOperatingMode = !string.IsNullOrWhiteSpace(operatingMode),
            CandidatePointsGeneratedText = OptionalNumber(analysis.Performance.CandidatePointsGenerated),
            ValidCandidatePointsText = OptionalNumber(analysis.Performance.ValidCandidatePoints),
            RejectedCandidatePointsText = OptionalNumber(analysis.Performance.RejectedCandidatePoints),
            SelectedCranePositionsText = Number(Math.Max(0, analysis.Performance.FinalSetupPositionCount)),
            OptimizationTimeText = hasOptimizationTime
                ? Duration(analysis.Performance.RunDurationMilliseconds)
                : string.Empty,
            HasOptimizationTime = hasOptimizationTime,
            PrimaryRecommendationText = primaryRecommendation,
            SecondaryRecommendations = secondaryRecommendations,
            HasAlternativesSection =
                failures.HasUnreliableEvidence ||
                analysis.AlternativeScreeningOutcome != AlternativeScreeningOutcome.NotRun,
            AlternativesMessage = alternativesMessage,
            AlternativeRows = alternativeRows,
            Limitations = limitations,
            SafetyNotice = ApprovedSafetyNotice,
            InternalConsistencyWarningCode = hasConsistencyWarning ? ConsistencyWarningCode : null
        };

        return model with { CopyText = BuildCopyText(model, failures, alternativeSummary) };
    }

    private static FailureAggregation AggregateFailures(
        IReadOnlyList<GaLiftAnalysisItem> unresolvedOutcomes,
        int expectedUnresolved)
    {
        int capacity = 0;
        int radius = 0;
        int boomHeight = 0;
        int chartEnvelope = 0;
        int chartEvidence = 0;
        int inputWeight = 0;
        int inputCenterOfGravity = 0;
        int inputGeometry = 0;
        int inputSetup = 0;
        int other = 0;

        foreach (GaLiftAnalysisItem outcome in unresolvedOutcomes)
        {
            FailureKind kind = ClassifyFailure(outcome);
            switch (kind)
            {
                case FailureKind.Capacity:
                    capacity++;
                    break;
                case FailureKind.Radius:
                    radius++;
                    break;
                case FailureKind.BoomHeight:
                    boomHeight++;
                    break;
                case FailureKind.LoadChartEnvelope:
                    chartEnvelope++;
                    break;
                case FailureKind.LoadChartEvidence:
                    chartEvidence++;
                    break;
                case FailureKind.InputWeight:
                    inputWeight++;
                    break;
                case FailureKind.InputCenterOfGravity:
                    inputCenterOfGravity++;
                    break;
                case FailureKind.InputGeometry:
                    inputGeometry++;
                    break;
                case FailureKind.InputSetup:
                    inputSetup++;
                    break;
                default:
                    other++;
                    break;
            }
        }

        FailureAggregation result = new(
            capacity,
            radius,
            boomHeight,
            chartEnvelope,
            chartEvidence,
            inputWeight,
            inputCenterOfGravity,
            inputGeometry,
            inputSetup,
            other);
        return result.Total >= expectedUnresolved
            ? result
            : result with { Other = result.Other + expectedUnresolved - result.Total };
    }

    private static FailureKind ClassifyFailure(GaLiftAnalysisItem outcome)
    {
        FailureKind canonical = ClassifyCode(outcome.FailureCategory, outcome.ChartStatus);
        if (canonical != FailureKind.Other)
            return canonical;

        FailureKind status = ClassifyCode(outcome.LiftStatus, outcome.ChartStatus);
        if (status != FailureKind.Other)
            return status;

        FailureKind chart = ClassifyChartStatus(outcome.ChartStatus);
        if (chart != FailureKind.Other)
            return chart;

        if (IsGreaterThanOne(outcome.CapacityUtilization) ||
            IsPositiveShortfall(outcome.RequiredCapacityTon, outcome.AvailableCapacityTon))
        {
            return FailureKind.Capacity;
        }
        if (IsGreaterThanOne(outcome.RadiusUtilization))
            return FailureKind.Radius;
        if (IsGreaterThanOne(outcome.BoomUtilization))
            return FailureKind.BoomHeight;

        FailureKind reason = ClassifyCode(outcome.FailureReason, outcome.ChartStatus);
        return reason;
    }

    private static FailureKind ClassifyCode(string? value, string? chartStatus)
    {
        string code = NormalizeCode(value);
        if (string.IsNullOrEmpty(code) || code is "unavailable" or "unassigned" or "specialcase")
            return FailureKind.Other;

        if (ContainsAny(code, "missingcenterofgravity", "missingcog", "invalidcenterofgravity"))
            return FailureKind.InputCenterOfGravity;
        if (ContainsAny(code, "missingweight", "invalidweight", "unreliableweight", "missingtrustedweight"))
            return FailureKind.InputWeight;
        if (ContainsAny(
                code,
                "missingliftpoint",
                "invalidliftgeometry",
                "missinggeometry",
                "incompletegeometry",
                "missingelementinformation"))
        {
            return FailureKind.InputGeometry;
        }
        if (ContainsAny(code, "novalidsetupposition", "missingsetup", "readinessblocker", "nocandidate"))
            return FailureKind.InputSetup;
        if (ContainsAny(code, "overweight", "capacitylimit", "insufficientcapacity", "capacityexceed"))
            return FailureKind.Capacity;
        if (ContainsAny(code, "outofradius", "radiuslimit", "radiusenvelope"))
            return FailureKind.Radius;
        if (ContainsAny(
                code,
                "heightfail",
                "boomlengthexceeded",
                "boomenvelope",
                "verticalreach",
                "boomunavailable"))
        {
            return FailureKind.BoomHeight;
        }
        if (ContainsAny(code, "outsidechartenvelope"))
            return FailureKind.LoadChartEnvelope;
        if (ContainsAny(
                code,
                "notcharted",
                "invalidblankcell",
                "invalidzerocell",
                "nolistedcapacity",
                "missingoperatingcondition",
                "missingchart",
                "unsupportedchartradiusboom"))
        {
            FailureKind chartKind = ClassifyChartStatus(chartStatus);
            return chartKind == FailureKind.LoadChartEnvelope
                ? FailureKind.LoadChartEnvelope
                : FailureKind.LoadChartEvidence;
        }
        if (ContainsAny(code, "inputdata", "invaliddata", "missingdata", "ifc"))
            return FailureKind.InputGeometry;
        return FailureKind.Other;
    }

    private static FailureKind ClassifyChartStatus(string? value)
    {
        string code = NormalizeCode(value);
        if (ContainsAny(code, "outsidechartenvelope"))
            return FailureKind.LoadChartEnvelope;
        if (ContainsAny(
                code,
                "invalidblankcell",
                "invalidzerocell",
                "nolistedcapacity",
                "missing",
                "invalid"))
        {
            return FailureKind.LoadChartEvidence;
        }
        return FailureKind.Other;
    }

    private static string BuildStatus(
        FailureAggregation failures,
        int unresolved,
        bool hasConsistencyWarning)
    {
        if (hasConsistencyWarning)
            return "The saved lift outcomes require review.";
        if (unresolved == 0)
            return "The current crane can complete the evaluated plan.";
        if (failures.ProvenCraneFailures > 0 && failures.HasInputOrOther)
            return "Some lifts exceed the evaluated crane limits, while others require corrected input information.";
        if (failures.ProvenCraneFailures > 0 && failures.ChartEvidence > 0)
            return "Some lifts exceed the evaluated crane limits, while others require corrected load-chart information.";
        if (failures.ProvenCraneFailures > 0)
            return "The current crane cannot complete all evaluated lifts.";
        if (failures.ChartEvidence > 0)
            return "Crane suitability cannot be confirmed because the selected load-chart data is incomplete.";
        return "Crane suitability cannot be confirmed until the missing lift information is corrected.";
    }

    private static string BuildExecutiveSummary(
        int total,
        int completed,
        int unresolved,
        string coverage,
        string dominantExplanation,
        string status,
        string alternativeSummary,
        string primaryRecommendation)
    {
        string result = unresolved == 0
            ? $"{total:N0} lifts were evaluated and all {completed:N0} were completed, giving {coverage} lift coverage."
            : $"{total:N0} lifts were evaluated: {completed:N0} were completed and {unresolved:N0} remain unresolved, giving {coverage} lift coverage.";
        string cause = unresolved == 0 ? string.Empty : $" {dominantExplanation}";
        return $"{result}{cause} {status} {alternativeSummary} {primaryRecommendation}".Trim();
    }

    private static string BuildPrimaryRecommendation(FailureAggregation failures, int unresolved)
    {
        if (unresolved == 0)
            return "Proceed to qualified engineering review and verify the plan against manufacturer data.";
        if (failures.ProvenCraneFailures > 0 && failures.HasInputOrOther)
            return "Correct incomplete lift information first, then review the proven crane-limit failures and rerun the optimizer.";
        if (failures.ProvenCraneFailures > 0 && failures.ChartEvidence > 0)
            return "Correct the incomplete load-chart information first, then review the proven crane-limit failures and rerun the optimizer.";
        return failures.Dominant switch
        {
            FailureKind.Capacity =>
                "Review a crane with greater effective capacity at the required working conditions, then rerun the optimizer.",
            FailureKind.Radius =>
                "Review the available crane setup areas or evaluate a crane with greater working reach, then rerun the optimizer.",
            FailureKind.BoomHeight =>
                "Review a crane or configuration with sufficient boom length and vertical reach, then rerun the optimizer.",
            FailureKind.LoadChartEnvelope or FailureKind.LoadChartEvidence =>
                "Review or correct the selected crane load chart before changing the crane.",
            FailureKind.InputWeight or FailureKind.InputCenterOfGravity or FailureKind.InputGeometry
                or FailureKind.InputSetup =>
                "Correct the missing or unreliable lift information before changing crane size.",
            _ => "Review the unresolved lifting information before changing the crane."
        };
    }

    private static IEnumerable<string> BuildSecondaryRecommendations(
        int nearLimitCount,
        double threshold,
        FailureAggregation failures)
    {
        if (nearLimitCount > 0)
            yield return $"Review the {nearLimitCount:N0} completed lifts operating near the configured {threshold:P0} capacity-use advisory threshold.";
        if (failures.ChartEvidence > 0)
            yield return "Complete the missing crane chart information before comparing replacement cranes.";
        if (failures.ProvenCraneFailures > 0)
            yield return "Confirm any crane or setup change with a full optimizer run and qualified engineering review.";
    }

    private static IEnumerable<GaLiftSummaryCandidateRow> BuildCandidateRows(
        GaLiftAnalysisResult analysis,
        FailureAggregation failures)
    {
        if (failures.HasUnreliableEvidence)
            return Array.Empty<GaLiftSummaryCandidateRow>();

        return analysis.CandidateResults
            .Where(candidate =>
                candidate.IsValidatedAlternative ||
                candidate.RecoveredFailedLiftCount > 0 ||
                candidate.IsMeaningfullySmaller)
            .OrderByDescending(candidate => candidate.IsValidatedAlternative)
            .ThenByDescending(candidate => candidate.RecoveredFailedLiftCount)
            .ThenBy(candidate => candidate.NominalCapacityTon)
            .Take(3)
            .Select(candidate => new GaLiftSummaryCandidateRow
            {
                Candidate = CleanCraneName(candidate.CraneName),
                RatedCapacity = $"{candidate.NominalCapacityTon:N1} t",
                SuccessfulPlanPreserved = candidate.PreservesSuccessfulPlan ? "Yes" : "No",
                FailedLiftsRecovered = Number(candidate.RecoveredFailedLiftCount),
                Result = candidate.IsValidatedAlternative
                    ? "Candidate available"
                    : "No complete recovery",
                Recommendation = "Rerun the optimizer before adoption."
            });
    }

    private static string BuildAlternativeSummary(
        AlternativeScreeningOutcome outcome,
        FailureAggregation failures,
        IReadOnlyList<GaLiftSummaryCandidateRow> rows)
    {
        if (failures.HasInputOrOther)
            return "Alternative cranes cannot be evaluated reliably until the missing lift information is corrected.";
        if (failures.ChartEvidence > 0)
            return "Alternative cranes cannot be evaluated reliably until the missing load-chart information is corrected.";
        if (rows.Count > 0)
            return "A replacement candidate is available for further evaluation.";
        if (outcome == AlternativeScreeningOutcome.NoValidatedAlternative)
            return "No validated replacement crane was found for the evaluated plan.";
        return "No reliable replacement-crane conclusion is available for this run.";
    }

    private static string BuildAlternativesMessage(
        AlternativeScreeningOutcome outcome,
        FailureAggregation failures,
        IReadOnlyList<GaLiftSummaryCandidateRow> rows)
    {
        if (rows.Count > 0)
            return string.Empty;
        if (failures.HasInputOrOther)
            return "Alternative cranes cannot be evaluated reliably until the missing lift information is corrected.";
        if (failures.ChartEvidence > 0)
            return "Alternative cranes cannot be evaluated reliably until the missing load-chart information is corrected.";
        if (outcome == AlternativeScreeningOutcome.NoValidatedAlternative)
        {
            return "No validated replacement crane was found for the evaluated plan. The screened candidates did not both preserve the successful lifting plan and recover all confirmed crane-related failures.";
        }
        return "Alternative screening did not provide a reliable replacement conclusion for this run.";
    }

    private static IEnumerable<string> BuildLimitations(
        GaLiftAnalysisResult analysis,
        bool hasConsistencyWarning)
    {
        if (string.Equals(
                analysis.GeometrySource,
                "RunSettingsSharedOffsets",
                StringComparison.OrdinalIgnoreCase))
        {
            yield return "Alternative crane checks used the same crane-geometry assumptions saved with the original optimizer run because candidate-specific geometry data was unavailable.";
        }
        else
        {
            yield return "Lift geometry reflects the assumptions saved with the completed optimizer run.";
        }

        if (analysis.Confidence == GaLiftAnalysisConfidence.Low)
            yield return "The available evidence supports only a low-confidence summary.";
        if (hasConsistencyWarning)
            yield return "The saved lift totals do not fully reconcile and require review.";
    }

    private static string BuildCopyText(
        GaLiftSummaryPresentationModel model,
        FailureAggregation failures,
        string alternativeSummary)
    {
        List<string> lines =
        [
            "Lift Summary",
            $"Project: {model.ProjectName}",
            $"Crane: {model.CraneName}",
            $"Run date: {model.RunDateText}",
            $"Total lifts: {model.TotalLiftsText}",
            $"Completed lifts: {model.CompletedLiftsText}",
            $"Unresolved lifts: {model.UnresolvedLiftsText}",
            $"Lift coverage: {model.CoverageText}",
            $"Highest capacity use: {model.HighestCapacityUseText}",
            $"Average capacity use: {model.AverageCapacityUseText}",
            $"Smallest capacity reserve: {model.SmallestCapacityReserveText}",
            $"Lifts near capacity limit: {model.NearCapacityLimitText}",
            $"Capacity failures: {model.CapacityFailuresText}",
            $"Radius failures: {model.RadiusFailuresText}",
            $"Boom or height failures: {model.BoomHeightFailuresText}",
            $"Load chart failures: {model.LoadChartFailuresText}"
        ];
        if (failures.Input > 0)
            lines.Add($"Input data issues: {model.InputDataIssuesText}");
        if (failures.Other > 0)
            lines.Add($"Other unresolved issues: {model.OtherUnresolvedIssuesText}");
        lines.Add($"Dominant failure: {model.DominantFailureExplanationText}");
        lines.Add($"Safety factor: {model.SafetyFactorText}");
        lines.Add($"Derating ratio: {model.DeratingRatioText}");
        lines.Add($"Candidate points generated: {model.CandidatePointsGeneratedText}");
        lines.Add($"Valid candidate points: {model.ValidCandidatePointsText}");
        lines.Add($"Rejected candidate points: {model.RejectedCandidatePointsText}");
        lines.Add($"Selected crane positions: {model.SelectedCranePositionsText}");
        lines.Add($"Recommended action: {model.PrimaryRecommendationText}");
        if (model.HasAlternativesSection)
            lines.Add($"Crane alternatives: {alternativeSummary}");
        lines.Add($"Safety limitation: {model.SafetyNotice}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string DominantTitle(FailureKind kind, int unresolved)
    {
        if (unresolved == 0)
            return "No Unresolved Failures";
        return kind switch
        {
            FailureKind.Capacity => "Dominant Failure: Insufficient Capacity",
            FailureKind.Radius => "Dominant Failure: Outside Working Radius",
            FailureKind.BoomHeight => "Dominant Failure: Boom or Height Requirement",
            FailureKind.LoadChartEnvelope or FailureKind.LoadChartEvidence =>
                "Dominant Failure: Load Chart Coverage",
            FailureKind.InputWeight or FailureKind.InputCenterOfGravity or FailureKind.InputGeometry
                or FailureKind.InputSetup => "Dominant Failure: Missing or Unreliable Lift Data",
            _ => "Dominant Failure: Other Unresolved Conditions"
        };
    }

    private static string DominantExplanation(FailureKind kind, int unresolved)
    {
        if (unresolved == 0)
            return "No unresolved failure category controls this completed run.";
        return kind switch
        {
            FailureKind.Capacity =>
                "Most unresolved lifts require more effective capacity than the current crane provides at their evaluated working conditions.",
            FailureKind.Radius =>
                "Most unresolved lifts are outside the usable working radius from the evaluated crane setup positions.",
            FailureKind.BoomHeight =>
                "Most unresolved lifts require a boom configuration or vertical reach outside the crane's supported evaluated range.",
            FailureKind.LoadChartEnvelope or FailureKind.LoadChartEvidence =>
                "Most unresolved lifts could not be validated within the selected crane's available load-chart data.",
            FailureKind.InputWeight or FailureKind.InputCenterOfGravity or FailureKind.InputGeometry
                or FailureKind.InputSetup =>
                "Required lifting information is incomplete or unreliable, so crane suitability cannot be confirmed for these lifts.",
            _ => "Some unresolved outcomes could not be assigned to a more specific user-facing category."
        };
    }

    private static string BuildInputDescription(FailureAggregation failures)
    {
        (FailureKind Kind, int Count)[] values =
        [
            (FailureKind.InputWeight, failures.InputWeight),
            (FailureKind.InputCenterOfGravity, failures.InputCenterOfGravity),
            (FailureKind.InputGeometry, failures.InputGeometry),
            (FailureKind.InputSetup, failures.InputSetup)
        ];
        FailureKind dominant = values
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Kind)
            .First().Kind;
        return dominant switch
        {
            FailureKind.InputWeight => "Missing or unreliable weight information.",
            FailureKind.InputCenterOfGravity => "Missing center-of-gravity information.",
            FailureKind.InputGeometry => "Incomplete lifting geometry.",
            FailureKind.InputSetup => "Missing setup-position evidence.",
            _ => "Other incomplete lifting data."
        };
    }

    private static double? MaximumCompletedCapacity(IEnumerable<GaLiftAnalysisItem> lifts) =>
        lifts.Where(IsValidCompletedCapacity)
            .Select(item => item.CapacityUtilization!.Value)
            .DefaultIfEmpty(double.NaN)
            .Max() is double value && double.IsFinite(value) ? value : null;

    private static double? AverageCompletedCapacity(IEnumerable<GaLiftAnalysisItem> lifts)
    {
        double[] values = lifts.Where(IsValidCompletedCapacity)
            .Select(item => item.CapacityUtilization!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Average();
    }

    private static double? MinimumCompletedReserve(IEnumerable<GaLiftAnalysisItem> lifts)
    {
        double[] values = lifts
            .Where(item => item.IsLifted && ValidFinite(item.SafetyMarginTon).HasValue)
            .Select(item => item.SafetyMarginTon!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Min();
    }

    private static bool IsValidCompletedCapacity(GaLiftAnalysisItem item) =>
        item.IsLifted &&
        Valid(item.CapacityUtilization).HasValue &&
        ValidPositive(item.AvailableCapacityTon).HasValue;

    private static bool IsPositiveShortfall(double? required, double? available) =>
        ValidPositive(required) is { } requiredValue &&
        ValidPositive(available) is { } availableValue &&
        requiredValue > availableValue;

    private static bool IsGreaterThanOne(double? value) =>
        Valid(value) is { } valid && valid > 1.0;

    private static double? Valid(double? value) =>
        value.HasValue && double.IsFinite(value.Value) && value.Value >= 0 ? value : null;

    private static double? ValidFinite(double? value) =>
        value.HasValue && double.IsFinite(value.Value) ? value : null;

    private static double? ValidPositive(double? value) =>
        value.HasValue && double.IsFinite(value.Value) && value.Value > 0 ? value : null;

    private static bool ContainsAny(string source, params string[] values) =>
        values.Any(source.Contains);

    private static string NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string CleanCraneName(string? value)
    {
        string name = ValueOrFallback(value, "Crane not identified");
        name = Regex.Replace(
            name,
            @"\b(solved|selected\s+result|complete\s+result)\b",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        name = Regex.Replace(
            name,
            @"(?<=\d)(?=tons?\b)",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        name = Regex.Replace(name, @"\s+", " ").Trim(' ', '-', '_');
        return string.IsNullOrWhiteSpace(name) ? "Crane not identified" : name;
    }

    private static string HumanizeOperatingMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        string result = Regex.Replace(value.Trim(), "([a-z0-9])([A-Z])", "$1 $2");
        return Regex.Replace(result.Replace('_', ' '), @"\s+", " ");
    }

    private static string ShortRunReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unavailable";
        string compact = value.Replace("-", string.Empty, StringComparison.Ordinal);
        return compact.Length <= 8 ? compact : compact[..8];
    }

    private static string Number(int value) =>
        value.ToString("N0", CultureInfo.CurrentCulture);

    private static string OptionalNumber(int? value) =>
        value.HasValue ? Number(value.Value) : "Unavailable";

    private static string Percent(double? value) =>
        Valid(value) is { } valid ? valid.ToString("P1", CultureInfo.CurrentCulture) : "Unavailable";

    private static string Tonnes(double? value) =>
        ValidFinite(value) is { } valid
            ? $"{valid.ToString("N2", CultureInfo.CurrentCulture)} t"
            : "Unavailable";

    private static string Meters(double? value) =>
        ValidFinite(value) is { } valid
            ? $"{valid.ToString("N1", CultureInfo.CurrentCulture)} m"
            : "Unavailable";

    private static string Ratio(double value) =>
        double.IsFinite(value)
            ? value.ToString("0.00##", CultureInfo.CurrentCulture)
            : "Unavailable";

    private static string Duration(long milliseconds)
    {
        TimeSpan duration = TimeSpan.FromMilliseconds(milliseconds);
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes:N0} min {duration.Seconds:N0} sec";
        if (duration.TotalSeconds >= 1)
            return $"{duration.TotalSeconds:N1} sec";
        return $"{milliseconds:N0} ms";
    }

    private static string ValueOrFallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private enum FailureKind
    {
        Capacity,
        Radius,
        BoomHeight,
        LoadChartEnvelope,
        LoadChartEvidence,
        InputWeight,
        InputCenterOfGravity,
        InputGeometry,
        InputSetup,
        Other
    }

    private sealed record FailureAggregation(
        int Capacity,
        int Radius,
        int BoomHeight,
        int ChartEnvelope,
        int ChartEvidence,
        int InputWeight,
        int InputCenterOfGravity,
        int InputGeometry,
        int InputSetup,
        int Other)
    {
        public int LoadChart => ChartEnvelope + ChartEvidence;
        public int Input => InputWeight + InputCenterOfGravity + InputGeometry + InputSetup;
        public int Total => Capacity + Radius + BoomHeight + LoadChart + Input + Other;
        public int ProvenCraneFailures => Capacity + Radius + BoomHeight + ChartEnvelope;
        public bool HasInputOrOther => Input > 0 || Other > 0;
        public bool HasUnreliableEvidence => HasInputOrOther || ChartEvidence > 0;
        public FailureKind Dominant =>
            new[]
            {
                (FailureKind.Capacity, Capacity),
                (FailureKind.Radius, Radius),
                (FailureKind.BoomHeight, BoomHeight),
                (ChartEnvelope >= ChartEvidence
                    ? FailureKind.LoadChartEnvelope
                    : FailureKind.LoadChartEvidence, LoadChart),
                (DominantInputKind(), Input),
                (FailureKind.Other, Other)
            }
            .OrderByDescending(item => item.Item2)
            .ThenBy(item => item.Item1)
            .First().Item1;

        private FailureKind DominantInputKind()
        {
            return new[]
            {
                (FailureKind.InputWeight, InputWeight),
                (FailureKind.InputCenterOfGravity, InputCenterOfGravity),
                (FailureKind.InputGeometry, InputGeometry),
                (FailureKind.InputSetup, InputSetup)
            }
            .OrderByDescending(item => item.Item2)
            .ThenBy(item => item.Item1)
            .First().Item1;
        }
    }
}
