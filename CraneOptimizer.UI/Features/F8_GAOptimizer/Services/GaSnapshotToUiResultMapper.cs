using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services;

public sealed class GaSnapshotToUiResultMapper : IGaSnapshotToUiResultMapper
{
    public CranePlacementResult Map(
        GaResultSnapshotDto snapshot,
        GaSnapshotUiMappingContext context)
    {
        var liftUnitsById = snapshot.LiftUnits.ToDictionary(unit => unit.LiftUnitId, StringComparer.Ordinal);
        var positions = snapshot.Positions
            .OrderBy(position => position.SetupIndex)
            .Select(position => MapPosition(position, liftUnitsById))
            .ToList();

        return new CranePlacementResult
        {
            Positions = positions,
            FitnessScore = snapshot.Summary.FitnessScore,
            CoverageRatio = snapshot.Summary.CoverageRatio,
            LiftedElementCount = snapshot.Summary.LiftedLiftUnitCount,
            TotalElementCount = snapshot.Summary.TotalTargetLiftUnitCount,
            RawGaEligibleElementCount = snapshot.Summary.RawGaEligibleElementCount,
            GroupedAssemblyCount = snapshot.Summary.GroupedAssemblyCount,
            UngroupedElementCount = snapshot.Summary.UngroupedElementCount,
            Statistics = new LiftStatistics
            {
                Liftable = snapshot.Summary.Liftable,
                OutOfRadius = snapshot.Summary.OutOfRadius,
                Overweight = snapshot.Summary.Overweight,
                HeightFail = snapshot.Summary.HeightFail,
                SpecialCase = snapshot.Summary.SpecialCase,
                NotCharted = snapshot.Summary.NotCharted
            },
            OptimizationRunId = snapshot.Run.OptimizationRunId,
            SnapshotSourceKind = context.SourceKind,
            ResultClassification = context.Classification,
            IsReadOnlyRestoredResult = context.IsReadOnly,
            PersistenceStatus = context.SourceKind == "CurrentSession" ? "Saved" : "Restored",
            PersistenceMessage = BuildBanner(context, snapshot),
            ResultSourceBanner = BuildBanner(context, snapshot),
            CraneName = snapshot.Crane.CraneName,
            CraneManufacturer = snapshot.Crane.Manufacturer,
            CraneModelOrSpecification = snapshot.Crane.ModelOrSpecification,
            CraneType = snapshot.Crane.CraneType,
            MaxCapacity = snapshot.Crane.MaxCapacityTon ?? 0,
            MinRadius = snapshot.Crane.MinRadiusM ?? 0,
            MaxRadius = snapshot.Crane.MaxRadiusM ?? 0,
            MinBoomLength = snapshot.Crane.MinBoomLengthM ?? 0,
            MaxBoomLength = snapshot.Crane.MaxBoomLengthM ?? 0,
            VehicleLength = snapshot.Crane.VehicleLengthM ?? 0,
            VehicleWidth = snapshot.Crane.VehicleWidthM ?? 0,
            VehicleHeight = snapshot.Crane.VehicleHeightM ?? 0,
            OutriggerLength = snapshot.Crane.OutriggerLengthM,
            OutriggerWidth = snapshot.Crane.OutriggerWidthM,
            OutriggerConfiguration = snapshot.Crane.OutriggerConfiguration,
            WireLength = snapshot.LiftingAccessories.RiggingWireRopeLengthM
                ?? snapshot.LiftingAccessories.WireRopeLengthM,
            AdditionalWeight = snapshot.Crane.HookRiggingAdditionalWeightTon ?? 0,
            DeratingRatio = snapshot.Crane.DeratingRatio ?? 0,
            SafetyFactor = snapshot.Crane.SafetyFactor ?? 0,
            CounterWeight = snapshot.Crane.CounterWeightTon ?? 0
        };
    }

    private static CranePositionResult MapPosition(
        GaPositionSnapshotDto position,
        IReadOnlyDictionary<string, CanonicalLiftUnitSnapshotDto> liftUnitsById) => new()
    {
        PositionId = position.PositionId,
        SetupIndex = position.SetupIndex,
        X = position.LocalPosition.X,
        Y = position.LocalPosition.Y,
        Z = position.LocalPosition.Z,
        NewLiftableCount = position.NewLiftableCount,
        FinalAssignedCount = position.FinalAssignedCount,
        FinalAssignedLiftUnitIds = position.FinalAssignedLiftUnitIds.ToList(),
        ElementResults = position.Evaluations
            .Select(evaluation => MapEvaluation(evaluation, liftUnitsById.TryGetValue(evaluation.LiftUnitId, out var unit) ? unit : null))
            .ToList()
    };

    private static ElementLiftResult MapEvaluation(
        GaElementEvaluationSnapshotDto evaluation,
        CanonicalLiftUnitSnapshotDto? unit) => new()
    {
        ElementId = evaluation.LiftUnitId,
        ElementMark = unit?.ElementMark,
        CanonicalMark = unit?.CanonicalMark,
        MarkSource = unit?.MarkSource,
        MarkConfidence = unit?.MarkConfidence,
        IsExplicitMark = unit?.IsExplicitMark ?? false,
        ValidationStatus = unit?.ValidationStatus,
        WeightTrustLevel = unit?.WeightTrustLevel,
        ValidationWarningCount = unit?.ValidationWarnings.Count ?? 0,
        ValidationWarningsJoined = unit?.ValidationWarnings.Count > 0 ? string.Join("; ", unit.ValidationWarnings) : null,
        IsAssemblyGroup = unit?.IsAssemblyGroup ?? false,
        AssemblyMemberCount = unit?.AssemblyMemberIds.Count ?? 0,
        AssemblyMemberIdsJoined = unit?.AssemblyMemberIds.Count > 0 ? string.Join(", ", unit.AssemblyMemberIds) : null,
        AssemblyMemberIds = unit?.AssemblyMemberIds.ToList() ?? new List<string>(),
        ParentAssemblyGlobalId = unit?.ParentAssemblyGlobalId,
        MainPartGlobalId = unit?.MainPartGlobalId,
        ElementType = unit?.ElementType,
        Weight = evaluation.ElementWeightTon ?? unit?.LiftWeightTon,
        CogX = unit?.LiftCogLocal?.X ?? 0,
        CogY = unit?.LiftCogLocal?.Y ?? 0,
        CogZ = unit?.LiftCogLocal?.Z ?? 0,
        Status = Parse(evaluation.LiftStatus, LiftStatus.NotCharted),
        Radius = evaluation.RadiusM ?? 0,
        BoomLength = evaluation.BoomLengthM ?? evaluation.RequiredBoomLengthM ?? evaluation.BestFailedRequiredBoomLengthM ?? 0,
        AvailableCapacity = evaluation.AvailableCapacityTon ?? 0,
        RawChartCapacity = evaluation.RawChartCapacityTon,
        RequiredCapacity = evaluation.RequiredCapacityTon ?? 0,
        ReachStatus = Parse(evaluation.ReachStatus, ReachStatus.OutOfRadius),
        ChartStatus = Parse(evaluation.ChartStatus, ChartStatus.NoListedCapacity),
        IsExactMatch = evaluation.IsExactMatch,
        IsSteppedRadius = evaluation.IsSteppedRadius,
        IsBlankCell = evaluation.IsBlankCell,
        IsZeroCell = evaluation.IsZeroCell,
        SafetyMargin = evaluation.SafetyMarginTon ?? 0,
        LookupMode = evaluation.LookupMode ?? "ConservativeStep",
        RequiredRadius = evaluation.RequiredRadiusM ?? evaluation.BestFailedRequiredRadiusM ?? evaluation.RadiusM ?? 0,
        SelectedChartRadius = evaluation.SelectedChartRadiusM ?? 0,
        SelectedChartBoomLength = evaluation.SelectedChartBoomLengthM ?? 0,
        CraneBaseX = evaluation.CraneBaseLocal?.X ?? 0,
        CraneBaseY = evaluation.CraneBaseLocal?.Y ?? 0,
        CraneBaseZ = evaluation.CraneBaseLocal?.Z ?? 0,
        SlewCenterX = evaluation.SlewCenterLocal?.X ?? 0,
        SlewCenterY = evaluation.SlewCenterLocal?.Y ?? 0,
        SlewCenterZ = evaluation.SlewCenterLocal?.Z ?? 0,
        BoomHingeX = evaluation.BoomHingeLocal?.X ?? 0,
        BoomHingeY = evaluation.BoomHingeLocal?.Y ?? 0,
        BoomHingeZ = evaluation.BoomHingeLocal?.Z ?? 0,
        LoadPointX = evaluation.LoadPointLocal?.X ?? 0,
        LoadPointY = evaluation.LoadPointLocal?.Y ?? 0,
        LoadPointZ = evaluation.LoadPointLocal?.Z ?? 0,
        RadiusOriginType = evaluation.RadiusOriginType ?? "SlewCenter",
        BoomOriginType = evaluation.BoomOriginType ?? "BoomHinge",
        CandidateBoomsChecked = evaluation.CandidateBoomsChecked ?? string.Empty,
        RejectedCandidates = evaluation.RejectedCandidates ?? string.Empty,
        CandidatePointId = evaluation.CandidatePointId,
        CandidatePointIndex = evaluation.CandidatePointIndex,
        IsFeasibleFromPoint = evaluation.IsFeasibleFromPoint,
        IsFinalAssignedToPoint = evaluation.IsFinalAssignedToPoint,
        AssignedPointId = evaluation.AssignedPointId ?? string.Empty,
        AssignedPointIndex = evaluation.AssignedPointIndex ?? -1,
        AssignmentReason = evaluation.AssignmentReason ?? string.Empty,
        AssignmentScore = evaluation.AssignmentScore ?? 0,
        DuplicateAssignmentResolved = evaluation.DuplicateAssignmentResolved,
        FeasiblePointIds = string.Join(",", evaluation.FeasiblePointIds),
        FinalOwnerPointId = evaluation.FinalOwnerPointId ?? string.Empty,
        IsFinalAssigned = evaluation.IsFinalAssigned,
        BestFailedPointId = evaluation.BestFailedPointId ?? string.Empty,
        BestFailureReason = evaluation.BestFailureReason ?? string.Empty,
        HasBestFailedCandidate = evaluation.HasBestFailedCandidate,
        BestFailedLiftStatus = Parse(evaluation.BestFailedLiftStatus, LiftStatus.OutOfRadius),
        BestFailedReachStatus = Parse(evaluation.BestFailedReachStatus, ReachStatus.OutOfRadius),
        BestFailedChartStatus = Parse(evaluation.BestFailedChartStatus, ChartStatus.NoListedCapacity),
        BestFailedSafetyMargin = evaluation.BestFailedSafetyMarginTon ?? 0,
        BestFailedRequiredRadius = evaluation.BestFailedRequiredRadiusM ?? 0,
        BestFailedRequiredBoomLength = evaluation.BestFailedRequiredBoomLengthM ?? 0
    };

    private static TEnum Parse<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
    {
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static string BuildBanner(GaSnapshotUiMappingContext context, GaResultSnapshotDto snapshot) =>
        context.SourceKind switch
        {
            "CurrentSession" => "Current-session GA result.",
            "LegacyCsv" or "PartialLegacy" => "Legacy GA result. Final assignments and available engineering values are shown; setup-specific feasibility and assignment review may be unavailable.",
            _ when context.Classification == "Historic" => "Persisted JSON GA result generated from an earlier project model revision.",
            _ when context.Classification == "CurrentProjectUnverified" => "Persisted JSON GA result. Project matches, but current model identity is not yet available for verification.",
            _ => $"Persisted JSON GA result completed {snapshot.Run.CompletedAtUtc:yyyy-MM-dd HH:mm}."
        };
}
