namespace CraneOptimizer.Application.Reports.Services;

public enum WeightWarningCode
{
    None = 0,
    MissingExplicitWeight,
    EstimatedFromVolumeDensity,
    ZeroWeightFromIFC,
    AmbiguousUnit,
    SuspiciousWeight,
    RequiresUserApproval,
    AssemblyRollupFromChildren
}
