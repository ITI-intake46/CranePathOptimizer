namespace CraneOptimizer.Domain.Diagnostics
{
    public sealed record IfcReadingOptions(double? ConcreteDensityKgPerM3 = null, bool HideGrids = false);
}
