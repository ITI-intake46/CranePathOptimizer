using CraneOptimizer.Domain.Schemas;
using System;
using System.Globalization;

namespace CraneOptimizer.UI.Features.F16_Filter.Services
{
    internal static class ElementWeightCalculator
    {
        private const double CubicMillimetersPerCubicMeter = 1_000_000_000d;
        private const double ConcreteDensityKgPerCubicMeter = 2500d;
        private const double SteelDensityKgPerCubicMeter = 7850d;
        private const double AluminumDensityKgPerCubicMeter = 2700d;
        private const double GlassDensityKgPerCubicMeter = 2500d;
        private const double TimberDensityKgPerCubicMeter = 600d;

        public static double GetWeightKg(PrecastElement element)
        {
            if (element.ElementWT.HasValue && element.ElementWT.Value > 0d)
                return element.ElementWT.Value;

            var volumeM3 = GetVolumeCubicMeters(element);
            if (volumeM3 <= 0d)
                return 0d;

            return volumeM3 * GetDensityKgPerCubicMeter(element);
        }

        public static bool IsEstimated(PrecastElement element) =>
            !(element.ElementWT.HasValue && element.ElementWT.Value > 0d)
            && GetWeightKg(element) > 0d;

        public static string FormatWeight(PrecastElement element, CultureInfo? culture = null)
        {
            var weight = GetWeightKg(element);
            if (weight <= 0d)
                return "0 kg";

            return FormatWeight(weight, IsEstimated(element), culture);
        }

        public static string FormatWeight(double weightKg, bool estimated = false, CultureInfo? culture = null)
        {
            culture ??= CultureInfo.CurrentCulture;
            var prefix = estimated ? "~" : string.Empty;
            return weightKg >= 1000d
                ? $"{prefix}{(weightKg / 1000d).ToString("N2", culture)} t"
                : $"{prefix}{weightKg.ToString("N0", culture)} kg";
        }

        private static double GetVolumeCubicMeters(PrecastElement element)
        {
            if (element.ElementVolume.HasValue && element.ElementVolume.Value > 0d)
                return element.ElementVolume.Value / CubicMillimetersPerCubicMeter;

            if ((element.BBoxLength ?? 0d) <= 0d
                || (element.BBoxWidth ?? 0d) <= 0d
                || (element.BBoxHeight ?? 0d) <= 0d)
                return 0d;

            return element.BBoxLength!.Value
                * element.BBoxWidth!.Value
                * element.BBoxHeight!.Value
                / CubicMillimetersPerCubicMeter;
        }

        private static double GetDensityKgPerCubicMeter(PrecastElement element)
        {
            var source = $"{element.ElementMaterial} {element.ElementType}".ToUpperInvariant();

            if (ContainsAny(source, "STEEL", "REBAR", "REINFORC"))
                return SteelDensityKgPerCubicMeter;
            if (ContainsAny(source, "ALUMIN"))
                return AluminumDensityKgPerCubicMeter;
            if (ContainsAny(source, "GLASS"))
                return GlassDensityKgPerCubicMeter;
            if (ContainsAny(source, "TIMBER", "WOOD"))
                return TimberDensityKgPerCubicMeter;

            return ConcreteDensityKgPerCubicMeter;
        }

        private static bool ContainsAny(string source, params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                if (source.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
