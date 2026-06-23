namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class LiftingAccessoriesInfoDto
{
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
}
