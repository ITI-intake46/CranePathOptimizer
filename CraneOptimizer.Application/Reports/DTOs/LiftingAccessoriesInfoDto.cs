namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class LiftingAccessoriesInfoDto
{
    public double? WireRopeLengthM { get; init; }
    public double? RiggingWireRopeLengthM { get; init; }
    public double? HookRiggingWeightTons { get; init; }
    public double? TotalBelowHookWeightTons { get; init; }
    public double? TotalLiftingAccessoriesWeightTons { get; init; }
    public int? NumberOfFallsOrLines { get; init; }
    public string? RiggingConfigurationName { get; init; }
    public string? Completeness { get; init; }
    public IReadOnlyList<LiftingAccessoryItemInfoDto> Items { get; init; } = Array.Empty<LiftingAccessoryItemInfoDto>();
    public double? HookBlockWeightTons { get; init; }
    public double? RiggingWeightTons { get; init; }
    public double? SlingsWeightTons { get; init; }
    public double? SpreaderBeamWeightTons { get; init; }
    public double? AdditionalWeightTons { get; init; }

    public double TotalAdditionalWeightTons =>
        (HookBlockWeightTons ?? 0)
        + (RiggingWeightTons ?? 0)
        + (SlingsWeightTons ?? 0)
        + (SpreaderBeamWeightTons ?? 0)
        + (AdditionalWeightTons ?? 0);

    public double VisibleTotalLiftingAccessoriesWeightTons =>
        TotalLiftingAccessoriesWeightTons ?? TotalAdditionalWeightTons;
}

public sealed class LiftingAccessoryItemInfoDto
{
    public string ItemType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double? WeightTons { get; init; }
    public double? LengthM { get; init; }
    public int? Quantity { get; init; }
}
