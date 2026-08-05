using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;

public static class CraneEnvelopeContainmentRadiusResolver
{
    public static double ResolveRenderedOperatingEnvelopeRadius(double craneSpecificationMaxRadius)
    {
        return IsPositiveFinite(craneSpecificationMaxRadius)
            ? craneSpecificationMaxRadius
            : 1.0;
    }

    public static double ResolveRequiredZoneContainmentRadius(GASettings settings)
    {
        double groundFootprintHalfDiagonal = IsPositiveFinite(settings.CraneFootprintRadius)
            ? settings.CraneFootprintRadius
            : 0.0;

        return ResolveRequiredZoneContainmentRadius(groundFootprintHalfDiagonal);
    }

    public static double ResolveRequiredZoneContainmentRadius(double groundFootprintHalfDiagonal)
    {
        return IsPositiveFinite(groundFootprintHalfDiagonal)
            ? groundFootprintHalfDiagonal
            : 0.0;
    }

    private static bool IsPositiveFinite(double value) =>
        value > 0.0 && !double.IsNaN(value) && !double.IsInfinity(value);
}
