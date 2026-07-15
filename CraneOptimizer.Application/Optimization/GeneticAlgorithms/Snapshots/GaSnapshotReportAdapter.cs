using System.Globalization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;

public sealed class GaSnapshotReportAdapter : IGaSnapshotReportAdapter
{
    private const string Placeholder = "\u2014";

    public IReadOnlyList<LiftingResultSourceRowDto> BuildRows(
        GaResultSnapshotDto snapshot,
        string templateName,
        string itemsIncluded,
        string? selectedPositionId = null)
    {
        ValidateFinalOutcomeIntegrity(snapshot);

        if (itemsIncluded.Equals("Selected Setup Only", StringComparison.OrdinalIgnoreCase))
        {
            if (!snapshot.Capabilities.HasPositionEvaluationMatrix)
                return Array.Empty<LiftingResultSourceRowDto>();

            var position = !string.IsNullOrWhiteSpace(selectedPositionId)
                ? snapshot.Positions.FirstOrDefault(p => p.PositionId == selectedPositionId)
                : snapshot.Positions.FirstOrDefault();

            return position == null
                ? Array.Empty<LiftingResultSourceRowDto>()
                : position.Evaluations.Select(evaluation => ToRow(snapshot, position, evaluation)).ToList();
        }

        if (templateName.Equals("All Feasible Debug", StringComparison.OrdinalIgnoreCase))
            return snapshot.Capabilities.HasPositionEvaluationMatrix
                ? BuildMatrixRows(snapshot, evaluation => evaluation.IsFeasibleFromPoint)
                : Array.Empty<LiftingResultSourceRowDto>();

        return snapshot.FinalOutcomes.Select(outcome =>
        {
            var referenceId = outcome.IsLifted
                ? outcome.FinalEvaluationId
                : outcome.BestFailedEvaluationId ?? outcome.FinalEvaluationId;
            var located = FindEvaluation(snapshot, referenceId);
            return located.evaluation == null
                ? ToOutcomeOnlyRow(snapshot, outcome)
                : ToRow(snapshot, located.position!, located.evaluation, outcome);
        }).ToList();
    }

    public IReadOnlyList<CraneSetupReportRowDto> BuildCraneSetups(GaResultSnapshotDto snapshot)
    {
        var total = snapshot.Summary.LiftedLiftUnitCount;
        return snapshot.Positions
            .OrderBy(position => position.SetupIndex)
            .Select(position => new CraneSetupReportRowDto
            {
                SetupNo = position.SetupIndex,
                SetupReference = FirstNonEmpty(position.SetupReference, position.PositionId, $"Point_{position.SetupIndex}"),
                CraneX = position.LocalPosition.X,
                CraneY = position.LocalPosition.Y,
                CraneZ = position.LocalPosition.Z,
                LiftedItemsCount = position.FinalAssignedCount,
                LiftedItemsPercentage = total > 0 ? position.FinalAssignedCount * 100.0 / total : 0,
                Zone = FirstNonEmpty(position.ZoneName, position.ZoneId, Placeholder)
            })
            .ToList();
    }

    private static void ValidateFinalOutcomeIntegrity(GaResultSnapshotDto snapshot)
    {
        var unitIds = snapshot.LiftUnits
            .Select(unit => unit.LiftUnitId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var outcomeIds = snapshot.FinalOutcomes
            .Select(outcome => outcome.LiftUnitId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();

        if (unitIds.Count != snapshot.LiftUnits.Count)
            throw new InvalidOperationException("GA result snapshot contains duplicate or missing LiftUnitId values.");

        var duplicateOutcome = outcomeIds
            .GroupBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateOutcome != null)
            throw new InvalidOperationException($"GA result snapshot contains duplicate final outcomes for LiftUnitId '{duplicateOutcome.Key}'.");

        if (snapshot.LiftUnits.Count != snapshot.FinalOutcomes.Count)
            throw new InvalidOperationException("GA result snapshot final outcomes do not match submitted lift-unit count.");

        if (snapshot.SchemaVersion >= 2)
        {
            foreach (var unit in snapshot.LiftUnits)
            {
                if (unit.LengthM == null || unit.WidthM == null || unit.HeightM == null)
                    throw new InvalidOperationException($"GA result snapshot canonical LiftUnitId '{unit.LiftUnitId}' is missing required dimensions.");
                if (string.IsNullOrWhiteSpace(unit.ElementName)
                    || string.IsNullOrWhiteSpace(unit.ElementType)
                    || string.IsNullOrWhiteSpace(unit.MaterialType)
                    || string.IsNullOrWhiteSpace(unit.SharedDataHash))
                {
                    throw new InvalidOperationException($"GA result snapshot canonical LiftUnitId '{unit.LiftUnitId}' is missing required shared fields.");
                }

                var recalculated = CanonicalLiftUnitSnapshotBuilder.ComputeSharedDataHash(unit);
                if (!string.Equals(unit.SharedDataHash, recalculated, StringComparison.Ordinal))
                    throw new InvalidOperationException($"GA result snapshot canonical LiftUnitId '{unit.LiftUnitId}' failed shared-data hash validation.");
            }
        }

        var unknownOutcome = outcomeIds.FirstOrDefault(id => !unitIds.Contains(id));
        if (!string.IsNullOrWhiteSpace(unknownOutcome))
            throw new InvalidOperationException($"GA result snapshot final outcome references unknown LiftUnitId '{unknownOutcome}'.");

        var missingOutcome = unitIds.FirstOrDefault(id => !outcomeIds.Contains(id, StringComparer.Ordinal));
        if (!string.IsNullOrWhiteSpace(missingOutcome))
            throw new InvalidOperationException($"GA result snapshot is missing final outcome for LiftUnitId '{missingOutcome}'.");
    }

    private static IReadOnlyList<LiftingResultSourceRowDto> BuildMatrixRows(
        GaResultSnapshotDto snapshot,
        Func<GaElementEvaluationSnapshotDto, bool> predicate) =>
        snapshot.Positions
            .OrderBy(position => position.SetupIndex)
            .SelectMany(position => position.Evaluations
                .Where(predicate)
                .Select(evaluation => ToRow(snapshot, position, evaluation)))
            .ToList();

    private static (GaPositionSnapshotDto? position, GaElementEvaluationSnapshotDto? evaluation) FindEvaluation(
        GaResultSnapshotDto snapshot,
        string? evaluationId)
    {
        if (string.IsNullOrWhiteSpace(evaluationId))
            return (null, null);

        foreach (var position in snapshot.Positions)
        {
            var evaluation = position.Evaluations.FirstOrDefault(item => item.EvaluationId == evaluationId);
            if (evaluation != null)
                return (position, evaluation);
        }

        return (null, null);
    }

    private static LiftingResultSourceRowDto ToRow(
        GaResultSnapshotDto snapshot,
        GaPositionSnapshotDto position,
        GaElementEvaluationSnapshotDto evaluation,
        GaFinalOutcomeSnapshotDto? outcome = null)
    {
        var unit = snapshot.LiftUnits.FirstOrDefault(item => item.LiftUnitId == evaluation.LiftUnitId);
        var assignedSetup = outcome?.FinalOwnerPositionId != null
            ? snapshot.Positions.FirstOrDefault(item => item.PositionId == outcome.FinalOwnerPositionId)?.SetupIndex ?? position.SetupIndex
            : position.SetupIndex;
        var elementMark = FirstNonEmpty(unit?.CanonicalMark, unit?.ElementMark, unit?.ElementName, Placeholder);
        var values = CreateCommonValues(snapshot, unit, evaluation, outcome, position, assignedSetup);

        return new LiftingResultSourceRowDto
        {
            Values = values,
            SourceType = $"GA Snapshot ({snapshot.Run.OptimizationRunId})",
            ElementId = evaluation.LiftUnitId,
            ElementMark = elementMark,
            CanonicalMark = unit?.CanonicalMark,
            MarkSource = unit?.MarkSource,
            MarkConfidence = unit?.MarkConfidence,
            IsExplicitMark = unit?.IsExplicitMark ?? false,
            ElementType = unit?.ElementType,
            StoreyOrElevation = unit?.StoreyOrElevation,
            Timestamp = snapshot.Run.CompletedAtUtc?.ToString("O", CultureInfo.InvariantCulture),
            ProjectName = snapshot.Project.ProjectName,
            IfcFileName = snapshot.Model.IfcFileName,
            CraneNameOrId = snapshot.Crane.CraneName,
            AssignedSetupNo = assignedSetup,
            Radius = evaluation.RadiusM ?? evaluation.BestFailedRequiredRadiusM ?? 0,
            BoomLength = evaluation.BoomLengthM ?? evaluation.BestFailedRequiredBoomLengthM ?? 0,
            RequiredCapacity = evaluation.RequiredCapacityTon ?? 0,
            AvailableCapacity = evaluation.AvailableCapacityTon ?? 0,
            InternalLiftStatus = outcome?.FinalLiftStatus ?? evaluation.LiftStatus,
            IsFinalAssignedToPoint = evaluation.IsFinalAssignedToPoint,
            IsFinalAssigned = evaluation.IsFinalAssigned,
            ParentAssemblyGlobalId = unit?.ParentAssemblyGlobalId,
            MainPartGlobalId = unit?.MainPartGlobalId,
            AssemblyLiftWeight = unit?.LiftWeightTon,
            AssemblyLiftCOG = ResolvePhysicalCog(unit) != null
                ? new DomainPoint3D(ResolvePhysicalCog(unit)!.X, ResolvePhysicalCog(unit)!.Y, ResolvePhysicalCog(unit)!.Z)
                : null,
            AssemblyMemberIds = unit?.AssemblyMemberIds
        };
    }

    private static LiftingResultSourceRowDto ToOutcomeOnlyRow(GaResultSnapshotDto snapshot, GaFinalOutcomeSnapshotDto outcome)
    {
        var unit = snapshot.LiftUnits.FirstOrDefault(item => item.LiftUnitId == outcome.LiftUnitId);
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OptimizationRunId"] = snapshot.Run.OptimizationRunId,
            ["GARunTimestamp"] = snapshot.Run.CompletedAtUtc?.ToString("O", CultureInfo.InvariantCulture),
            ["GARunHealthLabel"] = snapshot.Run.Status,
            ["ProjectName"] = snapshot.Project.ProjectName,
            ["IfcFileName"] = snapshot.Model.IfcFileName,
            ["CraneNameOrId"] = snapshot.Crane.CraneName,
            ["TotalTargetElements"] = snapshot.Summary.TotalTargetLiftUnitCount,
            ["SelectedElements"] = snapshot.Summary.TotalTargetLiftUnitCount,
            ["LiftedElements"] = snapshot.Summary.LiftedLiftUnitCount,
            ["FailedElements"] = snapshot.Summary.UnliftedLiftUnitCount,
            ["CoverageRatio"] = snapshot.Summary.CoverageRatio.ToString("P2", CultureInfo.InvariantCulture),
            ["FinalCraneSetupCount"] = snapshot.Summary.PositionCount,
            ["ElementId"] = outcome.LiftUnitId,
            ["ElementMark"] = FirstNonEmpty(unit?.CanonicalMark, unit?.ElementMark, unit?.ElementName, Placeholder),
            ["ElementName"] = FirstNonEmpty(unit?.ElementName, Placeholder),
            ["ElementType"] = FirstNonEmpty(unit?.ElementType, Placeholder),
            ["Material"] = FirstNonEmpty(unit?.RawMaterialValue, unit?.Material, Placeholder),
            ["MaterialType"] = FirstNonEmpty(unit?.MaterialType, Placeholder),
            ["MaterialGrade"] = FirstNonEmpty(unit?.MaterialGrade, Placeholder),
            ["Length"] = unit?.LengthM ?? (object)"Unavailable in this saved run",
            ["Width"] = unit?.WidthM ?? (object)"Unavailable in this saved run",
            ["Height"] = unit?.HeightM ?? (object)"Unavailable in this saved run",
            ["VolumeM3"] = unit?.VolumeM3 ?? (object)"Unavailable in this saved run",
            ["CogX"] = ResolvePhysicalCog(unit)?.X,
            ["CogY"] = ResolvePhysicalCog(unit)?.Y,
            ["CogZ"] = ResolvePhysicalCog(unit)?.Z,
            ["AssemblyMemberCount"] = unit?.AssemblyMemberCount > 0 ? unit.AssemblyMemberCount : unit?.AssemblyMemberIds?.Count ?? 1,
            ["Weight"] = unit?.LiftWeightTon,
            ["LiftStatus"] = LiftingResultReportDataProvider.MapLiftStatus(outcome.FinalLiftStatus),
            ["IsLifted"] = outcome.IsLifted ? "Yes" : "No",
            ["FailureReason"] = FirstNonEmpty(outcome.FailureReason, Placeholder),
            ["FailureCategory"] = FirstNonEmpty(outcome.FailureCategory, Placeholder),
            ["SuggestedAction"] = FirstNonEmpty(outcome.SuggestedAction, Placeholder),
            ["ParentAssemblyGlobalId"] = unit?.ParentAssemblyGlobalId,
            ["MainPartGlobalId"] = unit?.MainPartGlobalId,
            ["AssemblyLiftWeight"] = unit?.LiftWeightTon,
            ["AssemblyMemberIds"] = unit?.AssemblyMemberIds != null ? string.Join(", ", unit.AssemblyMemberIds) : null,
            ["SharedDataHash"] = unit?.SharedDataHash
        };

        return new LiftingResultSourceRowDto
        {
            Values = values,
            SourceType = $"GA Snapshot ({snapshot.Run.OptimizationRunId})",
            ElementId = outcome.LiftUnitId,
            ElementMark = FirstNonEmpty(unit?.CanonicalMark, unit?.ElementMark, unit?.ElementName, Placeholder),
            CanonicalMark = unit?.CanonicalMark,
            ElementType = unit?.ElementType,
            Timestamp = snapshot.Run.CompletedAtUtc?.ToString("O", CultureInfo.InvariantCulture),
            ProjectName = snapshot.Project.ProjectName,
            IfcFileName = snapshot.Model.IfcFileName,
            CraneNameOrId = snapshot.Crane.CraneName,
            InternalLiftStatus = outcome.FinalLiftStatus,
            ParentAssemblyGlobalId = unit?.ParentAssemblyGlobalId,
            MainPartGlobalId = unit?.MainPartGlobalId,
            AssemblyLiftWeight = unit?.LiftWeightTon,
            AssemblyMemberIds = unit?.AssemblyMemberIds
        };
    }

    private static Dictionary<string, object?> CreateCommonValues(
        GaResultSnapshotDto snapshot,
        CanonicalLiftUnitSnapshotDto? unit,
        GaElementEvaluationSnapshotDto evaluation,
        GaFinalOutcomeSnapshotDto? outcome,
        GaPositionSnapshotDto position,
        int assignedSetup)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OptimizationRunId"] = snapshot.Run.OptimizationRunId,
            ["StageRunId"] = snapshot.Run.FinalStageRunId,
            ["SourceKind"] = snapshot.Capabilities.SourceKind,
            ["GARunTimestamp"] = snapshot.Run.CompletedAtUtc?.ToString("O", CultureInfo.InvariantCulture),
            ["GARunHealthLabel"] = snapshot.Run.Status,
            ["ProjectName"] = snapshot.Project.ProjectName,
            ["IfcFileName"] = snapshot.Model.IfcFileName,
            ["CraneNameOrId"] = snapshot.Crane.CraneName,
            ["TotalTargetElements"] = snapshot.Summary.TotalTargetLiftUnitCount,
            ["SelectedElements"] = snapshot.Summary.TotalTargetLiftUnitCount,
            ["LiftedElements"] = snapshot.Summary.LiftedLiftUnitCount,
            ["FailedElements"] = snapshot.Summary.UnliftedLiftUnitCount,
            ["CoverageRatio"] = snapshot.Summary.CoverageRatio.ToString("P2", CultureInfo.InvariantCulture),
            ["FinalCraneSetupCount"] = snapshot.Summary.PositionCount,
            ["PopulationSize"] = snapshot.EffectiveFinalSettings.PopulationSize,
            ["ConfiguredGenerations"] = snapshot.EffectiveFinalSettings.MaxGenerations,
            ["ElementId"] = evaluation.LiftUnitId,
            ["ElementID"] = evaluation.LiftUnitId,
            ["ElementMark"] = FirstNonEmpty(unit?.CanonicalMark, unit?.ElementMark, unit?.ElementName, Placeholder),
            ["ElementName"] = FirstNonEmpty(unit?.ElementName, Placeholder),
            ["ElementType"] = FirstNonEmpty(unit?.ElementType, Placeholder),
            ["Material"] = FirstNonEmpty(unit?.RawMaterialValue, unit?.Material, Placeholder),
            ["MaterialType"] = FirstNonEmpty(unit?.MaterialType, Placeholder),
            ["MaterialGrade"] = FirstNonEmpty(unit?.MaterialGrade, Placeholder),
            ["Length"] = unit?.LengthM ?? (object)"Unavailable in this saved run",
            ["Width"] = unit?.WidthM ?? (object)"Unavailable in this saved run",
            ["Height"] = unit?.HeightM ?? (object)"Unavailable in this saved run",
            ["VolumeM3"] = unit?.VolumeM3 ?? (object)"Unavailable in this saved run",
            ["CogX"] = ResolvePhysicalCog(unit)?.X,
            ["CogY"] = ResolvePhysicalCog(unit)?.Y,
            ["CogZ"] = ResolvePhysicalCog(unit)?.Z,
            ["AssemblyMemberCount"] = unit?.AssemblyMemberCount > 0 ? unit.AssemblyMemberCount : unit?.AssemblyMemberIds?.Count ?? 1,
            ["StoreyOrElevation"] = FirstNonEmpty(unit?.StoreyOrElevation, Placeholder),
            ["Weight"] = evaluation.ElementWeightTon ?? unit?.LiftWeightTon,
            ["ElementWeight"] = evaluation.ElementWeightTon ?? unit?.LiftWeightTon,
            ["HookRiggingWeight"] = evaluation.HookRiggingWeightTon,
            ["AssignedSetupIndex"] = assignedSetup,
            ["AssignedSetupNo"] = assignedSetup,
            ["AssignedGaPoint"] = FirstNonEmpty(evaluation.FinalOwnerPointId, evaluation.AssignedPointId, position.PositionId),
            ["AssignedCraneX"] = position.LocalPosition.X,
            ["AssignedCraneY"] = position.LocalPosition.Y,
            ["AssignedCraneZ"] = position.LocalPosition.Z,
            ["BestRadius"] = evaluation.RadiusM ?? evaluation.BestFailedRequiredRadiusM,
            ["RequiredRadius"] = evaluation.RequiredRadiusM ?? evaluation.BestFailedRequiredRadiusM,
            ["SelectedChartRadius"] = evaluation.SelectedChartRadiusM,
            ["RequiredBoomLength"] = evaluation.RequiredBoomLengthM ?? evaluation.BestFailedRequiredBoomLengthM,
            ["SelectedChartBoomLength"] = evaluation.SelectedChartBoomLengthM,
            ["RequiredCapacity"] = evaluation.RequiredCapacityTon,
            ["RawChartCapacity"] = evaluation.RawChartCapacityTon,
            ["AvailableCapacity"] = evaluation.AvailableCapacityTon,
            ["CapacityUtilizationPercent"] = evaluation.CapacityUtilizationPercent ?? (object)Placeholder,
            ["CapacityMargin"] = evaluation.SafetyMarginTon,
            ["LiftStatus"] = LiftingResultReportDataProvider.MapLiftStatus(outcome?.FinalLiftStatus ?? evaluation.LiftStatus),
            ["IsLifted"] = outcome?.IsLifted == true || evaluation.IsFinalAssignedToPoint ? "Yes" : "No",
            ["FailureReason"] = FirstNonEmpty(outcome?.FailureReason, evaluation.FailureReason, Placeholder),
            ["FailureCategory"] = FirstNonEmpty(outcome?.FailureCategory, evaluation.FailureCategory, Placeholder),
            ["SuggestedAction"] = FirstNonEmpty(outcome?.SuggestedAction, evaluation.SuggestedAction, Placeholder),
            ["ReachStatus"] = evaluation.ReachStatus,
            ["ChartStatus"] = evaluation.ChartStatus,
            ["IsExactMatch"] = BoolText(evaluation.IsExactMatch),
            ["IsSteppedRadius"] = BoolText(evaluation.IsSteppedRadius),
            ["IsBlankCell"] = BoolText(evaluation.IsBlankCell),
            ["IsZeroCell"] = BoolText(evaluation.IsZeroCell),
            ["SafetyMargin"] = evaluation.SafetyMarginTon,
            ["DeratingRatio"] = evaluation.DeratingRatio,
            ["SafetyFactor"] = evaluation.SafetyFactor,
            ["CombinedCapacityFactor"] = evaluation.CombinedCapacityFactor,
            ["WireLength"] = snapshot.EffectiveFinalSettings.WireLength,
            ["LookupMode"] = evaluation.LookupMode,
            ["CraneBaseX"] = evaluation.CraneBaseLocal?.X,
            ["CraneBaseY"] = evaluation.CraneBaseLocal?.Y,
            ["CraneBaseZ"] = evaluation.CraneBaseLocal?.Z,
            ["SlewCenterX"] = evaluation.SlewCenterLocal?.X,
            ["SlewCenterY"] = evaluation.SlewCenterLocal?.Y,
            ["SlewCenterZ"] = evaluation.SlewCenterLocal?.Z,
            ["BoomHingeX"] = evaluation.BoomHingeLocal?.X,
            ["BoomHingeY"] = evaluation.BoomHingeLocal?.Y,
            ["BoomHingeZ"] = evaluation.BoomHingeLocal?.Z,
            ["LoadPointX"] = evaluation.LoadPointLocal?.X,
            ["LoadPointY"] = evaluation.LoadPointLocal?.Y,
            ["LoadPointZ"] = evaluation.LoadPointLocal?.Z,
            ["RadiusOriginType"] = evaluation.RadiusOriginType,
            ["BoomOriginType"] = evaluation.BoomOriginType,
            ["CandidatePointId"] = evaluation.CandidatePointId,
            ["CandidatePointIndex"] = evaluation.CandidatePointIndex >= 0 ? evaluation.CandidatePointIndex : Placeholder,
            ["IsFeasibleFromPoint"] = BoolText(evaluation.IsFeasibleFromPoint),
            ["IsFinalAssignedToPoint"] = BoolText(evaluation.IsFinalAssignedToPoint),
            ["AssignedPointId"] = FirstNonEmpty(evaluation.AssignedPointId, Placeholder),
            ["AssignedPointIndex"] = evaluation.AssignedPointIndex ?? (object)Placeholder,
            ["AssignmentReason"] = FirstNonEmpty(evaluation.AssignmentReason, Placeholder),
            ["AssignmentScore"] = evaluation.AssignmentScore,
            ["DuplicateAssignmentResolved"] = BoolText(evaluation.DuplicateAssignmentResolved),
            ["FeasiblePointIds"] = string.Join(",", evaluation.FeasiblePointIds),
            ["FinalOwnerPointId"] = FirstNonEmpty(evaluation.FinalOwnerPointId, Placeholder),
            ["IsFinalAssigned"] = BoolText(evaluation.IsFinalAssigned),
            ["BestFailedPointId"] = FirstNonEmpty(evaluation.BestFailedPointId, Placeholder),
            ["BestFailureReason"] = FirstNonEmpty(evaluation.BestFailureReason, Placeholder),
            ["HasBestFailedCandidate"] = BoolText(evaluation.HasBestFailedCandidate),
            ["BestFailedLiftStatus"] = LiftingResultReportDataProvider.MapLiftStatus(evaluation.BestFailedLiftStatus ?? string.Empty),
            ["BestFailedReachStatus"] = FirstNonEmpty(evaluation.BestFailedReachStatus, Placeholder),
            ["BestFailedChartStatus"] = FirstNonEmpty(evaluation.BestFailedChartStatus, Placeholder),
            ["BestFailedSafetyMargin"] = evaluation.BestFailedSafetyMarginTon ?? (object)Placeholder,
            ["BestFailedRequiredRadius"] = evaluation.BestFailedRequiredRadiusM ?? (object)Placeholder,
            ["BestFailedRequiredBoomLength"] = evaluation.BestFailedRequiredBoomLengthM ?? (object)Placeholder,
            ["CandidateBoomsChecked"] = FirstNonEmpty(evaluation.CandidateBoomsChecked, Placeholder),
            ["RejectedCandidates"] = FirstNonEmpty(evaluation.RejectedCandidates, Placeholder),
            ["SelectedVisualColumnIndex"] = evaluation.SelectedVisualColumnIndex ?? (object)Placeholder,
            ["SelectedVisualColumnHeader"] = FirstNonEmpty(evaluation.SelectedVisualColumnHeader, Placeholder),
            ["SelectedNumberOfLines"] = evaluation.SelectedNumberOfLines ?? (object)Placeholder,
            ["SelectedOperatingConditionLabel"] = FirstNonEmpty(evaluation.SelectedOperatingConditionLabel, Placeholder),
            ["ConservativeDuplicateFallback"] = BoolText(evaluation.IsConservativeDuplicateFallback),
            ["DuplicateAlternativeCount"] = evaluation.DuplicateAlternativeCount,
            ["DuplicateAlternativeSummary"] = FirstNonEmpty(evaluation.DuplicateAlternativeSummary, Placeholder),
            ["ParentAssemblyGlobalId"] = unit?.ParentAssemblyGlobalId,
            ["MainPartGlobalId"] = unit?.MainPartGlobalId,
            ["AssemblyLiftWeight"] = unit?.LiftWeightTon,
            ["AssemblyLiftCOG"] = FormatPoint(ResolvePhysicalCog(unit)),
            ["AssemblyMemberIds"] = unit?.AssemblyMemberIds != null ? string.Join(", ", unit.AssemblyMemberIds) : null,
            ["SharedDataHash"] = unit?.SharedDataHash
        };

        return values;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return Placeholder;
    }

    private static GaPointSnapshotDto? ResolvePhysicalCog(CanonicalLiftUnitSnapshotDto? unit)
    {
        if (unit == null)
            return null;

        var point = unit.Cog.PhysicalCogProjectM;
        if (double.IsFinite(point.X)
            && double.IsFinite(point.Y)
            && double.IsFinite(point.Z)
            && (Math.Abs(point.X) > double.Epsilon || Math.Abs(point.Y) > double.Epsilon || Math.Abs(point.Z) > double.Epsilon))
        {
            return point;
        }

        return unit.LiftCogLocal;
    }

    private static string? FormatPoint(GaPointSnapshotDto? point) =>
        point == null
            ? null
            : $"({point.X:F3}; {point.Y:F3}; {point.Z:F3})";

    private static string BoolText(bool value) => value ? "Yes" : "No";

}
