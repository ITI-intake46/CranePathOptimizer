using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;

namespace CraneOptimizer.Application.GAAnalysis;

public enum CurrentCraneAdequacy
{
    Suitable,
    Borderline,
    Inadequate,
    Inconclusive
}

public enum RightSizingAssessment
{
    Justified,
    PotentiallyOversized,
    Inconclusive,
    NotEvaluated
}

public enum AlternativeScreeningOutcome
{
    NotRun,
    NoValidatedAlternative,
    SmallerCandidateAvailable,
    LargerOrLongerReachCandidateAvailable,
    BetterCandidateRecommended,
    Inconclusive,
    Cancelled
}

public enum GaLiftAnalysisConfidence
{
    High,
    Medium,
    Low
}

public sealed class GaLiftAnalysisRequest
{
    public required GaResultSnapshotDto Snapshot { get; init; }
    public IReadOnlyList<GaCraneCandidate> Candidates { get; init; } = Array.Empty<GaCraneCandidate>();
    public bool RunAlternativeScreening { get; init; } = true;
    public string? AlternativeScreeningUnavailableReason { get; init; }
}

public sealed record GaCraneCandidate(
    GaCraneCandidateSpecification Specification,
    string DisplayName);

public sealed record GaCraneCandidateSpecification
{
    public int Id { get; init; }
    public string CraneName { get; init; } = string.Empty;
    public double MaxCapacity { get; init; }
    public double MaxRadius { get; init; }
    public double MinRadius { get; init; }
    public double MaxBoomLength { get; init; }
    public double MinBoomLength { get; init; }
    public double CounterWeight { get; init; }
    public double? MachineRotation { get; init; }
    public double? CraneDerating { get; init; }
    public string? OutriggerConfig { get; init; }
    public string? LoadChartStorageMode { get; init; }
    public string? LoadChartCapacityPolicy { get; init; }
    public string? ExactLoadChartRawGridJson { get; init; }
    public string? LoadChartSourceDescription { get; init; }
    public IReadOnlyList<GaCraneCandidateLoadChartPoint> LoadChart { get; init; }
        = Array.Empty<GaCraneCandidateLoadChartPoint>();
}

public sealed record GaCraneCandidateLoadChartPoint
{
    public double Radius { get; init; }
    public double BoomLength { get; init; }
    public double Capacity { get; init; }
    public int? VisualColumnIndex { get; init; }
    public string? VisualColumnHeader { get; init; }
    public int? NumberOfLines { get; init; }
    public string? OperatingConditionLabel { get; init; }
}

public sealed class GaLiftAnalysisResult
{
    public int SchemaVersion { get; init; } = 1;
    public string AnalysisType { get; init; } = "ga-lift-performance-analysis";
    public string AnalysisEngineId { get; init; } = GaLiftAnalysisRules.EngineId;
    public string AnalysisEngineVersion { get; init; } = GaLiftAnalysisRules.EngineVersion;
    public string RulesProfileId { get; init; } = GaLiftAnalysisRules.ProfileId;
    public string RulesPolicyHash { get; init; } = GaLiftAnalysisRules.PolicyHash;
    public string CatalogFingerprint { get; init; } = string.Empty;
    public string RevisionId { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public string ProjectGuid { get; init; } = string.Empty;
    public string OptimizationRunId { get; init; } = string.Empty;
    public GaAnalysisRunContext RunContext { get; init; } = new();
    public int SourceSnapshotSchemaVersion { get; init; }
    public string SourceSnapshotFingerprint { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> SourceFingerprintComponents { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
    public CurrentCraneAdequacy CurrentCraneAdequacy { get; init; }
    public RightSizingAssessment RightSizingAssessment { get; init; }
    public AlternativeScreeningOutcome AlternativeScreeningOutcome { get; init; }
    public GaLiftAnalysisConfidence Confidence { get; init; }
    public string HeadlineCode { get; init; } = string.Empty;
    public string Headline { get; init; } = string.Empty;
    public string HeadlineQualifier { get; init; } = string.Empty;
    public GaLiftAnalysisExecutiveSummary ExecutiveSummary { get; init; } = new();
    public GaLiftAnalysisThresholds Thresholds { get; init; } = new();
    public GaLiftMetricSummary Metrics { get; init; } = new();
    public GaLiftPlanSummary LiftPlan { get; init; } = new();
    public IReadOnlyList<GaLiftAnalysisItem> LiftResults { get; init; }
        = Array.Empty<GaLiftAnalysisItem>();
    public IReadOnlyList<GaSetupAnalysisSummary> SetupSummaries { get; init; }
        = Array.Empty<GaSetupAnalysisSummary>();
    public GaRelocationAnalysisSummary Relocation { get; init; } = new();
    public GaPerformanceAnalysisSummary Performance { get; init; } = new();
    public GaAnalysisProvenance Provenance { get; init; } = new();
    public IReadOnlyList<GaLiftFailureGroup> FailureGroups { get; init; } = Array.Empty<GaLiftFailureGroup>();
    public IReadOnlyList<GaCandidateScreeningResult> CandidateResults { get; init; } = Array.Empty<GaCandidateScreeningResult>();
    public IReadOnlyList<GaTechnicalEvidenceFile> TechnicalEvidence { get; init; } = Array.Empty<GaTechnicalEvidenceFile>();
    public IReadOnlyList<GaAnalysisRecommendation> Recommendations { get; init; }
        = Array.Empty<GaAnalysisRecommendation>();
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
    public string GeometrySource { get; init; } = "RunSettingsSharedOffsets";
    public string AdvisoryNotice { get; init; } =
        "This analysis is an application-generated advisory assessment based on the saved optimizer result, crane data, load-chart evidence, and configured safety assumptions. Final crane selection and lift planning require qualified engineering review and manufacturer data verification.";
}

public sealed class GaLiftAnalysisExecutiveSummary
{
    public string OverallStatus { get; init; } = string.Empty;
    public double CoverageRatio { get; init; }
    public int SuccessfulLiftCount { get; init; }
    public int TotalLiftCount { get; init; }
    public int UnresolvedLiftCount { get; init; }
    public int SetupCount { get; init; }
    public string? DominantFailureCategory { get; init; }
    public bool ConsiderCraneChange { get; init; }
    public string RecommendedNextAction { get; init; } = string.Empty;
    public GaLiftAnalysisConfidence Confidence { get; init; }
    public string CurrencyAtGeneration { get; init; } = "Current";
    public bool TechnicalEvidenceAvailable { get; init; }
    public IReadOnlyList<string> CriticalLimitations { get; init; } = Array.Empty<string>();
}

public sealed class GaLiftAnalysisThresholds
{
    public string ProfileId { get; init; } = GaLiftAnalysisRules.ProfileId;
    public double LowCapacityUpperExclusive { get; init; } = 0.50;
    public double ModerateCapacityUpperExclusive { get; init; } = 0.80;
    public double HighCapacityUpperExclusive { get; init; } = 0.90;
    public double BorderlineUtilization { get; init; } = GaLiftAnalysisRules.BorderlineUtilization;
    public int MinimumRightSizingLiftCount { get; init; } = GaLiftAnalysisRules.MinimumRightSizingLiftCount;
    public double OversizeCapacityP95MaximumExclusive { get; init; } =
        GaLiftAnalysisRules.OversizeCapacityP95Maximum;
    public double OversizeCapacityMaximumExclusive { get; init; } =
        GaLiftAnalysisRules.OversizeCapacityMaximum;
    public double OversizeRadiusAndBoomP95MaximumExclusive { get; init; } =
        GaLiftAnalysisRules.OversizeGeometryP95Maximum;
    public double MeaningfullySmallerMaximumRatio { get; init; } =
        GaLiftAnalysisRules.MeaningfullySmallerRatio;
    public double MaterialChangeMaximumRatio { get; init; } =
        GaLiftAnalysisRules.MaterialChangeRatio;
    public double RecommendedCapacityP95MinimumInclusive { get; init; } =
        GaLiftAnalysisRules.RecommendedCapacityP95Minimum;
    public double RecommendedCapacityP95MaximumExclusive { get; init; } =
        GaLiftAnalysisRules.RecommendedCapacityP95Maximum;
    public string PercentileMethod { get; init; } = "HyndmanFanR7";
    public string Classification { get; init; } = "AdvisoryApplicationPolicy";
}

public sealed class GaLiftAnalysisItem
{
    public string LiftUnitId { get; init; } = string.Empty;
    public string? SourceElementGlobalId { get; init; }
    public string ElementName { get; init; } = string.Empty;
    public string ElementType { get; init; } = string.Empty;
    public string LiftUnitKind { get; init; } = string.Empty;
    public string? ParentAssemblyGlobalId { get; init; }
    public string? MainPartGlobalId { get; init; }
    public double? LiftWeightTon { get; init; }
    public bool IsLifted { get; init; }
    public string LiftStatus { get; init; } = string.Empty;
    public string? FinalPositionId { get; init; }
    public string? EvidenceEvaluationId { get; init; }
    public string? FailureCategory { get; init; }
    public string? FailureReason { get; init; }
    public string? SuggestedAction { get; init; }
    public double? RequiredCapacityTon { get; init; }
    public double? AvailableCapacityTon { get; init; }
    public double? SafetyMarginTon { get; init; }
    public double? CapacityUtilization { get; init; }
    public double? RequiredRadiusM { get; init; }
    public double? RadiusUtilization { get; init; }
    public double? LoadPointElevationM { get; init; }
    public double? RequiredBoomLengthM { get; init; }
    public double? BoomUtilization { get; init; }
    public string? ReachStatus { get; init; }
    public string? ChartStatus { get; init; }
}

public sealed class GaAnalysisRunContext
{
    public string ProjectName { get; init; } = string.Empty;
    public string? ModelRevision { get; init; }
    public DateTimeOffset? OptimizerCompletedAtUtc { get; init; }
    public int? CraneId { get; init; }
    public int? CraneSpecificationId { get; init; }
    public string CraneName { get; init; } = string.Empty;
    public string? CraneManufacturer { get; init; }
    public string? CraneSpecificationDescription { get; init; }
    public string? LoadChartIdentity { get; init; }
    public string? LoadChartSource { get; init; }
    public string LoadChartFingerprint { get; init; } = string.Empty;
    public string OperatingMode { get; init; } = string.Empty;
    public string DiagnosticsLevel { get; init; } = string.Empty;
    public double SafetyFactor { get; init; }
    public double DeratingRatio { get; init; }
    public double WireRopeLengthM { get; init; }
    public double HookRiggingWeightTon { get; init; }
    public double TotalLiftingAccessoriesWeightTon { get; init; }
    public GaSettingsSnapshotDto EffectiveSettings { get; init; } = new();
    public string OutputCategory { get; init; } = "Reports/Analysis";
}

public sealed class GaLiftMetricSummary
{
    public int TotalLiftCount { get; init; }
    public int SuccessfulLiftCount { get; init; }
    public int FailedLiftCount { get; init; }
    public double CoverageRatio { get; init; }
    public double? CapacityUtilizationP95 { get; init; }
    public double? CapacityUtilizationMedian { get; init; }
    public double? CapacityUtilizationAverage { get; init; }
    public double? CapacityUtilizationMaximum { get; init; }
    public double? RadiusUtilizationP95 { get; init; }
    public double? RadiusUtilizationMaximum { get; init; }
    public double? BoomUtilizationP95 { get; init; }
    public double? BoomUtilizationMaximum { get; init; }
    public string CapacityBand { get; init; } = "Unavailable";
    public int LowUtilizationLiftCount { get; init; }
    public int ModerateUtilizationLiftCount { get; init; }
    public int HighUtilizationLiftCount { get; init; }
    public int NearLimitLiftCount { get; init; }
    public double? RadiusP95M { get; init; }
    public double? RadiusMaximumM { get; init; }
    public double? BoomP95M { get; init; }
    public double? BoomMaximumM { get; init; }
    public int NearMaximumRadiusLiftCount { get; init; }
    public int NearMaximumBoomLiftCount { get; init; }
    public double? MinimumCapacityMarginTon { get; init; }
    public string? ControllingLiftUnitId { get; init; }
    public string? ControllingPositionId { get; init; }
    public string? ControllingCriterion { get; init; }
    public double? ControllingUtilization { get; init; }
    public string GoverningDemand { get; init; } = "Unavailable";
    public int ReliableCraneLimitFailureCount { get; init; }
    public int OtherFailureCount { get; init; }
}

public sealed class GaLiftPlanSummary
{
    public double? AverageLiftsPerSetup { get; init; }
    public int? MinimumLiftsPerSetup { get; init; }
    public int? MaximumLiftsPerSetup { get; init; }
    public string? MostProductivePositionId { get; init; }
    public string? LeastProductiveRetainedPositionId { get; init; }
    public string? HeaviestSuccessfulLiftUnitId { get; init; }
    public double? HeaviestSuccessfulLiftTon { get; init; }
    public string? HighestSuccessfulLiftUnitId { get; init; }
    public double? HighestSuccessfulLiftElevationM { get; init; }
    public string? LongestRadiusLiftUnitId { get; init; }
    public string? LargestBoomLiftUnitId { get; init; }
}

public sealed class GaSetupAnalysisSummary
{
    public string PositionId { get; init; } = string.Empty;
    public int SetupIndex { get; init; }
    public string SetupReference { get; init; } = string.Empty;
    public double LocalXM { get; init; }
    public double LocalYM { get; init; }
    public double LocalZM { get; init; }
    public int FinalAssignedLiftCount { get; init; }
    public int NewlyCoveredLiftCount { get; init; }
    public double? AverageRadiusM { get; init; }
    public double? MaximumRadiusM { get; init; }
    public double? AverageRequiredCapacityTon { get; init; }
    public double? MaximumRequiredCapacityTon { get; init; }
    public double? MinimumCapacityMarginTon { get; init; }
    public double? CapacityUtilizationMaximum { get; init; }
    public double? RadiusUtilizationMaximum { get; init; }
    public double? BoomUtilizationMaximum { get; init; }
    public int AssociatedFailureCount { get; init; }
    public string EfficiencyClassification { get; init; } = "Unavailable";
}

public sealed class GaRelocationAnalysisSummary
{
    public int SetupCount { get; init; }
    public int RelocationCount { get; init; }
    public double TotalRelocationDistanceM { get; init; }
    public double? MaximumRelocationDistanceM { get; init; }
}

public sealed class GaPerformanceAnalysisSummary
{
    public long RunDurationMilliseconds { get; init; }
    public int StageCount { get; init; }
    public int? ConfiguredGenerations { get; init; }
    public int? ActualGenerationLoopCount { get; init; }
    public int? GreedySetCoverIterationCount { get; init; }
    public long? ElementAssignmentTimeMilliseconds { get; init; }
    public long? FitnessEvaluationTimeMilliseconds { get; init; }
    public double? CapacityCacheHitRatio { get; init; }
    public bool? StoppedByConvergence { get; init; }
    public bool? StoppedByMaxGenerations { get; init; }
    public double FitnessScore { get; init; }
    public double CoverageRatio { get; init; }
    public int? CandidatePointsGenerated { get; init; }
    public int? ValidCandidatePoints { get; init; }
    public int? RejectedCandidatePoints { get; init; }
    public double? ValidCandidateRatio { get; init; }
    public int? ZeroCoverageCandidateCount { get; init; }
    public int? HotSpotCount { get; init; }
    public int FinalSetupPositionCount { get; init; }
    public string RunInterpretation { get; init; } = "InconclusiveMissingEvidence";
    public string? EarlyStopCondition { get; init; }
    public IReadOnlyList<double> BestFitnessProgression { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> CoverageProgression { get; init; } = Array.Empty<double>();
}

public sealed class GaAnalysisProvenance
{
    public string SourceKind { get; init; } = string.Empty;
    public string ApplicationVersion { get; init; } = string.Empty;
    public string? GitCommitHash { get; init; }
    public string SourceSnapshotProjectRelativePath { get; init; } = string.Empty;
    public string AnalysisProjectRelativePath { get; init; } = string.Empty;
    public string? IfcProjectRelativePath { get; init; }
    public string? IfcSha256 { get; init; }
    public string CanonicalSnapshotId { get; init; } = string.Empty;
    public string CanonicalPayloadHash { get; init; } = string.Empty;
}

public sealed class GaLiftFailureGroup
{
    public string Category { get; init; } = string.Empty;
    public int Count { get; init; }
    public double PercentageOfUnresolved { get; init; }
    public bool IsReliableCraneLimitEvidence { get; init; }
    public IReadOnlyList<string> LiftUnitIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TopCriticalExamples { get; init; } = Array.Empty<string>();
    public string RootCauseExplanation { get; init; } = string.Empty;
    public bool CraneChangeMayHelp { get; init; }
    public bool PositionOrZoneReviewMayHelp { get; init; }
    public bool LoadChartCorrectionRequired { get; init; }
    public bool InputDataCorrectionRequired { get; init; }
    public string RecommendedNextAction { get; init; } = string.Empty;
    public GaLiftAnalysisConfidence Confidence { get; init; }
    public string EvidenceSource { get; init; } = "FinalGaOutcome";
}

public sealed class GaCandidateScreeningResult
{
    public int CraneSpecificationId { get; init; }
    public string CraneName { get; init; } = string.Empty;
    public double NominalCapacityTon { get; init; }
    public double MinRadiusM { get; init; }
    public double MaxRadiusM { get; init; }
    public double MinBoomLengthM { get; init; }
    public double MaxBoomLengthM { get; init; }
    public bool PreservesSuccessfulPlan { get; init; }
    public int PreservedSuccessfulLiftCount { get; init; }
    public int SuccessfulLiftCount { get; init; }
    public int FailedLiftCount { get; init; }
    public int RecoveredFailedLiftCount { get; init; }
    public int ReliableCraneLimitFailureCount { get; init; }
    public bool RecoversEveryReliableCraneLimitFailure { get; init; }
    public double? MaximumCapacityUtilization { get; init; }
    public double? CapacityUtilizationP95 { get; init; }
    public double? RadiusUtilizationP95 { get; init; }
    public double? BoomUtilizationP95 { get; init; }
    public double? MinimumCapacityMarginTon { get; init; }
    public double? MaximumRequiredRadiusM { get; init; }
    public double? MaximumRequiredBoomLengthM { get; init; }
    public double CurrentPositionCoverageRatio { get; init; }
    public bool IsMeaningfullySmaller { get; init; }
    public bool IsMaterialChange { get; init; }
    public bool IsLargerOrLongerReach { get; init; }
    public bool IsValidatedAlternative { get; init; }
    public bool RequiresFullGaReoptimization { get; init; } = true;
    public int? DeterministicRank { get; init; }
    public IReadOnlyList<string> RejectionReasons { get; init; } = Array.Empty<string>();
}

public sealed class GaAlternativeScreeningResult
{
    public AlternativeScreeningOutcome Outcome { get; init; }
    public IReadOnlyList<GaCandidateScreeningResult> Candidates { get; init; } = Array.Empty<GaCandidateScreeningResult>();
}

public sealed class GaTechnicalEvidenceFile
{
    public string ProjectRelativePath { get; init; } = string.Empty;
    public bool Exists { get; init; }
    public long? SizeBytes { get; init; }
    public string? Sha256 { get; init; }
    public string? Warning { get; init; }
}

public sealed class GaAnalysisRecommendation
{
    public int Priority { get; init; }
    public string Area { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
    public string SuggestedNextAction { get; init; } = string.Empty;
    public string RelevantApplicationArea { get; init; } = string.Empty;
    public GaLiftAnalysisConfidence Confidence { get; init; }
    public string SafetyLimitation { get; init; } =
        "Advisory only; qualified engineering review and manufacturer data verification remain required.";
}

public sealed record GaLiftAnalysisPaths(
    string ProjectRoot,
    string RunDirectory,
    string AnalysisPath,
    string ChecksumPath,
    string LatestManifestPath,
    string HistoryDirectory);

public sealed class GaLiftAnalysisManifest
{
    public int SchemaVersion { get; init; } = 1;
    public string ProjectGuid { get; init; } = string.Empty;
    public string OptimizationRunId { get; init; } = string.Empty;
    public string RevisionId { get; init; } = string.Empty;
    public string AnalysisRelativePath { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; init; }
}
