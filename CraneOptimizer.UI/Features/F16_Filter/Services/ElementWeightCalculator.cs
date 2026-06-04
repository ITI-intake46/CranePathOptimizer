using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Units;
using System;
using System.Globalization;

namespace CraneOptimizer.UI.Features.F16_Filter.Services
{
    internal static class ElementWeightCalculator
    {
        private static double _concreteDensityKgPerCubicMeter = 2500d;
        public static double ConcreteDensityKgPerCubicMeter
        {
            get => _concreteDensityKgPerCubicMeter;
            set
            {
                if (Math.Abs(_concreteDensityKgPerCubicMeter - value) < 0.000001)
                    return;
                _concreteDensityKgPerCubicMeter = value;
                ConcreteDensityChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static event EventHandler? ConcreteDensityChanged;

        private const double SteelDensityKgPerCubicMeter = 7850d;
        private const double AluminumDensityKgPerCubicMeter = 2700d;
        private const double GlassDensityKgPerCubicMeter = 2500d;
        private const double TimberDensityKgPerCubicMeter = 600d;

        public static double GetWeightTons(PrecastElement element)
        {
            if (!IsEstimated(element))
            {
                if (element.ElementWT.HasValue && element.ElementWT.Value > 0d)
                    return element.ElementWT.Value;
            }

            var volumeM3 = GetVolumeCubicMeters(element);
            if (volumeM3 <= 0d)
                return 0d;

            return UnitSystem.ToTons(volumeM3 * GetDensityKgPerCubicMeter(element), MassUnit.Kilogram);
        }

        public static double GetWeightKg(PrecastElement element) =>
            UnitSystem.FromTons(GetWeightTons(element), MassUnit.Kilogram);

        public static bool IsEstimated(PrecastElement element) =>
            element.WeightTrustLevel != IfcWeightTrustLevel.ExplicitQuantity &&
            element.WeightTrustLevel != IfcWeightTrustLevel.ExplicitProperty;

        public static string FormatWeight(PrecastElement element, CultureInfo? culture = null)
        {
            var weight = GetWeightTons(element);
            if (weight <= 0d)
                return "0 t";

            return FormatWeight(weight, IsEstimated(element), culture);
        }

        public static string FormatWeight(double weightTons, bool estimated = false, CultureInfo? culture = null)
            => FormatWeight(weightTons, UnitSettings.Default, estimated, culture);

        public static string FormatWeight(
            double weightTons,
            UnitSettings settings,
            bool estimated = false,
            CultureInfo? culture = null)
        {
            culture ??= CultureInfo.CurrentCulture;
            var prefix = estimated ? "~" : string.Empty;
            return $"{prefix}{UnitSystem.FormatMass(weightTons, settings, culture)}";
        }

        private static double GetVolumeCubicMeters(PrecastElement element)
        {
            if (element.ElementVolume.HasValue && element.ElementVolume.Value > 0d)
                return element.ElementVolume.Value;

            if ((element.BBoxLength ?? 0d) <= 0d
                || (element.BBoxWidth ?? 0d) <= 0d
                || (element.BBoxHeight ?? 0d) <= 0d)
                return 0d;

            return element.BBoxLength!.Value
                * element.BBoxWidth!.Value
                * element.BBoxHeight!.Value;
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
