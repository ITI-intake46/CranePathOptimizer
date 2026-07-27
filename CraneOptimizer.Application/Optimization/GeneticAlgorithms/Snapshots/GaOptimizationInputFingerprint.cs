using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;

/// <summary>
/// Produces the versioned, deterministic identity of every engineering input that can
/// materially affect a GA result. Runtime, diagnostics, and experiment metadata are
/// intentionally excluded.
/// </summary>
public static class GaOptimizationInputFingerprint
{
    public const int CurrentVersion = 2;

    public static readonly IReadOnlyList<string> RequiredComponentNames =
    [
        "Accessories",
        "CapacityFactors",
        "CraneSpecification",
        "GaSettings",
        "LoadChart",
        "Model",
        "OperatingMode",
        "TargetScope",
        "Zones"
    ];

    public static GaOptimizationInputFingerprintResult Build(PlacementInput input, GASettings settings)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(settings);

        var components = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["Accessories"] = Hash(Join(
                Number(settings.WireLength),
                Number(settings.AdditionalWt))),
            ["CapacityFactors"] = Hash(Join(
                Number(settings.DeratingRatio),
                Number(settings.SafetyFactor),
                Number(input.CraneSpec?.CraneDerating))),
            ["CraneSpecification"] = Hash(BuildCraneSpecification(input.CraneSpec)),
            ["GaSettings"] = Hash(BuildGaSettings(settings)),
            ["LoadChart"] = Hash(BuildLoadChart(input.CraneSpec?.LoadChart)),
            ["Model"] = Hash(Join(
                input.CanonicalSnapshotId,
                input.CanonicalPayloadHash,
                BuildElements(input.Elements))),
            ["OperatingMode"] = Hash(Join(
                settings.LoadChartNumberOfLinesConstraint,
                settings.LoadChartOperatingConditionConstraint,
                Number(settings.SlewOffsetX),
                Number(settings.SlewOffsetY),
                Number(settings.SlewOffsetZ),
                Number(settings.BoomHingeOffsetX),
                Number(settings.BoomHingeOffsetY),
                Number(settings.BoomHingeOffsetZ),
                input.CraneSpec?.OutriggerConfig,
                Number(input.CraneSpec?.MachineRotation))),
            ["TargetScope"] = Hash(Join(
                input.ScopeDefinitionHash,
                input.ProjectFeatureSnapshotId,
                input.RawElementCount,
                input.OptimizationScopeElementCount,
                input.RawGaEligibleElementCount,
                input.GroupedAssemblyCount,
                input.UngroupedElementCount,
                settings.EnableGroupedLifts,
                string.Join(";", (settings.TargetElementTypes ?? Array.Empty<string>())
                    .OrderBy(value => value, StringComparer.Ordinal)),
                string.Join(";", input.SubmittedCanonicalLiftUnits
                    .Select(unit => unit.LiftUnitId)
                    .OrderBy(value => value, StringComparer.Ordinal)))),
            ["Zones"] = Hash(BuildZones(input.Zones, input.ForbiddenZones))
        };

        var aggregate = Hash(string.Join(
            "\n",
            components.Select(pair => $"{pair.Key}={pair.Value}")));

        return new GaOptimizationInputFingerprintResult
        {
            Version = CurrentVersion,
            Aggregate = aggregate,
            Components = new Dictionary<string, string>(components, StringComparer.Ordinal)
        };
    }

    private static string BuildCraneSpecification(CraneSpecification? specification)
    {
        if (specification == null)
            return "<missing>";

        var crane = specification.Crane;
        return Join(
            specification.Id,
            specification.CraneId,
            Number(specification.MaxCapacity),
            Number(specification.MinRadius),
            Number(specification.MaxRadius),
            Number(specification.MinBoomLength),
            Number(specification.MaxBoomLength),
            Number(specification.CounterWeight),
            Number(specification.MachineRotation),
            specification.OutriggerConfig,
            specification.LoadChartStorageMode,
            specification.LoadChartCapacityPolicy,
            specification.LoadChartSourceDescription,
            specification.ExactLoadChartRawGridJson,
            crane?.Id,
            crane?.Name,
            crane?.Manufacturer,
            crane?.CraneType,
            Number(crane?.VehicleLength),
            Number(crane?.VehicleWidth),
            Number(crane?.VehicleHeight),
            Number(crane?.OutriggerLength),
            Number(crane?.OutriggerWidth));
    }

    private static string BuildGaSettings(GASettings settings) => Join(
        settings.PopulationSize,
        settings.MaxGenerations,
        Number(settings.MutationRate),
        Number(settings.MutationSigma),
        Number(settings.CrossoverRate),
        settings.RandomSeed,
        settings.MaxCranePositions,
        Number(settings.GridStep),
        Number(settings.CraneFootprintRadius),
        Number(settings.MinimumRelocationDistance),
        Number(settings.DuplicatePositionDistance),
        Number(settings.MutationExplorationMultiplier),
        settings.PositionSamplingAttempts,
        Number(settings.CoverageWeight),
        Number(settings.CoverageDominanceMultiplier),
        Number(settings.RelocationWeight),
        Number(settings.RadiusPenaltyWeight),
        Number(settings.RadiusQualityPenaltyWeight),
        Number(settings.OverweightPenaltyWeight),
        Number(settings.HeightPenaltyWeight),
        Number(settings.RadiusStdDevPenaltyWeight),
        Number(settings.BalancePenaltyWeight),
        Number(settings.SimilarPositionPenaltyWeight),
        Number(settings.RelocationDistancePenaltyWeight),
        Number(settings.MaxRadiusForNormalisation),
        settings.EarlyStoppingGenerations,
        Number(settings.ConvergenceThresholdPercent),
        settings.EnableAdaptiveSigma,
        Number(settings.SigmaCoolingExponent),
        Number(settings.MinimumMutationSigma),
        settings.EnableConcurrentOptimization,
        settings.EnableReachabilitySeeding,
        Number(settings.HotSpotSeedingRatio),
        settings.EnableMultiStageDiscovery,
        Number(settings.MultiStageCoverageTarget),
        Number(settings.AdaptiveRelocationScaleFactor),
        settings.UseHungarianAssignment);

    private static string BuildElements(IEnumerable<PrecastElement>? elements) =>
        string.Join(
            "\n",
            (elements ?? Array.Empty<PrecastElement>())
                .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
                .OrderBy(element => element.ElementID, StringComparer.Ordinal)
                .Select(element => Join(
                    element.ElementID,
                    element.ElementType,
                    element.RawIfcType,
                    element.ResolvedCategory,
                    Number(element.ElementWT),
                    Point(element.ElementCOG),
                    element.ParentGlobalId,
                    element.MainPartGlobalId,
                    string.Join(";", element.ChildGlobalIds.OrderBy(id => id, StringComparer.Ordinal)),
                    element.ValidationStatus)));

    private static string BuildLoadChart(IEnumerable<TargetPoint>? points) =>
        string.Join(
            "\n",
            (points ?? Array.Empty<TargetPoint>())
                .OrderBy(point => point.Radius)
                .ThenBy(point => point.BoomLength)
                .ThenBy(point => point.Capacity)
                .ThenBy(point => point.VisualColumnIndex ?? int.MaxValue)
                .ThenBy(point => point.VisualColumnHeader, StringComparer.Ordinal)
                .Select(point => Join(
                    Number(point.Radius),
                    Number(point.BoomLength),
                    Number(point.Capacity),
                    point.VisualColumnIndex,
                    point.VisualColumnHeader,
                    point.NumberOfLines,
                    point.OperatingConditionLabel)));

    private static string BuildZones(
        IEnumerable<GreenAreaZone>? allowed,
        IEnumerable<GreenAreaZone>? forbidden)
    {
        static IEnumerable<string> Project(IEnumerable<GreenAreaZone>? zones, string role) =>
            (zones ?? Array.Empty<GreenAreaZone>())
                .OrderBy(zone => zone.ZoneId)
                .Select(zone => Join(
                    role,
                    zone.ZoneId,
                    zone.Name,
                    zone.Type,
                    Number(zone.Area),
                    Number(zone.MinimumHeight),
                    Number(zone.MaximumHeight),
                    zone.OriginalAssignmentType,
                    string.Join(";", zone.Vertices.Select(Point))));

        return string.Join("\n", Project(allowed, "Allowed").Concat(Project(forbidden, "Forbidden")));
    }

    private static string Point(DomainPoint3D point) =>
        Join(Number(point.X), Number(point.Y), Number(point.Z));

    private static string Number(double? value) =>
        value?.ToString("R", CultureInfo.InvariantCulture) ?? "<null>";

    private static string Join(params object?[] values) =>
        string.Join("|", values.Select(value => value?.ToString() ?? "<null>"));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public sealed class GaOptimizationInputFingerprintResult
{
    public int Version { get; init; }
    public string Aggregate { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Components { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
}
