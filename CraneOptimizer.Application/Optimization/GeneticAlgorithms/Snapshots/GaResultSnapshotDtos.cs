using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;

public sealed class GaResultSnapshotDto
{
    public int SchemaVersion { get; init; } = 3;
    public string SnapshotType { get; init; } = "CraneOptimizer.GaResult";
    public string ApplicationVersion { get; init; } = string.Empty;
    public string? GitCommitHash { get; init; }
    public GaRunIdentitySnapshotDto Run { get; init; } = new();
    public GaProjectSnapshotDto Project { get; init; } = new();
    public GaModelSnapshotDto Model { get; init; } = new();
    public GaCoordinateReferenceSnapshotDto Coordinates { get; init; } = new();
    public GaSettingsSnapshotDto RequestedSettings { get; init; } = new();
    public GaSettingsSnapshotDto EffectiveFinalSettings { get; init; } = new();
    public IReadOnlyList<GaStageSummarySnapshotDto> Stages { get; init; } = Array.Empty<GaStageSummarySnapshotDto>();
    public GaCraneSnapshotDto Crane { get; init; } = new();
    public GaLiftingAccessoriesSnapshotDto LiftingAccessories { get; init; } = new();
    public GaLoadChartSnapshotDto LoadChart { get; init; } = new();
    public GaInputScopeSnapshotDto InputScope { get; init; } = new();
    public GaRunSummarySnapshotDto Summary { get; init; } = new();
    public GaPerformanceSnapshotDto? Performance { get; init; }
    public GaReachabilitySnapshotDto? Reachability { get; init; }
    public string CanonicalSnapshotId { get; init; } = string.Empty;
    public string CanonicalPayloadHash { get; init; } = string.Empty;
    public IReadOnlyList<CanonicalLiftUnitSnapshotDto> LiftUnits { get; init; } = Array.Empty<CanonicalLiftUnitSnapshotDto>();
    public IReadOnlyList<GaPositionSnapshotDto> Positions { get; init; } = Array.Empty<GaPositionSnapshotDto>();
    public IReadOnlyList<GaFinalOutcomeSnapshotDto> FinalOutcomes { get; init; } = Array.Empty<GaFinalOutcomeSnapshotDto>();
    public GaSnapshotCapabilitiesDto Capabilities { get; init; } = new();
    public GaDiagnosticsReferencesSnapshotDto Diagnostics { get; init; } = new();
    public GaIntegritySnapshotDto Integrity { get; init; } = new();
}

public sealed class GaRunIdentitySnapshotDto
{
    public string OptimizationRunId { get; init; } = string.Empty;
    public string FinalStageRunId { get; init; } = string.Empty;
    public int FinalStageIndex { get; init; }
    public string Status { get; init; } = "Completed";
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public long DurationMilliseconds { get; init; }
    public string? TerminationReason { get; init; }
    public string ApplicationVersion { get; init; } = string.Empty;
    public string? GitCommitHash { get; init; }
    public int SnapshotSchemaVersion { get; init; } = 3;
    public string? DiagnosticsSchemaVersion { get; init; }
}

public sealed class GaProjectSnapshotDto
{
    public string ProjectGuid { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string? ProjectDatabaseId { get; init; }
    public string ProjectRelativeSnapshotPath { get; init; } = string.Empty;
    public DateTimeOffset? ProjectCreatedAtUtc { get; init; }
}

public sealed class GaModelSnapshotDto
{
    public string CanonicalSnapshotId { get; init; } = string.Empty;
    public string CanonicalPayloadHash { get; init; } = string.Empty;
    public string ModelFingerprint { get; init; } = string.Empty;
    public string OptimizationInputFingerprint { get; init; } = string.Empty;
    public int OptimizationInputFingerprintVersion { get; init; }
    public IReadOnlyDictionary<string, string> OptimizationInputFingerprintComponents { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
    public string? ModelRevision { get; init; }
    public string? IfcFileName { get; init; }
    public string? IfcProjectRelativePath { get; init; }
    public string? IfcSha256 { get; init; }
    public string? IfcSchemaVersion { get; init; }
    public long? IfcFileSizeBytes { get; init; }
    public DateTimeOffset? IfcLastWriteUtc { get; init; }
    public string? ScenarioId { get; init; }
    public string? ProjectFeatureSnapshotId { get; init; }
    public string? ScopeDefinitionHash { get; init; }
}

public sealed class GaCoordinateReferenceSnapshotDto
{
    public bool CoordinateConversionEnabled { get; init; }
    public string? CoordinateSystemName { get; init; }
    public string? Epsg { get; init; }
    public GaPointSnapshotDto? LocalReferenceOrigin { get; init; }
    public GaPointSnapshotDto? SurveyOrigin { get; init; }
    public double? RotationToNorthDegrees { get; init; }
    public double Scale { get; init; } = 1.0;
    public string CanonicalLengthUnit { get; init; } = "m";
}

public sealed class GaPointSnapshotDto
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public string Frame { get; init; } = "ProjectLocalMeters";
    public string Unit { get; init; } = "m";
}

public sealed class CoordinateTransformSnapshotDto
{
    public string TransformId { get; init; } = string.Empty;
    public string SourceFrameId { get; init; } = string.Empty;
    public string TargetFrameId { get; init; } = string.Empty;
    public double OffsetXM { get; init; }
    public double OffsetYM { get; init; }
    public double OffsetZM { get; init; }
    public double RotationDegrees { get; init; }
    public double Scale { get; init; } = 1.0;
}

public sealed class CanonicalCogSnapshotDto
{
    public GaPointSnapshotDto PhysicalCogProjectM { get; init; } = new();
    public GaPointSnapshotDto? GeometricCenterProjectM { get; init; }
    public GaPointSnapshotDto? GaCalculationCogM { get; init; }
    public string PhysicalCogMethod { get; init; } = string.Empty;
    public string PhysicalCogSource { get; init; } = string.Empty;
    public string PhysicalCogConfidence { get; init; } = string.Empty;
    public bool PhysicalCogTrustedForGa { get; init; }
    public string ProjectCoordinateFrameId { get; init; } = "ProjectLocalMeters";
    public string? GaCoordinateFrameId { get; init; }
    public CoordinateTransformSnapshotDto? ProjectToGaTransform { get; init; }
}

public sealed class GaSettingsSnapshotDto
{
    public int PopulationSize { get; init; }
    public int MaxGenerations { get; init; }
    public double MutationRate { get; init; }
    public double MutationSigma { get; init; }
    public double CrossoverRate { get; init; }
    public int? RandomSeed { get; init; }
    public double WireLength { get; init; }
    public double AdditionalWt { get; init; }
    public double DeratingRatio { get; init; }
    public double SafetyFactor { get; init; }
    public int MaxCranePositions { get; init; }
    public double GridStep { get; init; }
    public double CraneFootprintRadius { get; init; }
    public double RenderedOperatingEnvelopeRadius { get; init; }
    public double MinimumRelocationDistance { get; init; }
    public double DuplicatePositionDistance { get; init; }
    public double MutationExplorationMultiplier { get; init; }
    public int PositionSamplingAttempts { get; init; }
    public double CoverageWeight { get; init; }
    public double CoverageDominanceMultiplier { get; init; }
    public double RelocationWeight { get; init; }
    public double RadiusPenaltyWeight { get; init; }
    public double RadiusQualityPenaltyWeight { get; init; }
    public double OverweightPenaltyWeight { get; init; }
    public double HeightPenaltyWeight { get; init; }
    public double RadiusStdDevPenaltyWeight { get; init; }
    public double BalancePenaltyWeight { get; init; }
    public double SimilarPositionPenaltyWeight { get; init; }
    public double RelocationDistancePenaltyWeight { get; init; }
    public double MaxRadiusForNormalisation { get; init; }
    public int EarlyStoppingGenerations { get; init; }
    public double ConvergenceThresholdPercent { get; init; }
    public bool EnableAdaptiveSigma { get; init; }
    public double SigmaCoolingExponent { get; init; }
    public double MinimumMutationSigma { get; init; }
    public bool EnableConcurrentOptimization { get; init; }
    public bool EnableReachabilitySeeding { get; init; }
    public double HotSpotSeedingRatio { get; init; }
    public bool EnableMultiStageDiscovery { get; init; }
    public double MultiStageCoverageTarget { get; init; }
    public double AdaptiveRelocationScaleFactor { get; init; }
    public bool UseHungarianAssignment { get; init; }
    public bool EnableGroupedLifts { get; init; }
    public IReadOnlyList<string> TargetElementTypes { get; init; } = Array.Empty<string>();
    public int? LoadChartNumberOfLinesConstraint { get; init; }
    public string? LoadChartOperatingConditionConstraint { get; init; }
    public double SlewOffsetX { get; init; }
    public double SlewOffsetY { get; init; }
    public double SlewOffsetZ { get; init; }
    public double BoomHingeOffsetX { get; init; }
    public double BoomHingeOffsetY { get; init; }
    public double BoomHingeOffsetZ { get; init; }
    public string OptimizationRunId { get; init; } = string.Empty;
    public string StageRunId { get; init; } = string.Empty;
    public int StageIndex { get; init; }
    public bool BatchModeEnabled { get; init; }
    public string? ExperimentBatchId { get; init; }
    public string? ExperimentName { get; init; }
    public string? ParameterSetId { get; init; }
    public string? ParameterSetName { get; init; }
    public int? RunIndex { get; init; }
    public string DiagnosticsLevel { get; init; } = string.Empty;
    public bool EnableProfiling { get; init; }

    public static GaSettingsSnapshotDto FromSettings(GASettings settings) => new()
    {
        PopulationSize = settings.PopulationSize,
        MaxGenerations = settings.MaxGenerations,
        MutationRate = settings.MutationRate,
        MutationSigma = settings.MutationSigma,
        CrossoverRate = settings.CrossoverRate,
        RandomSeed = settings.RandomSeed,
        WireLength = settings.WireLength,
        AdditionalWt = settings.AdditionalWt,
        DeratingRatio = settings.DeratingRatio,
        SafetyFactor = settings.SafetyFactor,
        MaxCranePositions = settings.MaxCranePositions,
        GridStep = settings.GridStep,
        CraneFootprintRadius = settings.CraneFootprintRadius,
        RenderedOperatingEnvelopeRadius = settings.RenderedOperatingEnvelopeRadius,
        MinimumRelocationDistance = settings.MinimumRelocationDistance,
        DuplicatePositionDistance = settings.DuplicatePositionDistance,
        MutationExplorationMultiplier = settings.MutationExplorationMultiplier,
        PositionSamplingAttempts = settings.PositionSamplingAttempts,
        CoverageWeight = settings.CoverageWeight,
        CoverageDominanceMultiplier = settings.CoverageDominanceMultiplier,
        RelocationWeight = settings.RelocationWeight,
        RadiusPenaltyWeight = settings.RadiusPenaltyWeight,
        RadiusQualityPenaltyWeight = settings.RadiusQualityPenaltyWeight,
        OverweightPenaltyWeight = settings.OverweightPenaltyWeight,
        HeightPenaltyWeight = settings.HeightPenaltyWeight,
        RadiusStdDevPenaltyWeight = settings.RadiusStdDevPenaltyWeight,
        BalancePenaltyWeight = settings.BalancePenaltyWeight,
        SimilarPositionPenaltyWeight = settings.SimilarPositionPenaltyWeight,
        RelocationDistancePenaltyWeight = settings.RelocationDistancePenaltyWeight,
        MaxRadiusForNormalisation = settings.MaxRadiusForNormalisation,
        EarlyStoppingGenerations = settings.EarlyStoppingGenerations,
        ConvergenceThresholdPercent = settings.ConvergenceThresholdPercent,
        EnableAdaptiveSigma = settings.EnableAdaptiveSigma,
        SigmaCoolingExponent = settings.SigmaCoolingExponent,
        MinimumMutationSigma = settings.MinimumMutationSigma,
        EnableConcurrentOptimization = settings.EnableConcurrentOptimization,
        EnableReachabilitySeeding = settings.EnableReachabilitySeeding,
        HotSpotSeedingRatio = settings.HotSpotSeedingRatio,
        EnableMultiStageDiscovery = settings.EnableMultiStageDiscovery,
        MultiStageCoverageTarget = settings.MultiStageCoverageTarget,
        AdaptiveRelocationScaleFactor = settings.AdaptiveRelocationScaleFactor,
        UseHungarianAssignment = settings.UseHungarianAssignment,
        EnableGroupedLifts = settings.EnableGroupedLifts,
        TargetElementTypes = settings.TargetElementTypes?.ToArray() ?? Array.Empty<string>(),
        LoadChartNumberOfLinesConstraint = settings.LoadChartNumberOfLinesConstraint,
        LoadChartOperatingConditionConstraint = settings.LoadChartOperatingConditionConstraint,
        SlewOffsetX = settings.SlewOffsetX,
        SlewOffsetY = settings.SlewOffsetY,
        SlewOffsetZ = settings.SlewOffsetZ,
        BoomHingeOffsetX = settings.BoomHingeOffsetX,
        BoomHingeOffsetY = settings.BoomHingeOffsetY,
        BoomHingeOffsetZ = settings.BoomHingeOffsetZ,
        OptimizationRunId = settings.OptimizationRunId,
        StageRunId = settings.StageRunId,
        StageIndex = settings.StageIndex,
        BatchModeEnabled = settings.BatchModeEnabled,
        ExperimentBatchId = settings.ExperimentBatchId,
        ExperimentName = settings.ExperimentName,
        ParameterSetId = settings.ParameterSetId,
        ParameterSetName = settings.ParameterSetName,
        RunIndex = settings.RunIndex,
        DiagnosticsLevel = settings.DiagnosticsLevel.ToString(),
        EnableProfiling = settings.EnableProfiling
    };
}

public sealed class GaStageSummarySnapshotDto
{
    public string OptimizationRunId { get; init; } = string.Empty;
    public string StageRunId { get; init; } = string.Empty;
    public int StageIndex { get; init; }
    public bool WasSelectedAsFinal { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset CompletedAtUtc { get; init; }
    public long DurationMilliseconds { get; init; }
    public string? TerminationReason { get; init; }
    public int MaxCranePositions { get; init; }
    public double RelocationWeight { get; init; }
    public bool UseHungarianAssignment { get; init; }
    public bool EnableGroupedLifts { get; init; }
    public int? LoadChartNumberOfLinesConstraint { get; init; }
    public string? LoadChartOperatingConditionConstraint { get; init; }
    public int? RandomSeed { get; init; }
    public int PopulationSize { get; init; }
    public int MaxGenerations { get; init; }
    public int EarlyStoppingGenerations { get; init; }
    public double FitnessScore { get; init; }
    public double CoverageRatio { get; init; }
    public int LiftedLiftUnitCount { get; init; }
    public int TotalTargetLiftUnitCount { get; init; }
    public int PositionCount { get; init; }
}

public sealed class GaCraneSnapshotDto
{
    public int? CraneId { get; init; }
    public int? CraneSpecificationId { get; init; }
    public string CraneName { get; init; } = string.Empty;
    public string? Manufacturer { get; init; }
    public string? ModelOrSpecification { get; init; }
    public string? ModelName { get; init; }
    public string? SpecificationName { get; init; }
    public string? CraneType { get; init; }
    public double? MinRadiusM { get; init; }
    public double? MaxRadiusM { get; init; }
    public double? MinBoomLengthM { get; init; }
    public double? MaxBoomLengthM { get; init; }
    public double? MaxCapacityTon { get; init; }
    public double? MaxHookHeightM { get; init; }
    public double? VehicleLengthM { get; init; }
    public double? VehicleWidthM { get; init; }
    public double? VehicleHeightM { get; init; }
    public double? OutriggerLengthM { get; init; }
    public double? OutriggerWidthM { get; init; }
    public string? OutriggerConfiguration { get; init; }
    public double? CounterWeightTon { get; init; }
    public double? WireLengthM { get; init; }
    public double? HoistWireLengthM { get; init; }
    public double? HookRiggingAdditionalWeightTon { get; init; }
    public double? DeratingRatio { get; init; }
    public double? SafetyFactor { get; init; }
    public double? CombinedCapacityFactor { get; init; }
    public string? LoadChartNameOrReference { get; init; }
    public string? LoadChartSource { get; init; }
}

public sealed class GaLiftingAccessoriesSnapshotDto
{
    public double WireRopeLengthM { get; init; }
    public double? RiggingWireRopeLengthM { get; init; }
    public double HookRiggingWeightTon { get; init; }
    public double TotalBelowHookWeightTon { get; init; }
    public double TotalLiftingAccessoriesWeightTon { get; init; }
    public int? NumberOfFallsOrLines { get; init; }
    public string RiggingConfigurationName { get; init; } = string.Empty;
    public string Completeness { get; init; } = "Partial";
    public IReadOnlyList<GaLiftingAccessoryItemSnapshotDto> Items { get; init; } = Array.Empty<GaLiftingAccessoryItemSnapshotDto>();
}

public sealed class GaLiftingAccessoryItemSnapshotDto
{
    public string ItemType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double? WeightTon { get; init; }
    public double? LengthM { get; init; }
    public int? Quantity { get; init; }
}

public sealed class GaLoadChartSnapshotDto
{
    public int? CraneSpecificationId { get; init; }
    public string? ChartNameOrSourceDescription { get; init; }
    public string? SourceFileName { get; init; }
    public string LoadChartFingerprint { get; init; } = string.Empty;
    public int TargetPointCount { get; init; }
    public int PositiveUsableCapacityCount { get; init; }
    public int DistinctVisualColumnCount { get; init; }
    public string LookupPolicy { get; init; } = "ConservativeStep";
    public int? RequestedNumberOfLinesConstraint { get; init; }
    public string? RequestedOperatingConditionConstraint { get; init; }
    public string? ResolvedOperatingSetupDescription { get; init; }
    public string DuplicateColumnResolutionPolicy { get; init; } = "ConservativeMinimumAcrossDuplicateVisualColumns";
}

public sealed class GaInputScopeSnapshotDto
{
    public int RawSceneElementCount { get; init; }
    public int RawIncludeInGaCount { get; init; }
    public int OptimizationScopeElementCount { get; init; }
    public int ReferenceGeometryExcludedCount { get; init; }
    public int SubmittedLiftUnitCount { get; init; }
    public int RawGaEligibleElementCount { get; init; }
    public int GroupedAssemblyCount { get; init; }
    public int UngroupedElementCount { get; init; }
    public int RequestedMaxCranePositions { get; init; }
    public int AllowedZoneCount { get; init; }
    public int ForbiddenZoneCount { get; init; }
    public int? CandidateSetupPointCount { get; init; }
    public int? ValidCandidatePointCount { get; init; }
    public int? RejectedCandidatePointCount { get; init; }
    public bool EnableGroupedLiftsRequested { get; init; }
    public string EffectiveLiftMode { get; init; } = "Individual";
    public IReadOnlyList<string> TargetElementTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SubmittedLiftUnitIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FeatureScopeElementIds { get; init; } = Array.Empty<string>();
}

public sealed class GaRunSummarySnapshotDto
{
    public int TotalTargetLiftUnitCount { get; init; }
    public int LiftedLiftUnitCount { get; init; }
    public int UnliftedLiftUnitCount { get; init; }
    public int RawGaEligibleElementCount { get; init; }
    public int GroupedAssemblyCount { get; init; }
    public int UngroupedElementCount { get; init; }
    public int PositionCount { get; init; }
    public double FitnessScore { get; init; }
    public double CoverageRatio { get; init; }
    public int Liftable { get; init; }
    public int OutOfRadius { get; init; }
    public int Overweight { get; init; }
    public int HeightFail { get; init; }
    public int SpecialCase { get; init; }
    public int NotCharted { get; init; }
}

public sealed class GaPerformanceSnapshotDto
{
    public int? ConfiguredGenerations { get; init; }
    public int? ActualGenerationLoopCount { get; init; }
    public int? GreedySetCoverIterationCount { get; init; }
    public long? TotalRuntimeMilliseconds { get; init; }
    public long? ElementAssignmentTimeMilliseconds { get; init; }
    public long? FitnessEvaluationTimeMilliseconds { get; init; }
    public int? CapacityLookupCount { get; init; }
    public int? CapacityCacheHits { get; init; }
    public int? CapacityCacheMisses { get; init; }
    public double? CapacityCacheHitRatio { get; init; }
    public int? CandidatePointsGenerated { get; init; }
    public int? ValidPointsGenerated { get; init; }
    public long? MemoryBeforeBytes { get; init; }
    public long? MemoryAfterBytes { get; init; }
    public long? MemoryDeltaBytes { get; init; }
    public bool? StoppedByConvergence { get; init; }
    public bool? StoppedByMaxGenerations { get; init; }
}

public sealed class GaReachabilitySnapshotDto
{
    public int? TotalCandidatePoints { get; init; }
    public int? ValidCandidatePoints { get; init; }
    public int? RejectedCandidatePoints { get; init; }
    public int? ZeroCoveragePoints { get; init; }
    public int? HotSpotCount { get; init; }
    public int? MaximumHotSpotCoverage { get; init; }
    public double? AverageHotSpotCoverage { get; init; }
    public double? ReachabilityMean { get; init; }
    public double? ReachabilityStandardDeviation { get; init; }
}

public sealed record CurrentCanonicalModelSnapshotDto
{
    public string CanonicalSnapshotId { get; init; } = string.Empty;
    public string ProjectGuid { get; init; } = string.Empty;
    public string ModelFingerprint { get; init; } = string.Empty;
    public string? ModelRevision { get; init; }
    public string LiftMode { get; init; } = string.Empty;
    public string LiftUnitDefinitionVersion { get; init; } = string.Empty;
    public string CanonicalPayloadHash { get; init; } = string.Empty;
    public IReadOnlyList<CanonicalLiftUnitSnapshotDto> CanonicalLiftUnits { get; init; } = Array.Empty<CanonicalLiftUnitSnapshotDto>();
    public IReadOnlyList<ImportAnomalySnapshotDto> ImportAnomalies { get; init; } = Array.Empty<ImportAnomalySnapshotDto>();
}

public sealed record ImportAnomalySnapshotDto
{
    public string SourceObjectId { get; init; } = string.Empty;
    public string IfcGlobalId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string RawIfcType { get; init; } = string.Empty;
    public string AnomalyCode { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public string SourceFile { get; init; } = string.Empty;
    public DateTimeOffset DetectedAt { get; init; }
}

public sealed record CanonicalLiftUnitSnapshotDto
{
    public string LiftUnitId { get; init; } = string.Empty;
    public string? SourceElementGlobalId { get; init; }
    public string ElementName { get; init; } = string.Empty;
    public string ElementType { get; init; } = string.Empty;
    public string? RawIfcType { get; init; }
    public string? ResolvedCategory { get; init; }
    public string? Material { get; init; }
    public string MaterialType { get; init; } = string.Empty;
    public string? MaterialGrade { get; init; }
    public string? RawMaterialValue { get; init; }
    public string MaterialSource { get; init; } = string.Empty;
    public string? StoreyOrElevation { get; init; }
    public string ElementMark { get; init; } = string.Empty;
    public string? CanonicalMark { get; init; }
    public string? AssemblyMark { get; init; }
    public string? MarkSource { get; init; }
    public string? MarkConfidence { get; init; }
    public bool IsExplicitMark { get; init; }
    public string LiftUnitKind { get; init; } = string.Empty;
    public string RoleAtRunTime { get; init; } = string.Empty;
    public bool IsAssemblyGroup { get; init; }
    public string? ParentAssemblyGlobalId { get; init; }
    public string? MainPartGlobalId { get; init; }
    public IReadOnlyList<string> AssemblyMemberIds { get; init; } = Array.Empty<string>();
    public double? LengthM { get; init; }
    public double? WidthM { get; init; }
    public double? HeightM { get; init; }
    public double? VolumeM3 { get; init; }
    public double? LiftWeightTon { get; init; }
    public CanonicalCogSnapshotDto Cog { get; init; } = new();
    public GaPointSnapshotDto? LiftCogLocal { get; init; }
    public GaPointSnapshotDto? LiftCogSurvey { get; init; }
    public int AssemblyMemberCount { get; init; }
    public string DimensionSource { get; init; } = string.Empty;
    public string DimensionMethod { get; init; } = string.Empty;
    public string LiftUnitDefinitionVersion { get; init; } = string.Empty;
    public string CogMethod { get; init; } = string.Empty;
    public string CogConfidence { get; init; } = string.Empty;
    public bool CogTrustedForGa { get; init; }
    public bool ImportIncludeInGaFlag { get; init; }
    public bool PassedFinalReadiness { get; init; }
    public bool WasSubmittedToGa { get; init; }
    public string? WeightSource { get; init; }
    public string? WeightTrustLevel { get; init; }
    public string? ValidationStatus { get; init; }
    public IReadOnlyList<string> ValidationWarnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();
    public string SharedDataHash { get; init; } = string.Empty;
}

public sealed class GaPositionSnapshotDto
{
    public string PositionId { get; init; } = string.Empty;
    public int SetupIndex { get; init; }
    public string SetupReference { get; init; } = string.Empty;
    public int? OriginalCandidatePointIndex { get; init; }
    public GaPointSnapshotDto LocalPosition { get; init; } = new();
    public GaPointSnapshotDto? SurveyPosition { get; init; }
    public string? ZoneId { get; init; }
    public string? ZoneName { get; init; }
    public int NewLiftableCount { get; init; }
    public int FinalAssignedCount { get; init; }
    public IReadOnlyList<string> FinalAssignedLiftUnitIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<GaElementEvaluationSnapshotDto> Evaluations { get; init; } = Array.Empty<GaElementEvaluationSnapshotDto>();
}

public sealed class GaElementEvaluationSnapshotDto
{
    public string EvaluationId { get; init; } = string.Empty;
    public string LiftUnitId { get; init; } = string.Empty;
    public string CandidatePointId { get; init; } = string.Empty;
    public int CandidatePointIndex { get; init; }
    public string LiftStatus { get; init; } = string.Empty;
    public string ReachStatus { get; init; } = string.Empty;
    public string ChartStatus { get; init; } = string.Empty;
    public double? RadiusM { get; init; }
    public double? RequiredRadiusM { get; init; }
    public double? SelectedChartRadiusM { get; init; }
    public double? BoomLengthM { get; init; }
    public double? RequiredBoomLengthM { get; init; }
    public double? SelectedChartBoomLengthM { get; init; }
    public double? ElementWeightTon { get; init; }
    public double? HookRiggingWeightTon { get; init; }
    public double? RequiredCapacityTon { get; init; }
    public double? RawChartCapacityTon { get; init; }
    public double? AvailableCapacityTon { get; init; }
    public double? SafetyMarginTon { get; init; }
    public double? CapacityUtilizationPercent { get; init; }
    public double? DeratingRatio { get; init; }
    public double? SafetyFactor { get; init; }
    public double? CombinedCapacityFactor { get; init; }
    public string? LookupMode { get; init; }
    public bool IsExactMatch { get; init; }
    public bool IsSteppedRadius { get; init; }
    public bool IsBlankCell { get; init; }
    public bool IsZeroCell { get; init; }
    public int? SelectedVisualColumnIndex { get; init; }
    public string? SelectedVisualColumnHeader { get; init; }
    public int? SelectedNumberOfLines { get; init; }
    public string? SelectedOperatingConditionLabel { get; init; }
    public bool IsConservativeDuplicateFallback { get; init; }
    public int DuplicateAlternativeCount { get; init; }
    public string? DuplicateAlternativeSummary { get; init; }
    public string? CandidateBoomsChecked { get; init; }
    public string? RejectedCandidates { get; init; }
    public GaPointSnapshotDto? CraneBaseLocal { get; init; }
    public GaPointSnapshotDto? SlewCenterLocal { get; init; }
    public GaPointSnapshotDto? BoomHingeLocal { get; init; }
    public GaPointSnapshotDto? LoadPointLocal { get; init; }
    public GaPointSnapshotDto? CraneBaseSurvey { get; init; }
    public GaPointSnapshotDto? SlewCenterSurvey { get; init; }
    public GaPointSnapshotDto? BoomHingeSurvey { get; init; }
    public GaPointSnapshotDto? LoadPointSurvey { get; init; }
    public string? RadiusOriginType { get; init; }
    public string? BoomOriginType { get; init; }
    public bool IsFeasibleFromPoint { get; init; }
    public bool IsFinalAssigned { get; init; }
    public bool IsFinalAssignedToPoint { get; init; }
    public string? AssignedPointId { get; init; }
    public int? AssignedPointIndex { get; init; }
    public string? FinalOwnerPointId { get; init; }
    public string? AssignmentReason { get; init; }
    public double? AssignmentScore { get; init; }
    public bool DuplicateAssignmentResolved { get; init; }
    public IReadOnlyList<string> FeasiblePointIds { get; init; } = Array.Empty<string>();
    public bool HasBestFailedCandidate { get; init; }
    public string? BestFailedPointId { get; init; }
    public string? BestFailedLiftStatus { get; init; }
    public string? BestFailedReachStatus { get; init; }
    public string? BestFailedChartStatus { get; init; }
    public double? BestFailedSafetyMarginTon { get; init; }
    public double? BestFailedRequiredRadiusM { get; init; }
    public double? BestFailedRequiredBoomLengthM { get; init; }
    public string? BestFailureReason { get; init; }
    public string? FailureReason { get; init; }
    public string? FailureCategory { get; init; }
    public string? SuggestedAction { get; init; }
}

public sealed class GaFinalOutcomeSnapshotDto
{
    public string LiftUnitId { get; init; } = string.Empty;
    public bool IsLifted { get; init; }
    public string? FinalEvaluationId { get; init; }
    public string? FinalOwnerPositionId { get; init; }
    public string? BestFailedEvaluationId { get; init; }
    public string FinalLiftStatus { get; init; } = string.Empty;
    public string? FailureReason { get; init; }
    public string? FailureCategory { get; init; }
    public string? SuggestedAction { get; init; }
}

public sealed class GaSnapshotCapabilitiesDto
{
    public bool HasFinalAssignments { get; init; }
    public bool HasPositionEvaluationMatrix { get; init; }
    public bool HasFailureEvidence { get; init; }
    public bool HasLoadChartProvenance { get; init; }
    public bool HasAssemblyMetadata { get; init; }
    public bool HasStageSummaries { get; init; }
    public bool HasPerformanceMetrics { get; init; }
    public bool HasReachabilityMetrics { get; init; }
    public bool HasSurveyCoordinates { get; init; }
    public bool SupportsSelectedPointFeasibility { get; init; }
    public bool SupportsAssignmentReview { get; init; }
    public bool SupportsAllFeasibleDebug { get; init; }
    public string SourceKind { get; init; } = "ProductionJson";

    public static GaSnapshotCapabilitiesDto Production(bool hasPerformance, bool hasReachability, bool hasSurvey) => new()
    {
        HasFinalAssignments = true,
        HasPositionEvaluationMatrix = true,
        HasFailureEvidence = true,
        HasLoadChartProvenance = true,
        HasAssemblyMetadata = true,
        HasStageSummaries = true,
        HasPerformanceMetrics = hasPerformance,
        HasReachabilityMetrics = hasReachability,
        HasSurveyCoordinates = hasSurvey,
        SupportsSelectedPointFeasibility = true,
        SupportsAssignmentReview = true,
        SupportsAllFeasibleDebug = true,
        SourceKind = "ProductionJson"
    };
}

public sealed class GaDiagnosticsReferencesSnapshotDto
{
    public IReadOnlyList<string> ProjectRelativeFiles { get; init; } = Array.Empty<string>();
    public string? ProfilingLogProjectRelativePath { get; init; }
}

public sealed class GaIntegritySnapshotDto
{
    public string HashAlgorithm { get; init; } = "SHA-256";
    public string CanonicalUnitContract { get; init; } = "m,m2,metric-ton,degrees,ratio,percent";
    public int LiftUnitCount { get; init; }
    public int PositionCount { get; init; }
    public int EvaluationCount { get; init; }
    public int FinalOutcomeCount { get; init; }
    public DateTimeOffset ValidatedAtUtc { get; init; }
    public string ValidatorVersion { get; init; } = "1";
}

public sealed class GaResultManifestDto
{
    public int SchemaVersion { get; init; } = 1;
    public string OptimizationRunId { get; init; } = string.Empty;
    public string ProjectGuid { get; init; } = string.Empty;
    public string ModelFingerprint { get; init; } = string.Empty;
    public string? IfcSha256 { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public string CraneName { get; init; } = string.Empty;
    public string EffectiveLiftMode { get; init; } = string.Empty;
    public int TotalTargetLiftUnitCount { get; init; }
    public int LiftedLiftUnitCount { get; init; }
    public double CoverageRatio { get; init; }
    public int PositionCount { get; init; }
    public long SnapshotSizeBytes { get; init; }
    public GaSnapshotCapabilitiesDto Capabilities { get; init; } = new();
}

public sealed class GaRunDescriptorDto
{
    public string OptimizationRunId { get; init; } = string.Empty;
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ProjectGuid { get; init; } = string.Empty;
    public string ModelFingerprint { get; init; } = string.Empty;
    public string? IfcSha256 { get; init; }
    public double CoverageRatio { get; init; }
    public int LiftedLiftUnitCount { get; init; }
    public int TotalTargetLiftUnitCount { get; init; }
    public int SetupCount { get; init; }
    public string CraneName { get; init; } = string.Empty;
    public string EffectiveLiftMode { get; init; } = string.Empty;
    public string Classification { get; init; } = string.Empty;
    public string SnapshotPath { get; init; } = string.Empty;
    public GaSnapshotCapabilitiesDto Capabilities { get; init; } = new();
    public string SourceKind { get; init; } = "ProductionJson";
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class GaSnapshotValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class ResolvedGaResultSnapshot
{
    public GaResultSnapshotDto? Snapshot { get; init; }
    public string SourceKind { get; init; } = string.Empty;
    public string Classification { get; init; } = string.Empty;
    public GaSnapshotCapabilitiesDto Capabilities { get; init; } = new();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public GaRunDescriptorDto? SelectedRun { get; init; }
}

public sealed record GaProjectActivationContext(
    string ProjectRoot,
    string ProjectGuid,
    string ProjectName,
    string? IfcPath,
    string? CurrentModelFingerprint,
    string? CurrentIfcSha256,
    long? ModelRevision);

public sealed record GaSnapshotClassificationResult(
    string Classification,
    IReadOnlyList<string> Warnings)
{
    public bool IsUsableCompleted(string status, string sourceKind) =>
        string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
        && Classification is "Current" or "CurrentByIfcHash" or "CurrentProjectUnverified" or "Historic"
        && sourceKind is "ProductionJson" or "LegacyCsv" or "PartialLegacy";
}

public sealed record GaProjectRunState(
    string ProjectGuid,
    IReadOnlyList<GaRunDescriptorDto> Runs,
    string? SelectedOptimizationRunId,
    string SelectedClassification,
    IReadOnlyList<string> Warnings,
    string DiscoveryStatus,
    string? DiscoveryError);

public sealed record GaCurrentModelIdentity(
    string? ModelFingerprint,
    string? IfcSha256,
    string? IfcFileName,
    string? IfcRelativePath,
    long? FileSizeBytes,
    DateTimeOffset? LastWriteUtc,
    long? ModelRevision,
    string IdentityMethodVersion);
