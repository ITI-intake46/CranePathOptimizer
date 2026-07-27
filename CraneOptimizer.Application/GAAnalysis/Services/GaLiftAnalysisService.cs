using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;

namespace CraneOptimizer.Application.GAAnalysis;

public sealed class GaLiftAnalysisService : IGaLiftAnalysisService
{
    private readonly IGaAlternativeScreeningService _alternativeScreening;
    private readonly IGaTechnicalEvidenceProvider? _technicalEvidence;

    public GaLiftAnalysisService(
        IGaAlternativeScreeningService alternativeScreening,
        IGaTechnicalEvidenceProvider? technicalEvidence = null)
    {
        _alternativeScreening = alternativeScreening;
        _technicalEvidence = technicalEvidence;
    }

    public async Task<GaLiftAnalysisResult> AnalyzeAsync(
        GaLiftAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var snapshot = request.Snapshot ?? throw new ArgumentException("A GA result snapshot is required.", nameof(request));
        var catalogFingerprint = CatalogFingerprint(request.Candidates);
        cancellationToken.ThrowIfCancellationRequested();
        var technicalEvidenceTask = _technicalEvidence?.CollectAsync(snapshot, cancellationToken)
            ?? Task.FromResult<IReadOnlyList<GaTechnicalEvidenceFile>>(Array.Empty<GaTechnicalEvidenceFile>());

        var evaluations = snapshot.Positions
            .SelectMany(position => position.Evaluations)
            .ToDictionary(evaluation => evaluation.EvaluationId, StringComparer.Ordinal);
        var successful = snapshot.FinalOutcomes
            .Where(outcome => outcome.IsLifted && outcome.FinalEvaluationId != null)
            .Select(outcome => evaluations.GetValueOrDefault(outcome.FinalEvaluationId!))
            .Where(evaluation => evaluation != null)
            .Cast<GaElementEvaluationSnapshotDto>()
            .ToArray();
        var failures = snapshot.FinalOutcomes.Where(outcome => !outcome.IsLifted).ToArray();
        var reliableCraneLimitFailures = failures.Where(IsReliableCraneLimitFailure).ToArray();
        var failureGroups = BuildFailureGroups(failures);
        var liftResults = BuildLiftResults(snapshot, evaluations);

        var capacity = successful
            .Select(evaluation => evaluation.CapacityUtilizationPercent / 100.0)
            .Where(value => value is >= 0)
            .Select(value => value!.Value)
            .ToArray();
        var radius = Ratio(successful.Select(evaluation => evaluation.RequiredRadiusM ?? evaluation.RadiusM), snapshot.Crane.MaxRadiusM);
        var boom = Ratio(successful.Select(evaluation => evaluation.RequiredBoomLengthM ?? evaluation.BoomLengthM), snapshot.Crane.MaxBoomLengthM);
        var radiusM = Available(successful.Select(evaluation => evaluation.RequiredRadiusM ?? evaluation.RadiusM));
        var boomM = Available(successful.Select(evaluation => evaluation.RequiredBoomLengthM ?? evaluation.BoomLengthM));
        var controlling = FindControllingLift(successful, snapshot);
        var controllingPositionId = snapshot.FinalOutcomes
            .FirstOrDefault(outcome => outcome.LiftUnitId == controlling.LiftUnitId)
            ?.FinalOwnerPositionId;

        var metrics = new GaLiftMetricSummary
        {
            TotalLiftCount = snapshot.Summary.TotalTargetLiftUnitCount,
            SuccessfulLiftCount = snapshot.Summary.LiftedLiftUnitCount,
            FailedLiftCount = snapshot.Summary.UnliftedLiftUnitCount,
            CoverageRatio = snapshot.Summary.CoverageRatio,
            CapacityUtilizationP95 = GaLiftAnalysisRules.PercentileR7(capacity, 0.95),
            CapacityUtilizationMedian = GaLiftAnalysisRules.PercentileR7(capacity, 0.50),
            CapacityUtilizationAverage = Average(capacity),
            CapacityUtilizationMaximum = Maximum(capacity),
            RadiusUtilizationP95 = GaLiftAnalysisRules.PercentileR7(radius, 0.95),
            RadiusUtilizationMaximum = Maximum(radius),
            BoomUtilizationP95 = GaLiftAnalysisRules.PercentileR7(boom, 0.95),
            BoomUtilizationMaximum = Maximum(boom),
            CapacityBand = GaLiftAnalysisRules.UtilizationBand(Maximum(capacity)),
            LowUtilizationLiftCount = capacity.Count(value => value < 0.50),
            ModerateUtilizationLiftCount = capacity.Count(value => value is >= 0.50 and < 0.80),
            HighUtilizationLiftCount = capacity.Count(value => value is >= 0.80 and < 0.90),
            NearLimitLiftCount = capacity.Count(value => value >= GaLiftAnalysisRules.BorderlineUtilization),
            RadiusP95M = GaLiftAnalysisRules.PercentileR7(radiusM, 0.95),
            RadiusMaximumM = Maximum(radiusM),
            BoomP95M = GaLiftAnalysisRules.PercentileR7(boomM, 0.95),
            BoomMaximumM = Maximum(boomM),
            NearMaximumRadiusLiftCount = radius.Count(value => value >= GaLiftAnalysisRules.BorderlineUtilization),
            NearMaximumBoomLiftCount = boom.Count(value => value >= GaLiftAnalysisRules.BorderlineUtilization),
            MinimumCapacityMarginTon = Minimum(successful.Select(evaluation => evaluation.SafetyMarginTon)),
            ControllingLiftUnitId = controlling.LiftUnitId,
            ControllingPositionId = controllingPositionId,
            ControllingCriterion = controlling.Criterion,
            ControllingUtilization = controlling.Utilization,
            GoverningDemand = controlling.Criterion ?? "Unavailable",
            ReliableCraneLimitFailureCount = reliableCraneLimitFailures.Length,
            OtherFailureCount = failures.Length - reliableCraneLimitFailures.Length
        };
        var setupSummaries = BuildSetupSummaries(snapshot);
        var liftPlan = BuildLiftPlanSummary(liftResults, setupSummaries);
        var relocation = BuildRelocationSummary(snapshot.Positions);
        var performance = BuildPerformanceSummary(snapshot);

        var adequacy = AssessAdequacy(snapshot, metrics, successful.Length);
        var rightSizing = AssessRightSizing(snapshot, adequacy, metrics);
        var screening = !string.IsNullOrWhiteSpace(request.AlternativeScreeningUnavailableReason)
            ? new GaAlternativeScreeningResult { Outcome = AlternativeScreeningOutcome.Inconclusive }
            : request.RunAlternativeScreening && !snapshot.Capabilities.HasPositionEvaluationMatrix
            ? new GaAlternativeScreeningResult { Outcome = AlternativeScreeningOutcome.Inconclusive }
            : request.RunAlternativeScreening
            ? await _alternativeScreening.ScreenAsync(snapshot, adequacy, request.Candidates, cancellationToken)
                .ConfigureAwait(false)
            : new GaAlternativeScreeningResult { Outcome = AlternativeScreeningOutcome.NotRun };
        var technicalEvidence = await technicalEvidenceTask.ConfigureAwait(false);

        // Enforce the authoritative cross-dimensional constraints at the final boundary.
        if (adequacy == CurrentCraneAdequacy.Borderline && rightSizing == RightSizingAssessment.PotentiallyOversized)
            rightSizing = RightSizingAssessment.Justified;
        if ((screening.Outcome is AlternativeScreeningOutcome.SmallerCandidateAvailable
                or AlternativeScreeningOutcome.BetterCandidateRecommended)
            && adequacy is not (CurrentCraneAdequacy.Suitable or CurrentCraneAdequacy.Borderline))
        {
            screening = new GaAlternativeScreeningResult
            {
                Outcome = AlternativeScreeningOutcome.Inconclusive,
                Candidates = screening.Candidates
            };
        }
        if (screening.Outcome == AlternativeScreeningOutcome.LargerOrLongerReachCandidateAvailable
            && adequacy != CurrentCraneAdequacy.Inadequate)
        {
            screening = new GaAlternativeScreeningResult
            {
                Outcome = AlternativeScreeningOutcome.Inconclusive,
                Candidates = screening.Candidates
            };
        }

        var (headlineCode, headline, qualifier) = BuildHeadline(adequacy, rightSizing, screening.Outcome);
        var confidence = AssessConfidence(snapshot, metrics);
        var generatedAt = DateTimeOffset.UtcNow;
        var fingerprint = string.IsNullOrWhiteSpace(snapshot.Model.OptimizationInputFingerprint)
            ? snapshot.Model.ModelFingerprint
            : snapshot.Model.OptimizationInputFingerprint;
        var revisionId = RevisionId(snapshot, adequacy, rightSizing, screening, catalogFingerprint);
        var limitations = BuildLimitations(snapshot, request, successful.Length);
        var recommendations = BuildRecommendations(
            headlineCode,
            qualifier,
            confidence,
            failureGroups,
            screening);
        var dominantFailure = failureGroups.FirstOrDefault()?.Category;

        return new GaLiftAnalysisResult
        {
            RevisionId = revisionId,
            RulesPolicyHash = GaLiftAnalysisRules.PolicyHash,
            CatalogFingerprint = catalogFingerprint,
            GeneratedAtUtc = generatedAt,
            ProjectGuid = snapshot.Project.ProjectGuid,
            OptimizationRunId = snapshot.Run.OptimizationRunId,
            RunContext = new GaAnalysisRunContext
            {
                ProjectName = snapshot.Project.ProjectName,
                ModelRevision = snapshot.Model.ModelRevision,
                OptimizerCompletedAtUtc = snapshot.Run.CompletedAtUtc,
                CraneId = snapshot.Crane.CraneId,
                CraneSpecificationId = snapshot.Crane.CraneSpecificationId,
                CraneName = snapshot.Crane.CraneName,
                CraneManufacturer = snapshot.Crane.Manufacturer,
                CraneSpecificationDescription = snapshot.Crane.ModelOrSpecification
                    ?? snapshot.Crane.SpecificationName,
                LoadChartIdentity = snapshot.Crane.LoadChartNameOrReference
                    ?? snapshot.LoadChart.ChartNameOrSourceDescription,
                LoadChartSource = snapshot.Crane.LoadChartSource
                    ?? snapshot.LoadChart.SourceFileName,
                LoadChartFingerprint = snapshot.LoadChart.LoadChartFingerprint,
                OperatingMode = snapshot.InputScope.EffectiveLiftMode,
                DiagnosticsLevel = snapshot.EffectiveFinalSettings.DiagnosticsLevel,
                SafetyFactor = snapshot.EffectiveFinalSettings.SafetyFactor,
                DeratingRatio = snapshot.EffectiveFinalSettings.DeratingRatio,
                WireRopeLengthM = snapshot.LiftingAccessories.WireRopeLengthM,
                HookRiggingWeightTon = snapshot.LiftingAccessories.HookRiggingWeightTon,
                TotalLiftingAccessoriesWeightTon =
                    snapshot.LiftingAccessories.TotalLiftingAccessoriesWeightTon,
                EffectiveSettings = snapshot.EffectiveFinalSettings
            },
            SourceSnapshotSchemaVersion = snapshot.SchemaVersion,
            SourceSnapshotFingerprint = fingerprint,
            SourceFingerprintComponents = snapshot.Model.OptimizationInputFingerprintComponents,
            CurrentCraneAdequacy = adequacy,
            RightSizingAssessment = rightSizing,
            AlternativeScreeningOutcome = screening.Outcome,
            Confidence = confidence,
            HeadlineCode = headlineCode,
            Headline = headline,
            HeadlineQualifier = qualifier,
            ExecutiveSummary = new GaLiftAnalysisExecutiveSummary
            {
                OverallStatus = headlineCode,
                CoverageRatio = metrics.CoverageRatio,
                SuccessfulLiftCount = metrics.SuccessfulLiftCount,
                TotalLiftCount = metrics.TotalLiftCount,
                UnresolvedLiftCount = metrics.FailedLiftCount,
                SetupCount = setupSummaries.Count,
                DominantFailureCategory = dominantFailure,
                ConsiderCraneChange = headlineCode is
                    "CURRENT_CRANE_INADEQUATE"
                    or "POTENTIALLY_OVERSIZED"
                    or "SMALLER_CRANE_CANDIDATE_AVAILABLE"
                    or "CHANGE_RECOMMENDED"
                    or "LARGER_OR_LONGER_REACH_CRANE_REQUIRED",
                RecommendedNextAction = recommendations.FirstOrDefault()?.SuggestedNextAction
                    ?? "Review the saved analysis evidence and limitations.",
                Confidence = confidence,
                TechnicalEvidenceAvailable = technicalEvidence.Any(item => item.Exists),
                CriticalLimitations = limitations.Take(5).ToArray()
            },
            Metrics = metrics,
            LiftPlan = liftPlan,
            LiftResults = liftResults,
            SetupSummaries = setupSummaries,
            Relocation = relocation,
            Performance = performance,
            Provenance = new GaAnalysisProvenance
            {
                SourceKind = snapshot.Capabilities.SourceKind,
                ApplicationVersion = snapshot.ApplicationVersion,
                GitCommitHash = snapshot.GitCommitHash,
                SourceSnapshotProjectRelativePath = snapshot.Project.ProjectRelativeSnapshotPath,
                AnalysisProjectRelativePath =
                    $"Reports/Analysis/{snapshot.Run.OptimizationRunId}/ga_lift_performance_analysis.json",
                IfcProjectRelativePath = snapshot.Model.IfcProjectRelativePath,
                IfcSha256 = snapshot.Model.IfcSha256,
                CanonicalSnapshotId = snapshot.CanonicalSnapshotId,
                CanonicalPayloadHash = snapshot.CanonicalPayloadHash
            },
            FailureGroups = failureGroups,
            CandidateResults = screening.Candidates,
            TechnicalEvidence = technicalEvidence,
            Recommendations = recommendations,
            Limitations = limitations
        };
    }

    private static IReadOnlyList<GaLiftAnalysisItem> BuildLiftResults(
        GaResultSnapshotDto snapshot,
        IReadOnlyDictionary<string, GaElementEvaluationSnapshotDto> evaluations)
    {
        var liftUnits = snapshot.LiftUnits.ToDictionary(unit => unit.LiftUnitId, StringComparer.Ordinal);
        return snapshot.FinalOutcomes
            .OrderBy(outcome => outcome.LiftUnitId, StringComparer.Ordinal)
            .Select(outcome =>
            {
                liftUnits.TryGetValue(outcome.LiftUnitId, out var unit);
                var evidenceId = outcome.IsLifted
                    ? outcome.FinalEvaluationId
                    : outcome.BestFailedEvaluationId;
                GaElementEvaluationSnapshotDto? evidence = null;
                if (!string.IsNullOrWhiteSpace(evidenceId))
                    evaluations.TryGetValue(evidenceId, out evidence);

                var requiredRadius = evidence?.RequiredRadiusM ?? evidence?.RadiusM;
                var requiredBoom = evidence?.RequiredBoomLengthM ?? evidence?.BoomLengthM;
                var capacityUtilization = evidence?.CapacityUtilizationPercent is >= 0
                    ? evidence.CapacityUtilizationPercent / 100.0
                    : null;

                return new GaLiftAnalysisItem
                {
                    LiftUnitId = outcome.LiftUnitId,
                    SourceElementGlobalId = unit?.SourceElementGlobalId,
                    ElementName = unit?.ElementName ?? string.Empty,
                    ElementType = unit?.ElementType ?? string.Empty,
                    LiftUnitKind = unit?.LiftUnitKind ?? string.Empty,
                    ParentAssemblyGlobalId = unit?.ParentAssemblyGlobalId,
                    MainPartGlobalId = unit?.MainPartGlobalId,
                    LiftWeightTon = unit?.LiftWeightTon ?? evidence?.ElementWeightTon,
                    IsLifted = outcome.IsLifted,
                    LiftStatus = outcome.FinalLiftStatus,
                    FinalPositionId = outcome.FinalOwnerPositionId,
                    EvidenceEvaluationId = evidenceId,
                    FailureCategory = outcome.FailureCategory ?? evidence?.FailureCategory,
                    FailureReason = outcome.FailureReason ?? evidence?.FailureReason,
                    SuggestedAction = outcome.SuggestedAction ?? evidence?.SuggestedAction,
                    RequiredCapacityTon = evidence?.RequiredCapacityTon,
                    AvailableCapacityTon = evidence?.AvailableCapacityTon,
                    SafetyMarginTon = evidence?.SafetyMarginTon,
                    CapacityUtilization = capacityUtilization,
                    RequiredRadiusM = requiredRadius,
                    RadiusUtilization = RatioValue(requiredRadius, snapshot.Crane.MaxRadiusM),
                    LoadPointElevationM = evidence?.LoadPointLocal?.Z,
                    RequiredBoomLengthM = requiredBoom,
                    BoomUtilization = RatioValue(requiredBoom, snapshot.Crane.MaxBoomLengthM),
                    ReachStatus = evidence?.ReachStatus,
                    ChartStatus = evidence?.ChartStatus
                };
            })
            .ToArray();
    }

    private static GaLiftAnalysisConfidence AssessConfidence(
        GaResultSnapshotDto snapshot,
        GaLiftMetricSummary metrics)
    {
        if (snapshot.SchemaVersion >= 3
            && snapshot.Capabilities.HasFinalAssignments
            && snapshot.Capabilities.HasPositionEvaluationMatrix
            && snapshot.Capabilities.HasLoadChartProvenance
            && metrics.SuccessfulLiftCount >= GaLiftAnalysisRules.MinimumRightSizingLiftCount
            && metrics.CapacityUtilizationP95.HasValue
            && metrics.RadiusUtilizationP95.HasValue
            && metrics.BoomUtilizationP95.HasValue
            && metrics.OtherFailureCount == 0)
        {
            return GaLiftAnalysisConfidence.High;
        }

        return snapshot.Capabilities.HasFinalAssignments && metrics.SuccessfulLiftCount > 0
            ? GaLiftAnalysisConfidence.Medium
            : GaLiftAnalysisConfidence.Low;
    }

    private static IReadOnlyList<GaSetupAnalysisSummary> BuildSetupSummaries(GaResultSnapshotDto snapshot)
    {
        var finalEvaluationIds = snapshot.FinalOutcomes
            .Where(outcome => outcome.IsLifted && !string.IsNullOrWhiteSpace(outcome.FinalEvaluationId))
            .Select(outcome => outcome.FinalEvaluationId!)
            .ToHashSet(StringComparer.Ordinal);
        var bestFailedPositionIds = snapshot.Positions
            .SelectMany(position => position.Evaluations.Select(evaluation => new
            {
                position.PositionId,
                evaluation.EvaluationId
            }))
            .ToDictionary(item => item.EvaluationId, item => item.PositionId, StringComparer.Ordinal);
        var averageAssigned = snapshot.Positions.Count == 0
            ? 0
            : snapshot.Positions.Average(position => position.FinalAssignedCount);
        var maximumAssigned = snapshot.Positions.Count == 0
            ? 0
            : snapshot.Positions.Max(position => position.FinalAssignedCount);

        return snapshot.Positions
            .OrderBy(position => position.SetupIndex)
            .Select(position =>
            {
                var assigned = position.Evaluations
                    .Where(evaluation => finalEvaluationIds.Contains(evaluation.EvaluationId))
                    .ToArray();
                var radii = Available(assigned.Select(
                    evaluation => evaluation.RequiredRadiusM ?? evaluation.RadiusM));
                var requiredCapacity = Available(assigned.Select(evaluation => evaluation.RequiredCapacityTon));
                var associatedFailureCount = snapshot.FinalOutcomes.Count(outcome =>
                    !outcome.IsLifted
                    && (outcome.FinalOwnerPositionId == position.PositionId
                        || (!string.IsNullOrWhiteSpace(outcome.BestFailedEvaluationId)
                            && bestFailedPositionIds.GetValueOrDefault(
                                outcome.BestFailedEvaluationId!) == position.PositionId)));
                return new GaSetupAnalysisSummary
                {
                    PositionId = position.PositionId,
                    SetupIndex = position.SetupIndex,
                    SetupReference = position.SetupReference,
                    LocalXM = position.LocalPosition.X,
                    LocalYM = position.LocalPosition.Y,
                    LocalZM = position.LocalPosition.Z,
                    FinalAssignedLiftCount = position.FinalAssignedCount,
                    NewlyCoveredLiftCount = position.NewLiftableCount,
                    AverageRadiusM = Average(radii),
                    MaximumRadiusM = Maximum(radii),
                    AverageRequiredCapacityTon = Average(requiredCapacity),
                    MaximumRequiredCapacityTon = Maximum(requiredCapacity),
                    MinimumCapacityMarginTon = Minimum(
                        assigned.Select(evaluation => evaluation.SafetyMarginTon)),
                    CapacityUtilizationMaximum = Maximum(assigned
                        .Select(evaluation => evaluation.CapacityUtilizationPercent / 100.0)
                        .Where(value => value is >= 0)
                        .Select(value => value!.Value)
                        .ToArray()),
                    RadiusUtilizationMaximum = Maximum(Ratio(
                        assigned.Select(evaluation => evaluation.RequiredRadiusM ?? evaluation.RadiusM),
                        snapshot.Crane.MaxRadiusM)),
                    BoomUtilizationMaximum = Maximum(Ratio(
                        assigned.Select(evaluation => evaluation.RequiredBoomLengthM ?? evaluation.BoomLengthM),
                        snapshot.Crane.MaxBoomLengthM)),
                    AssociatedFailureCount = associatedFailureCount,
                    EfficiencyClassification = position.FinalAssignedCount switch
                    {
                        0 => "EmptyRetainedSetup",
                        _ when position.FinalAssignedCount == maximumAssigned => "High",
                        _ when position.FinalAssignedCount >= averageAssigned => "Moderate",
                        _ => "Low"
                    }
                };
            })
            .ToArray();
    }

    private static GaLiftPlanSummary BuildLiftPlanSummary(
        IReadOnlyList<GaLiftAnalysisItem> liftResults,
        IReadOnlyList<GaSetupAnalysisSummary> setups)
    {
        var retained = setups.Where(setup => setup.FinalAssignedLiftCount > 0).ToArray();
        var successful = liftResults.Where(lift => lift.IsLifted).ToArray();
        var mostProductive = retained
            .OrderByDescending(setup => setup.FinalAssignedLiftCount)
            .ThenBy(setup => setup.SetupIndex)
            .FirstOrDefault();
        var leastProductive = retained
            .OrderBy(setup => setup.FinalAssignedLiftCount)
            .ThenBy(setup => setup.SetupIndex)
            .FirstOrDefault();
        var heaviest = successful
            .Where(lift => lift.LiftWeightTon.HasValue)
            .OrderByDescending(lift => lift.LiftWeightTon)
            .ThenBy(lift => lift.LiftUnitId, StringComparer.Ordinal)
            .FirstOrDefault();
        var highest = successful
            .Where(lift => lift.LoadPointElevationM.HasValue)
            .OrderByDescending(lift => lift.LoadPointElevationM)
            .ThenBy(lift => lift.LiftUnitId, StringComparer.Ordinal)
            .FirstOrDefault();
        var longestRadius = successful
            .Where(lift => lift.RequiredRadiusM.HasValue)
            .OrderByDescending(lift => lift.RequiredRadiusM)
            .ThenBy(lift => lift.LiftUnitId, StringComparer.Ordinal)
            .FirstOrDefault();
        var largestBoom = successful
            .Where(lift => lift.RequiredBoomLengthM.HasValue)
            .OrderByDescending(lift => lift.RequiredBoomLengthM)
            .ThenBy(lift => lift.LiftUnitId, StringComparer.Ordinal)
            .FirstOrDefault();

        return new GaLiftPlanSummary
        {
            AverageLiftsPerSetup = retained.Length == 0
                ? null
                : retained.Average(setup => setup.FinalAssignedLiftCount),
            MinimumLiftsPerSetup = retained.Length == 0
                ? null
                : retained.Min(setup => setup.FinalAssignedLiftCount),
            MaximumLiftsPerSetup = retained.Length == 0
                ? null
                : retained.Max(setup => setup.FinalAssignedLiftCount),
            MostProductivePositionId = mostProductive?.PositionId,
            LeastProductiveRetainedPositionId = leastProductive?.PositionId,
            HeaviestSuccessfulLiftUnitId = heaviest?.LiftUnitId,
            HeaviestSuccessfulLiftTon = heaviest?.LiftWeightTon,
            HighestSuccessfulLiftUnitId = highest?.LiftUnitId,
            HighestSuccessfulLiftElevationM = highest?.LoadPointElevationM,
            LongestRadiusLiftUnitId = longestRadius?.LiftUnitId,
            LargestBoomLiftUnitId = largestBoom?.LiftUnitId
        };
    }

    private static GaRelocationAnalysisSummary BuildRelocationSummary(
        IReadOnlyList<GaPositionSnapshotDto> positions)
    {
        var ordered = positions.OrderBy(position => position.SetupIndex).ToArray();
        var distances = ordered
            .Zip(ordered.Skip(1), (from, to) =>
            {
                var dx = to.LocalPosition.X - from.LocalPosition.X;
                var dy = to.LocalPosition.Y - from.LocalPosition.Y;
                var dz = to.LocalPosition.Z - from.LocalPosition.Z;
                return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            })
            .ToArray();
        return new GaRelocationAnalysisSummary
        {
            SetupCount = ordered.Length,
            RelocationCount = distances.Length,
            TotalRelocationDistanceM = distances.Sum(),
            MaximumRelocationDistanceM = distances.Length == 0 ? null : distances.Max()
        };
    }

    private static GaPerformanceAnalysisSummary BuildPerformanceSummary(GaResultSnapshotDto snapshot)
    {
        var generated = snapshot.Performance?.CandidatePointsGenerated
            ?? snapshot.Reachability?.TotalCandidatePoints
            ?? snapshot.InputScope.CandidateSetupPointCount;
        var valid = snapshot.Performance?.ValidPointsGenerated
            ?? snapshot.Reachability?.ValidCandidatePoints
            ?? snapshot.InputScope.ValidCandidatePointCount;
        var rejected = snapshot.Reachability?.RejectedCandidatePoints
            ?? snapshot.InputScope.RejectedCandidatePointCount;
        var interpretation = BuildRunInterpretation(snapshot, generated, valid);
        return new GaPerformanceAnalysisSummary
        {
            RunDurationMilliseconds = snapshot.Run.DurationMilliseconds,
            StageCount = snapshot.Stages.Count,
            ConfiguredGenerations = snapshot.Performance?.ConfiguredGenerations,
            ActualGenerationLoopCount = snapshot.Performance?.ActualGenerationLoopCount,
            GreedySetCoverIterationCount = snapshot.Performance?.GreedySetCoverIterationCount,
            ElementAssignmentTimeMilliseconds = snapshot.Performance?.ElementAssignmentTimeMilliseconds,
            FitnessEvaluationTimeMilliseconds = snapshot.Performance?.FitnessEvaluationTimeMilliseconds,
            CapacityCacheHitRatio = snapshot.Performance?.CapacityCacheHitRatio,
            StoppedByConvergence = snapshot.Performance?.StoppedByConvergence,
            StoppedByMaxGenerations = snapshot.Performance?.StoppedByMaxGenerations,
            FitnessScore = snapshot.Summary.FitnessScore,
            CoverageRatio = snapshot.Summary.CoverageRatio,
            CandidatePointsGenerated = generated,
            ValidCandidatePoints = valid,
            RejectedCandidatePoints = rejected,
            ValidCandidateRatio = generated is > 0 && valid.HasValue
                ? valid.Value / (double)generated.Value
                : null,
            ZeroCoverageCandidateCount = snapshot.Reachability?.ZeroCoveragePoints,
            HotSpotCount = snapshot.Reachability?.HotSpotCount,
            FinalSetupPositionCount = snapshot.Positions.Count,
            RunInterpretation = interpretation,
            EarlyStopCondition = snapshot.Run.TerminationReason
        };
    }

    private static string BuildRunInterpretation(
        GaResultSnapshotDto snapshot,
        int? candidatePoints,
        int? validPoints)
    {
        if (snapshot.Performance?.StoppedByConvergence == true)
            return "ConvergedNormally";
        if (validPoints == 0 || candidatePoints == 0)
            return "CandidateStarved";
        if (snapshot.Summary.NotCharted > 0)
            return "LoadChartLimited";
        if (snapshot.Summary.OutOfRadius > 0)
            return "RadiusLimited";
        if (snapshot.Summary.Overweight > 0)
            return "CapacityLimited";
        if (snapshot.Summary.UnliftedLiftUnitCount > 0)
            return "InputFeasibilityLimited";
        if (snapshot.Performance?.StoppedByMaxGenerations == true)
            return "StoppedWithoutConvergence";
        return snapshot.Capabilities.HasPerformanceMetrics
            ? "CompletedWithoutExplicitConvergenceClaim"
            : "InconclusiveMissingConvergenceEvidence";
    }

    private static (string? LiftUnitId, string? Criterion, double? Utilization) FindControllingLift(
        IReadOnlyList<GaElementEvaluationSnapshotDto> successful,
        GaResultSnapshotDto snapshot)
    {
        var evidence = successful.SelectMany(evaluation => new[]
        {
            (
                evaluation.LiftUnitId,
                Criterion: "Capacity",
                Utilization: evaluation.CapacityUtilizationPercent is >= 0
                    ? evaluation.CapacityUtilizationPercent / 100.0
                    : null),
            (
                evaluation.LiftUnitId,
                Criterion: "Radius",
                Utilization: RatioValue(
                    evaluation.RequiredRadiusM ?? evaluation.RadiusM,
                    snapshot.Crane.MaxRadiusM)),
            (
                evaluation.LiftUnitId,
                Criterion: "Boom",
                Utilization: RatioValue(
                    evaluation.RequiredBoomLengthM ?? evaluation.BoomLengthM,
                    snapshot.Crane.MaxBoomLengthM))
        });
        var controlling = evidence
            .Where(item => item.Utilization.HasValue)
            .OrderByDescending(item => item.Utilization)
            .ThenBy(item => item.LiftUnitId, StringComparer.Ordinal)
            .FirstOrDefault();
        return controlling.Utilization.HasValue
            ? controlling
            : (null, null, null);
    }

    private static CurrentCraneAdequacy AssessAdequacy(
        GaResultSnapshotDto snapshot,
        GaLiftMetricSummary metrics,
        int successfulEvidenceCount)
    {
        if (!snapshot.Capabilities.HasFinalAssignments || metrics.SuccessfulLiftCount == 0)
            return CurrentCraneAdequacy.Inconclusive;
        if (metrics.ReliableCraneLimitFailureCount > 0)
            return CurrentCraneAdequacy.Inadequate;
        if (metrics.FailedLiftCount > 0)
            return CurrentCraneAdequacy.Inconclusive;
        if (successfulEvidenceCount != metrics.SuccessfulLiftCount
            || !metrics.CapacityUtilizationMaximum.HasValue
            || !metrics.RadiusUtilizationMaximum.HasValue
            || !metrics.BoomUtilizationMaximum.HasValue)
        {
            return CurrentCraneAdequacy.Inconclusive;
        }

        var nearLimit = new[]
        {
            metrics.CapacityUtilizationMaximum,
            metrics.RadiusUtilizationMaximum,
            metrics.BoomUtilizationMaximum
        }.Any(value => value >= GaLiftAnalysisRules.BorderlineUtilization);
        return nearLimit ? CurrentCraneAdequacy.Borderline : CurrentCraneAdequacy.Suitable;
    }

    private static RightSizingAssessment AssessRightSizing(
        GaResultSnapshotDto snapshot,
        CurrentCraneAdequacy adequacy,
        GaLiftMetricSummary metrics)
    {
        if (adequacy == CurrentCraneAdequacy.Inadequate)
            return RightSizingAssessment.NotEvaluated;
        if (adequacy == CurrentCraneAdequacy.Inconclusive)
            return RightSizingAssessment.Inconclusive;
        if (adequacy == CurrentCraneAdequacy.Borderline)
            return RightSizingAssessment.Justified;
        if (metrics.SuccessfulLiftCount < GaLiftAnalysisRules.MinimumRightSizingLiftCount)
            return RightSizingAssessment.Inconclusive;
        if (!snapshot.Capabilities.HasLoadChartProvenance
            || !metrics.CapacityUtilizationP95.HasValue
            || !metrics.CapacityUtilizationMaximum.HasValue
            || !metrics.RadiusUtilizationP95.HasValue
            || !metrics.BoomUtilizationP95.HasValue)
        {
            return RightSizingAssessment.Inconclusive;
        }

        var oversized = metrics.CapacityUtilizationP95 < GaLiftAnalysisRules.OversizeCapacityP95Maximum
            && metrics.CapacityUtilizationMaximum < GaLiftAnalysisRules.OversizeCapacityMaximum
            && metrics.RadiusUtilizationP95 < GaLiftAnalysisRules.OversizeGeometryP95Maximum
            && metrics.BoomUtilizationP95 < GaLiftAnalysisRules.OversizeGeometryP95Maximum
            && metrics.ReliableCraneLimitFailureCount == 0;
        return oversized ? RightSizingAssessment.PotentiallyOversized : RightSizingAssessment.Justified;
    }

    internal static bool IsReliableCraneLimitFailure(GaFinalOutcomeSnapshotDto outcome)
    {
        var evidence = $"{outcome.FailureCategory} {outcome.FailureReason} {outcome.FinalLiftStatus}";
        return !string.IsNullOrWhiteSpace(outcome.BestFailedEvaluationId)
            && (Contains(evidence, "capacity")
                || Contains(evidence, "overweight")
                || Contains(evidence, "radius")
                || Contains(evidence, "boom")
                || Contains(evidence, "reach")
                || Contains(evidence, "height")
                || Contains(evidence, "chart envelope")
                || Contains(evidence, "outsidechartenvelope"));
    }

    private static IReadOnlyList<GaLiftFailureGroup> BuildFailureGroups(
        IReadOnlyList<GaFinalOutcomeSnapshotDto> failures)
    {
        return failures
            .GroupBy(
                outcome => string.IsNullOrWhiteSpace(outcome.FailureCategory)
                    ? "Unclassified"
                    : outcome.FailureCategory!,
                StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var items = group.ToArray();
                var guidance = FailureGuidance(group.Key);
                var ids = items.Select(outcome => outcome.LiftUnitId)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                return new GaLiftFailureGroup
                {
                    Category = group.Key,
                    Count = items.Length,
                    PercentageOfUnresolved = failures.Count == 0
                        ? 0
                        : items.Length / (double)failures.Count,
                    IsReliableCraneLimitEvidence = items.Any(IsReliableCraneLimitFailure),
                    LiftUnitIds = ids,
                    TopCriticalExamples = ids.Take(5).ToArray(),
                    RootCauseExplanation = guidance.Explanation,
                    CraneChangeMayHelp = guidance.CraneChangeMayHelp,
                    PositionOrZoneReviewMayHelp = guidance.PositionOrZoneReviewMayHelp,
                    LoadChartCorrectionRequired = guidance.LoadChartCorrectionRequired,
                    InputDataCorrectionRequired = guidance.InputDataCorrectionRequired,
                    RecommendedNextAction = guidance.RecommendedNextAction,
                    Confidence = items.Any(outcome => !string.IsNullOrWhiteSpace(outcome.BestFailedEvaluationId))
                        ? GaLiftAnalysisConfidence.High
                        : GaLiftAnalysisConfidence.Medium
                };
            })
            .ToArray();
    }

    private static FailureGuidanceResult FailureGuidance(string category)
    {
        if (Contains(category, "overweight") || Contains(category, "capacity"))
        {
            return new(
                "The best saved evaluation could not provide sufficient effective chart capacity.",
                true,
                false,
                false,
                false,
                "Review the critical lift and screen a crane with a matching higher-capacity chart envelope.");
        }
        if (Contains(category, "radius") || Contains(category, "reach"))
        {
            return new(
                "The required working radius exceeded the supported saved crane or chart envelope.",
                true,
                true,
                false,
                false,
                "Review setup positions and zones; if the radius cannot be reduced, screen a longer-reach crane.");
        }
        if (Contains(category, "boom") || Contains(category, "height"))
        {
            return new(
                "The required vertical geometry or boom length exceeded the supported saved envelope.",
                true,
                true,
                false,
                false,
                "Review the lift geometry and screen a longer-boom crane if the geometry is confirmed.");
        }
        if (Contains(category, "chart") || Contains(category, "notcharted"))
        {
            return new(
                "The saved operating condition could not be resolved to complete positive load-chart evidence.",
                false,
                false,
                true,
                false,
                "Review the load-chart import, operating condition, line constraint, and missing or invalid cells.");
        }
        if (Contains(category, "weight") || Contains(category, "ifc") || Contains(category, "geometry"))
        {
            return new(
                "Trusted lift-input evidence is missing or unsafe for a deterministic crane decision.",
                false,
                false,
                false,
                true,
                "Correct the IFC lift-unit readiness, trusted weight, or geometry evidence before screening cranes.");
        }
        if (Contains(category, "candidate") || Contains(category, "zone") || Contains(category, "position"))
        {
            return new(
                "The optimizer did not retain a valid setup candidate for this lift.",
                false,
                true,
                false,
                false,
                "Review allowed/forbidden zones and add valid setup candidates before rerunning the optimizer.");
        }

        return new(
            "The final GA outcome did not contain enough categorized evidence for a crane-change decision.",
            false,
            false,
            false,
            true,
            "Review the affected lift evidence and correct its input readiness before rerunning analysis.");
    }

    private static IReadOnlyList<GaAnalysisRecommendation> BuildRecommendations(
        string headlineCode,
        string headlineQualifier,
        GaLiftAnalysisConfidence confidence,
        IReadOnlyList<GaLiftFailureGroup> failureGroups,
        GaAlternativeScreeningResult screening)
    {
        var recommendations = new List<GaAnalysisRecommendation>();
        var primaryAction = headlineCode switch
        {
            "LARGER_OR_LONGER_REACH_CRANE_REQUIRED" =>
                "Review the validated recovery candidate, then rerun GA with that crane before adoption.",
            "CURRENT_CRANE_INADEQUATE" =>
                "Review the controlling failures and screen a matching larger-capacity or longer-reach crane.",
            "CHANGE_RECOMMENDED" =>
                "Review the top-ranked validated candidate and rerun GA with it before changing the plan.",
            "SMALLER_CRANE_CANDIDATE_AVAILABLE" =>
                "Review the validated smaller candidate and rerun GA before selecting it.",
            "POTENTIALLY_OVERSIZED" =>
                "Complete or review smaller-crane screening before making a crane-selection change.",
            "CURRENT_CRANE_BORDERLINE" =>
                "Obtain qualified review of the controlling lift and verify manufacturer chart evidence.",
            "KEEP_CURRENT_CRANE" =>
                "Retain the current crane for planning, subject to the recorded limitations and engineering review.",
            _ =>
                "Resolve missing or conflicting evidence, then rebuild Lift Analysis."
        };
        recommendations.Add(new GaAnalysisRecommendation
        {
            Priority = 1,
            Area = "Crane suitability",
            Evidence = headlineCode,
            Explanation = headlineQualifier,
            SuggestedNextAction = primaryAction,
            RelevantApplicationArea = "GA Optimizer / Lift Analysis",
            Confidence = confidence
        });

        foreach (var group in failureGroups.Take(5))
        {
            recommendations.Add(new GaAnalysisRecommendation
            {
                Priority = recommendations.Count + 1,
                Area = $"Failure: {group.Category}",
                Evidence = $"{group.Count} unresolved lift(s): {string.Join(", ", group.TopCriticalExamples)}",
                Explanation = group.RootCauseExplanation,
                SuggestedNextAction = group.RecommendedNextAction,
                RelevantApplicationArea = group.LoadChartCorrectionRequired
                    ? "Crane Setup / Load Chart"
                    : group.PositionOrZoneReviewMayHelp
                        ? "Zones / GA Optimizer"
                        : "Element Management / GA Optimizer",
                Confidence = group.Confidence
            });
        }

        var bestCandidate = screening.Candidates
            .Where(candidate => candidate.IsValidatedAlternative)
            .OrderBy(candidate => candidate.DeterministicRank ?? int.MaxValue)
            .ThenBy(candidate => candidate.CraneSpecificationId)
            .FirstOrDefault();
        if (bestCandidate != null)
        {
            recommendations.Add(new GaAnalysisRecommendation
            {
                Priority = recommendations.Count + 1,
                Area = "Alternative crane",
                Evidence = $"{bestCandidate.CraneName}; current-position coverage {bestCandidate.CurrentPositionCoverageRatio:P1}.",
                Explanation = "The candidate passed deterministic screening at the saved final setup coordinates.",
                SuggestedNextAction = "Rerun the optimizer with this candidate and recheck site feasibility before adoption.",
                RelevantApplicationArea = "Crane Setup / GA Optimizer",
                Confidence = GaLiftAnalysisConfidence.Medium
            });
        }

        return recommendations;
    }

    private static IReadOnlyList<string> BuildLimitations(
        GaResultSnapshotDto snapshot,
        GaLiftAnalysisRequest request,
        int successfulEvidenceCount)
    {
        var limitations = new List<string>
        {
            "Candidate-specific slew-center and boom-hinge geometry was unavailable in the current crane-catalog schema. Alternative cranes were evaluated using the shared geometry offsets captured from the original optimizer run.",
            "Alternative screening does not revalidate crane footprint, access, ground bearing, outrigger reactions, collision paths, or manufacturer approval.",
            "The analysis is advisory and does not change the selected crane or rerun the genetic algorithm."
        };
        if (snapshot.SchemaVersion < 3)
            limitations.Add("The source is a legacy snapshot and does not contain the v2 component fingerprint.");
        if (!snapshot.Capabilities.HasPositionEvaluationMatrix)
            limitations.Add("The source does not contain a complete position-evaluation matrix; failure recovery cannot be proven.");
        if (!snapshot.Capabilities.HasLoadChartProvenance)
            limitations.Add("Matching load-chart provenance is incomplete; right-sizing conclusions remain inconclusive.");
        if (successfulEvidenceCount < GaLiftAnalysisRules.MinimumRightSizingLiftCount)
            limitations.Add($"Right-sizing needs at least {GaLiftAnalysisRules.MinimumRightSizingLiftCount} successful lifts.");
        if (request.RunAlternativeScreening && request.Candidates.Count == 0)
            limitations.Add("No alternative crane specifications were available for screening.");
        if (!string.IsNullOrWhiteSpace(request.AlternativeScreeningUnavailableReason))
            limitations.Add($"Alternative screening was unavailable: {request.AlternativeScreeningUnavailableReason}");
        return limitations;
    }

    private static (string Code, string Headline, string Qualifier) BuildHeadline(
        CurrentCraneAdequacy adequacy,
        RightSizingAssessment rightSizing,
        AlternativeScreeningOutcome screening)
    {
        var headline = (adequacy, rightSizing, screening) switch
        {
            (CurrentCraneAdequacy.Inconclusive, _, _) =>
                ("ANALYSIS_INCONCLUSIVE", "Crane suitability is inconclusive", "The saved evidence is insufficient or includes failures not attributable to crane capacity or reach."),
            (CurrentCraneAdequacy.Inadequate, _, AlternativeScreeningOutcome.LargerOrLongerReachCandidateAvailable) =>
                ("LARGER_OR_LONGER_REACH_CRANE_REQUIRED", "Current crane is inadequate", "A validated larger-capacity or longer-reach catalog candidate recovered the reliable crane-limit failures."),
            (CurrentCraneAdequacy.Inadequate, _, _) =>
                ("CURRENT_CRANE_INADEQUATE", "Current crane is inadequate", "At least one unresolved lift has reliable crane capacity or reach-limit evidence."),
            (_, _, AlternativeScreeningOutcome.BetterCandidateRecommended) =>
                ("CHANGE_RECOMMENDED", "Current crane is suitable", "A catalog candidate offers a materially better validated fit for the saved lift plan."),
            (_, _, AlternativeScreeningOutcome.SmallerCandidateAvailable) =>
                ("SMALLER_CRANE_CANDIDATE_AVAILABLE", "Smaller crane candidate available", "A meaningfully smaller catalog candidate preserves every successful final assignment."),
            (CurrentCraneAdequacy.Borderline, _, _) =>
                ("CURRENT_CRANE_BORDERLINE", "Current crane is borderline", "The saved plan is feasible, but at least one capacity, radius, or boom utilization reaches 90%."),
            (CurrentCraneAdequacy.Suitable, RightSizingAssessment.PotentiallyOversized, _) =>
                ("POTENTIALLY_OVERSIZED", "Current crane is suitable but may be oversized", "Utilization is consistently low; no smaller candidate has yet been validated."),
            (CurrentCraneAdequacy.Suitable, _, _) =>
                ("KEEP_CURRENT_CRANE", "Current crane is suitable", "The saved final assignments are feasible without reliable crane-limit failures."),
            _ => ("ANALYSIS_INCONCLUSIVE", "Crane suitability is inconclusive", "The saved evidence is insufficient or includes failures not attributable to crane capacity or reach.")
        };

        var qualifier = screening switch
        {
            AlternativeScreeningOutcome.NotRun =>
                $"{headline.Item3} Alternative-crane screening was not run.",
            AlternativeScreeningOutcome.Cancelled =>
                $"{headline.Item3} Alternative-crane screening was cancelled.",
            AlternativeScreeningOutcome.Inconclusive =>
                $"{headline.Item3} Alternative-crane screening remains inconclusive.",
            AlternativeScreeningOutcome.SmallerCandidateAvailable
                or AlternativeScreeningOutcome.BetterCandidateRecommended
                or AlternativeScreeningOutcome.LargerOrLongerReachCandidateAvailable =>
                $"{headline.Item3} Validation used the saved setup coordinates and shared run geometry offsets; rerun GA and recheck site feasibility before adoption.",
            _ => headline.Item3
        };
        return (headline.Item1, headline.Item2, qualifier);
    }

    private static string RevisionId(
        GaResultSnapshotDto snapshot,
        CurrentCraneAdequacy adequacy,
        RightSizingAssessment rightSizing,
        GaAlternativeScreeningResult screening,
        string catalogFingerprint)
    {
        var candidateProjection = string.Join(
            ";",
            screening.Candidates
                .OrderBy(candidate => candidate.CraneSpecificationId)
                .Select(candidate => $"{candidate.CraneSpecificationId}:{candidate.IsValidatedAlternative}:{candidate.RecoveredFailedLiftCount}"));
        var text = string.Join(
            "|",
            GaLiftAnalysisRules.EngineId,
            GaLiftAnalysisRules.EngineVersion,
            GaLiftAnalysisRules.ProfileId,
            GaLiftAnalysisRules.PolicyHash,
            catalogFingerprint,
            snapshot.Project.ProjectGuid,
            snapshot.Run.OptimizationRunId,
            snapshot.Model.OptimizationInputFingerprint,
            adequacy,
            rightSizing,
            screening.Outcome,
            candidateProjection);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        return $"rev_{hash[..24]}";
    }

    private static string CatalogFingerprint(IReadOnlyList<GaCraneCandidate> candidates)
    {
        var builder = new StringBuilder();
        foreach (var candidate in candidates.OrderBy(item => item.Specification.Id))
        {
            var specification = candidate.Specification;
            Append(specification.Id);
            Append(candidate.DisplayName);
            Append(specification.CraneName);
            Append(specification.MaxCapacity);
            Append(specification.MinRadius);
            Append(specification.MaxRadius);
            Append(specification.MinBoomLength);
            Append(specification.MaxBoomLength);
            Append(specification.CounterWeight);
            Append(specification.MachineRotation);
            Append(specification.CraneDerating);
            Append(specification.OutriggerConfig);
            Append(specification.LoadChartStorageMode);
            Append(specification.LoadChartCapacityPolicy);
            Append(specification.ExactLoadChartRawGridJson);
            Append(specification.LoadChartSourceDescription);
            foreach (var point in specification.LoadChart
                         .OrderBy(point => point.Radius)
                         .ThenBy(point => point.BoomLength)
                         .ThenBy(point => point.Capacity)
                         .ThenBy(point => point.VisualColumnIndex)
                         .ThenBy(point => point.VisualColumnHeader, StringComparer.Ordinal)
                         .ThenBy(point => point.NumberOfLines)
                         .ThenBy(point => point.OperatingConditionLabel, StringComparer.Ordinal))
            {
                Append(point.Radius);
                Append(point.BoomLength);
                Append(point.Capacity);
                Append(point.VisualColumnIndex);
                Append(point.VisualColumnHeader);
                Append(point.NumberOfLines);
                Append(point.OperatingConditionLabel);
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();

        void Append(object? value)
        {
            var text = value switch
            {
                double number => number.ToString("R", CultureInfo.InvariantCulture),
                float number => number.ToString("R", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                null => "<null>",
                _ => value.ToString() ?? string.Empty
            };
            builder.Append(text.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(text)
                .Append('|');
        }
    }

    private static double[] Ratio(IEnumerable<double?> values, double? denominator) =>
        denominator is > 0
            ? values.Where(value => value is >= 0).Select(value => value!.Value / denominator.Value).ToArray()
            : Array.Empty<double>();

    private static double[] Available(IEnumerable<double?> values) =>
        values.Where(value => value is >= 0).Select(value => value!.Value).ToArray();

    private static double? RatioValue(double? value, double? denominator) =>
        value is >= 0 && denominator is > 0
            ? value.Value / denominator.Value
            : null;

    private static double? Maximum(IReadOnlyCollection<double> values) =>
        values.Count == 0 ? null : values.Max();

    private static double? Average(IReadOnlyCollection<double> values) =>
        values.Count == 0 ? null : values.Average();

    private static double? Minimum(IEnumerable<double?> values)
    {
        var available = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return available.Length == 0 ? null : available.Min();
    }

    private static bool Contains(string value, string term) =>
        value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private sealed record FailureGuidanceResult(
        string Explanation,
        bool CraneChangeMayHelp,
        bool PositionOrZoneReviewMayHelp,
        bool LoadChartCorrectionRequired,
        bool InputDataCorrectionRequired,
        string RecommendedNextAction);
}
