namespace CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

public sealed record GaLiftSummaryPresentationModel
{
    public string ProjectName { get; init; } = string.Empty;
    public string CraneName { get; init; } = string.Empty;
    public string RunDateText { get; init; } = string.Empty;
    public string RunReferenceText { get; init; } = string.Empty;
    public string CoverageText { get; init; } = string.Empty;
    public string SummaryStatusText { get; init; } = string.Empty;
    public string ExecutiveSummaryText { get; init; } = string.Empty;
    public string TotalLiftsText { get; init; } = string.Empty;
    public string CompletedLiftsText { get; init; } = string.Empty;
    public string UnresolvedLiftsText { get; init; } = string.Empty;
    public string HighestCapacityUseText { get; init; } = string.Empty;
    public string AverageCapacityUseText { get; init; } = string.Empty;
    public string SmallestCapacityReserveText { get; init; } = string.Empty;
    public string NearCapacityLimitText { get; init; } = string.Empty;
    public string NearCapacityLimitTooltip { get; init; } = string.Empty;
    public string CapacityFailuresText { get; init; } = string.Empty;
    public string RadiusFailuresText { get; init; } = string.Empty;
    public string BoomHeightFailuresText { get; init; } = string.Empty;
    public string LoadChartFailuresText { get; init; } = string.Empty;
    public string InputDataIssuesText { get; init; } = string.Empty;
    public string OtherUnresolvedIssuesText { get; init; } = string.Empty;
    public bool HasCapacityFailures { get; init; }
    public bool HasRadiusFailures { get; init; }
    public bool HasBoomHeightFailures { get; init; }
    public bool HasLoadChartFailures { get; init; }
    public bool HasInputDataIssues { get; init; }
    public bool HasOtherUnresolvedIssues { get; init; }
    public string InputDataIssueDescription { get; init; } = string.Empty;
    public string DominantFailureTitleText { get; init; } = string.Empty;
    public string DominantFailureExplanationText { get; init; } = string.Empty;
    public string SafetyFactorText { get; init; } = string.Empty;
    public string DeratingRatioText { get; init; } = string.Empty;
    public string WireLengthText { get; init; } = string.Empty;
    public string HookWeightText { get; init; } = string.Empty;
    public string AccessoriesWeightText { get; init; } = string.Empty;
    public string OperatingModeText { get; init; } = string.Empty;
    public bool HasOperatingMode { get; init; }
    public string CandidatePointsGeneratedText { get; init; } = string.Empty;
    public string ValidCandidatePointsText { get; init; } = string.Empty;
    public string RejectedCandidatePointsText { get; init; } = string.Empty;
    public string SelectedCranePositionsText { get; init; } = string.Empty;
    public string OptimizationTimeText { get; init; } = string.Empty;
    public bool HasOptimizationTime { get; init; }
    public string PrimaryRecommendationText { get; init; } = string.Empty;
    public IReadOnlyList<string> SecondaryRecommendations { get; init; } = Array.Empty<string>();
    public bool HasSecondaryRecommendations => SecondaryRecommendations.Count > 0;
    public bool HasAlternativesSection { get; init; }
    public bool HasAlternativeRows => AlternativeRows.Count > 0;
    public bool HasAlternativesMessage => !string.IsNullOrWhiteSpace(AlternativesMessage);
    public string AlternativesMessage { get; init; } = string.Empty;
    public IReadOnlyList<GaLiftSummaryCandidateRow> AlternativeRows { get; init; } =
        Array.Empty<GaLiftSummaryCandidateRow>();
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
    public string SafetyNotice { get; init; } = string.Empty;
    public string CopyText { get; init; } = string.Empty;
    public string? InternalConsistencyWarningCode { get; init; }
}

public sealed record GaLiftSummaryCandidateRow
{
    public string Candidate { get; init; } = string.Empty;
    public string RatedCapacity { get; init; } = string.Empty;
    public string SuccessfulPlanPreserved { get; init; } = string.Empty;
    public string FailedLiftsRecovered { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public string Recommendation { get; init; } = string.Empty;
}
