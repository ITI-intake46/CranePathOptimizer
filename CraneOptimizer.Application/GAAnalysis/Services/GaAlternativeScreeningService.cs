using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.GAAnalysis;

public sealed class GaAlternativeScreeningService : IGaAlternativeScreeningService
{
    private readonly ICraneConfigurationEngine _configurationEngine;

    public GaAlternativeScreeningService(ICraneConfigurationEngine configurationEngine)
    {
        _configurationEngine = configurationEngine;
    }

    public Task<GaAlternativeScreeningResult> ScreenAsync(
        GaResultSnapshotDto snapshot,
        CurrentCraneAdequacy adequacy,
        IReadOnlyList<GaCraneCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(new GaAlternativeScreeningResult
            {
                Outcome = AlternativeScreeningOutcome.Cancelled
            });
        }

        if (candidates.Count == 0)
        {
            return Task.FromResult(new GaAlternativeScreeningResult
            {
                Outcome = AlternativeScreeningOutcome.NoValidatedAlternative
            });
        }

        var settings = ToSettings(snapshot.EffectiveFinalSettings);
        var units = snapshot.LiftUnits.ToDictionary(unit => unit.LiftUnitId, StringComparer.Ordinal);
        var evaluations = snapshot.Positions
            .SelectMany(position => position.Evaluations)
            .ToArray();
        var byId = evaluations.ToDictionary(evaluation => evaluation.EvaluationId, StringComparer.Ordinal);
        var successful = snapshot.FinalOutcomes
            .Where(outcome => outcome.IsLifted && outcome.FinalEvaluationId != null)
            .Select(outcome => byId.GetValueOrDefault(outcome.FinalEvaluationId!))
            .Where(evaluation => evaluation != null)
            .Cast<GaElementEvaluationSnapshotDto>()
            .ToArray();
        var reliableFailures = snapshot.FinalOutcomes
            .Where(outcome => !outcome.IsLifted && GaLiftAnalysisService.IsReliableCraneLimitFailure(outcome))
            .ToArray();

        var currentCapacity = snapshot.Crane.MaxCapacityTon.GetValueOrDefault();
        var currentRadius = snapshot.Crane.MaxRadiusM.GetValueOrDefault();
        var currentBoom = snapshot.Crane.MaxBoomLengthM.GetValueOrDefault();
        var results = new List<GaCandidateScreeningResult>(candidates.Count);

        foreach (var candidate in candidates
                     .Where(candidate => candidate.Specification.Id != snapshot.Crane.CraneSpecificationId)
                     .OrderBy(candidate => candidate.Specification.MaxCapacity)
                     .ThenBy(candidate => candidate.Specification.MaxRadius)
                     .ThenBy(candidate => candidate.Specification.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _configurationEngine.ClearCache();
            var specification = ToDomainSpecification(candidate.Specification);

            var successfulChecks = successful
                .Select(evaluation => Evaluate(specification, evaluation, units, settings))
                .ToArray();
            var preservedCount = successfulChecks.Count(check => check.Feasible);
            var planPreserved = preservedCount == successful.Length;

            var recoveredCount = 0;
            foreach (var failure in reliableFailures)
            {
                var recovered = evaluations
                    .Where(evaluation => evaluation.LiftUnitId == failure.LiftUnitId)
                    .Select(evaluation => Evaluate(specification, evaluation, units, settings))
                    .Any(check => check.Feasible);
                if (recovered)
                    recoveredCount++;
            }

            var utilization = successfulChecks
                .Where(check => check.Feasible)
                .Select(check => check.CapacityUtilization)
                .ToArray();
            var radiusUtilization = successfulChecks
                .Where(check => check.Feasible)
                .Select(check => check.RadiusUtilization)
                .ToArray();
            var boomUtilization = successfulChecks
                .Where(check => check.Feasible)
                .Select(check => check.BoomUtilization)
                .ToArray();
            var capacityMargins = successfulChecks
                .Where(check => check.Feasible)
                .Select(check => check.CapacityMarginTon)
                .ToArray();
            var requiredRadii = successfulChecks
                .Where(check => check.Feasible)
                .Select(check => check.RequiredRadiusM)
                .ToArray();
            var requiredBooms = successfulChecks
                .Where(check => check.Feasible)
                .Select(check => check.RequiredBoomLengthM)
                .ToArray();
            double? maximum = utilization.Length == 0 ? null : utilization.Max();
            var p95 = GaLiftAnalysisRules.PercentileR7(utilization, 0.95);
            var radiusP95 = GaLiftAnalysisRules.PercentileR7(radiusUtilization, 0.95);
            var boomP95 = GaLiftAnalysisRules.PercentileR7(boomUtilization, 0.95);
            var meaningfullySmaller = currentCapacity > 0
                && candidate.Specification.MaxCapacity <= currentCapacity * GaLiftAnalysisRules.MeaningfullySmallerRatio;
            var materialChange = currentCapacity > 0
                && candidate.Specification.MaxCapacity <= currentCapacity * GaLiftAnalysisRules.MaterialChangeRatio;
            var largerOrLonger = candidate.Specification.MaxCapacity > currentCapacity
                || candidate.Specification.MaxRadius > currentRadius
                || candidate.Specification.MaxBoomLength > currentBoom;
            var recoversAll = reliableFailures.Length > 0 && recoveredCount == reliableFailures.Length;
            var comparedLiftCount = successful.Length + reliableFailures.Length;
            var candidatePassedLiftCount = preservedCount + recoveredCount;

            var rightSizeEnvelope = maximum < GaLiftAnalysisRules.BorderlineUtilization
                && p95 is >= GaLiftAnalysisRules.RecommendedCapacityP95Minimum
                    and < GaLiftAnalysisRules.RecommendedCapacityP95Maximum
                && radiusP95 < GaLiftAnalysisRules.BorderlineUtilization
                && boomP95 < GaLiftAnalysisRules.BorderlineUtilization;
            var hasValidSpecification = candidate.Specification.MaxCapacity > 0
                && candidate.Specification.MaxRadius > 0
                && candidate.Specification.MaxBoomLength > 0;
            var hasValidChart = candidate.Specification.LoadChart.Any(point =>
                point.Capacity > 0
                && point.Radius >= 0
                && point.BoomLength > 0);
            var validated = adequacy switch
            {
                CurrentCraneAdequacy.Suitable or CurrentCraneAdequacy.Borderline =>
                    hasValidSpecification
                    && hasValidChart
                    && meaningfullySmaller
                    && planPreserved
                    && rightSizeEnvelope,
                CurrentCraneAdequacy.Inadequate =>
                    hasValidSpecification
                    && hasValidChart
                    && largerOrLonger
                    && planPreserved
                    && recoversAll,
                _ => false
            };

            var rejectionReasons = new List<string>();
            if (!hasValidSpecification)
                rejectionReasons.Add("Crane specification has an invalid capacity, radius, or boom envelope.");
            if (!hasValidChart)
                rejectionReasons.Add("Complete positive load-chart evidence is unavailable.");
            if (!planPreserved)
                rejectionReasons.Add("Does not preserve every successful final assignment.");
            if (adequacy is CurrentCraneAdequacy.Suitable or CurrentCraneAdequacy.Borderline && !meaningfullySmaller)
                rejectionReasons.Add("Nominal capacity is not at least 10% lower.");
            if (adequacy is CurrentCraneAdequacy.Suitable or CurrentCraneAdequacy.Borderline && !rightSizeEnvelope)
                rejectionReasons.Add("Utilization falls outside the conservative recommendation envelope.");
            if (adequacy == CurrentCraneAdequacy.Inadequate && !recoversAll)
                rejectionReasons.Add("Does not recover every reliable crane-limit failure across the saved setup positions.");

            results.Add(new GaCandidateScreeningResult
            {
                CraneSpecificationId = candidate.Specification.Id,
                CraneName = string.IsNullOrWhiteSpace(candidate.DisplayName)
                    ? candidate.Specification.CraneName
                    : candidate.DisplayName,
                NominalCapacityTon = candidate.Specification.MaxCapacity,
                MinRadiusM = candidate.Specification.MinRadius,
                MaxRadiusM = candidate.Specification.MaxRadius,
                MinBoomLengthM = candidate.Specification.MinBoomLength,
                MaxBoomLengthM = candidate.Specification.MaxBoomLength,
                PreservesSuccessfulPlan = planPreserved,
                PreservedSuccessfulLiftCount = preservedCount,
                SuccessfulLiftCount = candidatePassedLiftCount,
                FailedLiftCount = comparedLiftCount - candidatePassedLiftCount,
                RecoveredFailedLiftCount = recoveredCount,
                ReliableCraneLimitFailureCount = reliableFailures.Length,
                RecoversEveryReliableCraneLimitFailure = recoversAll,
                MaximumCapacityUtilization = maximum,
                CapacityUtilizationP95 = p95,
                RadiusUtilizationP95 = radiusP95,
                BoomUtilizationP95 = boomP95,
                MinimumCapacityMarginTon = capacityMargins.Length == 0 ? null : capacityMargins.Min(),
                MaximumRequiredRadiusM = requiredRadii.Length == 0 ? null : requiredRadii.Max(),
                MaximumRequiredBoomLengthM = requiredBooms.Length == 0 ? null : requiredBooms.Max(),
                CurrentPositionCoverageRatio = comparedLiftCount == 0
                    ? 0
                    : candidatePassedLiftCount / (double)comparedLiftCount,
                IsMeaningfullySmaller = meaningfullySmaller,
                IsMaterialChange = materialChange,
                IsLargerOrLongerReach = largerOrLonger,
                IsValidatedAlternative = validated,
                DeterministicRank = validated
                    ? results.Count(result => result.IsValidatedAlternative) + 1
                    : null,
                RejectionReasons = rejectionReasons
            });
        }

        var validatedResults = results.Where(result => result.IsValidatedAlternative).ToArray();
        var outcome = AlternativeScreeningOutcome.NoValidatedAlternative;
        if (adequacy == CurrentCraneAdequacy.Inadequate
            && validatedResults.Any(result => result.IsLargerOrLongerReach
                && result.RecoversEveryReliableCraneLimitFailure))
        {
            outcome = AlternativeScreeningOutcome.LargerOrLongerReachCandidateAvailable;
        }
        else if (adequacy is CurrentCraneAdequacy.Suitable or CurrentCraneAdequacy.Borderline
                 && validatedResults.Any(result => result.IsMaterialChange))
        {
            outcome = AlternativeScreeningOutcome.BetterCandidateRecommended;
        }
        else if (adequacy is CurrentCraneAdequacy.Suitable or CurrentCraneAdequacy.Borderline
                 && validatedResults.Any(result => result.IsMeaningfullySmaller))
        {
            outcome = AlternativeScreeningOutcome.SmallerCandidateAvailable;
        }
        else if (adequacy == CurrentCraneAdequacy.Inconclusive)
        {
            outcome = AlternativeScreeningOutcome.Inconclusive;
        }

        var bounded = validatedResults
            .OrderBy(result => result.DeterministicRank)
            .Take(5)
            .Concat(results
                .Where(result => !result.IsValidatedAlternative)
                .OrderBy(result => result.RejectionReasons.Count)
                .ThenByDescending(result => result.CurrentPositionCoverageRatio)
                .ThenBy(result => result.CraneSpecificationId)
                .Take(5))
            .ToArray();
        return Task.FromResult(new GaAlternativeScreeningResult
        {
            Outcome = outcome,
            Candidates = bounded
        });
    }

    private CandidateCheck Evaluate(
        CraneSpecification specification,
        GaElementEvaluationSnapshotDto source,
        IReadOnlyDictionary<string, CanonicalLiftUnitSnapshotDto> units,
        GASettings settings)
    {
        if (!units.TryGetValue(source.LiftUnitId, out var unit)
            || source.CraneBaseLocal == null
            || source.LoadPointLocal == null)
        {
            return CandidateCheck.Failed;
        }

        var craneBase = new DomainPoint3D(
            source.CraneBaseLocal.X,
            source.CraneBaseLocal.Y,
            source.CraneBaseLocal.Z);
        var loadPoint = new DomainPoint3D(
            source.LoadPointLocal.X,
            source.LoadPointLocal.Y,
            source.LoadPointLocal.Z);
        var slewX = craneBase.X + settings.SlewOffsetX;
        var slewY = craneBase.Y + settings.SlewOffsetY;
        var radius = Math.Sqrt(
            Math.Pow(loadPoint.X - slewX, 2)
            + Math.Pow(loadPoint.Y - slewY, 2));
        var element = new PrecastElement
        {
            ElementID = unit.LiftUnitId,
            ElementCOG = loadPoint,
            ElementHeight = unit.HeightM,
            ElementWT = unit.LiftWeightTon
        };
        var boom = _configurationEngine.CalculateRequiredBoomLength(craneBase, element, settings);
        var requiredCapacity = source.RequiredCapacityTon
            ?? unit.LiftWeightTon.GetValueOrDefault() + snapshotAccessoryWeight(source);

        if (_configurationEngine.ExceedsMaxRadius(radius, specification)
            || _configurationEngine.ExceedsMaxBoomLength(boom, specification))
        {
            return CandidateCheck.Failed;
        }

        var diagnostic = _configurationEngine.EvaluateCraneCapacityDiagnostic(
            specification,
            radius,
            boom,
            settings);
        var available = diagnostic.Capacity;
        var feasible = available > 0 && available >= requiredCapacity;
        return new CandidateCheck(
            feasible,
            available > 0 ? requiredCapacity / available : double.PositiveInfinity,
            specification.MaxRadius > 0 ? radius / specification.MaxRadius : double.PositiveInfinity,
            specification.MaxBoomLength > 0 ? boom / specification.MaxBoomLength : double.PositiveInfinity,
            available - requiredCapacity,
            radius,
            boom);

        static double snapshotAccessoryWeight(GaElementEvaluationSnapshotDto evaluation) =>
            evaluation.HookRiggingWeightTon.GetValueOrDefault();
    }

    private static GASettings ToSettings(GaSettingsSnapshotDto source) => new()
    {
        WireLength = source.WireLength,
        AdditionalWt = source.AdditionalWt,
        DeratingRatio = source.DeratingRatio,
        SafetyFactor = source.SafetyFactor,
        LoadChartNumberOfLinesConstraint = source.LoadChartNumberOfLinesConstraint,
        LoadChartOperatingConditionConstraint = source.LoadChartOperatingConditionConstraint,
        SlewOffsetX = source.SlewOffsetX,
        SlewOffsetY = source.SlewOffsetY,
        SlewOffsetZ = source.SlewOffsetZ,
        BoomHingeOffsetX = source.BoomHingeOffsetX,
        BoomHingeOffsetY = source.BoomHingeOffsetY,
        BoomHingeOffsetZ = source.BoomHingeOffsetZ
    };

    private static CraneSpecification ToDomainSpecification(GaCraneCandidateSpecification source)
    {
        var specification = new CraneSpecification
        {
            Id = source.Id,
            MaxCapacity = source.MaxCapacity,
            MaxRadius = source.MaxRadius,
            MinRadius = source.MinRadius,
            MaxBoomLength = source.MaxBoomLength,
            MinBoomLength = source.MinBoomLength,
            CounterWeight = source.CounterWeight,
            MachineRotation = source.MachineRotation,
            CraneDerating = source.CraneDerating,
            OutriggerConfig = source.OutriggerConfig,
            LoadChartStorageMode = source.LoadChartStorageMode,
            LoadChartCapacityPolicy = source.LoadChartCapacityPolicy,
            ExactLoadChartRawGridJson = source.ExactLoadChartRawGridJson,
            LoadChartSourceDescription = source.LoadChartSourceDescription,
            Crane = new Crane { Name = source.CraneName }
        };

        specification.LoadChart = source.LoadChart
            .Select(point => new TargetPoint
            {
                Radius = point.Radius,
                BoomLength = point.BoomLength,
                Capacity = point.Capacity,
                VisualColumnIndex = point.VisualColumnIndex,
                VisualColumnHeader = point.VisualColumnHeader,
                NumberOfLines = point.NumberOfLines,
                OperatingConditionLabel = point.OperatingConditionLabel,
                CraneSpecificationId = source.Id,
                CraneSpecification = specification
            })
            .ToArray();
        return specification;
    }

    private readonly record struct CandidateCheck(
        bool Feasible,
        double CapacityUtilization,
        double RadiusUtilization,
        double BoomUtilization,
        double CapacityMarginTon,
        double RequiredRadiusM,
        double RequiredBoomLengthM)
    {
        public static CandidateCheck Failed { get; } =
            new(
                false,
                double.PositiveInfinity,
                double.PositiveInfinity,
                double.PositiveInfinity,
                double.NegativeInfinity,
                double.PositiveInfinity,
                double.PositiveInfinity);
    }
}
