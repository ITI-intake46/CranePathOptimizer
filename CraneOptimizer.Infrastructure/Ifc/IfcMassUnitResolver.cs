using CraneOptimizer.Domain.Diagnostics;
using System;
using System.Globalization;
using Xbim.Ifc4.Interfaces;

namespace CraneOptimizer.Infrastructure.Ifc
{
    /// <summary>
    /// IFC mass-unit resolution used by both diagnostics and production extraction.
    /// IFC physical-simple-quantity semantics are explicit-unit first, then project unit.
    /// No magnitude-based unit inference is permitted.
    /// </summary>
    internal static class IfcMassUnitResolver
    {
        internal sealed record Resolution(
            double RawValue,
            double? Tons,
            string DetectedUnit,
            string TrustStatus,
            string Source,
            string? Warning,
            bool IsResolved,
            bool HasUnitConflict);

        public static Resolution ResolveQuantity(
            double rawValue,
            IIfcNamedUnit? explicitQuantityUnit,
            IfcAuditMetadata projectMetadata)
        {
            if (explicitQuantityUnit != null)
            {
                if (!TryGetKilogramsPerUnit(explicitQuantityUnit, out var explicitUnitName, out var kgPerUnit))
                {
                    return new Resolution(
                        rawValue,
                        null,
                        explicitQuantityUnit.ToString() ?? "Unrecognized explicit quantity unit",
                        "CannotConvertUnit",
                        "IfcQuantityWeight.ExplicitUnit",
                        "The IFC quantity declares a mass unit that CraneOpt cannot convert. No magnitude inference was applied.",
                        false,
                        false);
                }

                bool conflict = projectMetadata.MassUnitWasDeclared
                    && projectMetadata.MassConversionFactor.HasValue
                    && !NearlyEqual(kgPerUnit, projectMetadata.MassConversionFactor.Value);

                return new Resolution(
                    rawValue,
                    rawValue * kgPerUnit / 1000d,
                    explicitUnitName,
                    conflict ? "ExplicitQuantityUnitOverridesProject" : "ExplicitQuantityUnit",
                    "IfcQuantityWeight.ExplicitUnit",
                    conflict
                        ? $"Quantity unit '{explicitUnitName}' overrides conflicting project mass unit '{projectMetadata.MassUnit}'."
                        : null,
                    true,
                    conflict);
            }

            if (projectMetadata.MassUnitWasDeclared
                && projectMetadata.MassConversionFactor.HasValue
                && double.IsFinite(projectMetadata.MassConversionFactor.Value)
                && projectMetadata.MassConversionFactor.Value > 0d)
            {
                return new Resolution(
                    rawValue,
                    rawValue * projectMetadata.MassConversionFactor.Value / 1000d,
                    string.IsNullOrWhiteSpace(projectMetadata.MassUnit) ? "ProjectMassUnit" : projectMetadata.MassUnit,
                    "ProjectMassUnit",
                    "IfcQuantityWeight.ProjectUnit",
                    null,
                    true,
                    false);
            }

            return new Resolution(
                rawValue,
                null,
                "Unspecified",
                "MissingUnit",
                "IfcQuantityWeight.UnresolvedUnit",
                "IfcQuantityWeight has no explicit unit and the IFC project declares no mass unit. The value is blocked from GA; no kg/tonne guess was made.",
                false,
                false);
        }

        public static Resolution ResolveProperty(
            double rawValue,
            string? unitText,
            IfcAuditMetadata projectMetadata,
            string sourceName)
        {
            if (TryGetKilogramsPerUnit(unitText, out var normalizedName, out var kgPerUnit))
            {
                bool conflict = projectMetadata.MassUnitWasDeclared
                    && projectMetadata.MassConversionFactor.HasValue
                    && !NearlyEqual(kgPerUnit, projectMetadata.MassConversionFactor.Value);

                return new Resolution(
                    rawValue,
                    rawValue * kgPerUnit / 1000d,
                    normalizedName,
                    conflict ? "ExplicitPropertyUnitOverridesProject" : "ExplicitPropertyUnit",
                    sourceName,
                    conflict
                        ? $"Property unit '{normalizedName}' overrides conflicting project mass unit '{projectMetadata.MassUnit}'."
                        : null,
                    true,
                    conflict);
            }

            if (!string.IsNullOrWhiteSpace(unitText))
            {
                return new Resolution(
                    rawValue,
                    null,
                    unitText.Trim(),
                    "CannotConvertUnit",
                    sourceName,
                    "The IFC property declares a mass unit that CraneOpt cannot convert. No magnitude inference was applied.",
                    false,
                    false);
            }

            if (projectMetadata.MassUnitWasDeclared
                && projectMetadata.MassConversionFactor.HasValue
                && double.IsFinite(projectMetadata.MassConversionFactor.Value)
                && projectMetadata.MassConversionFactor.Value > 0d)
            {
                return new Resolution(
                    rawValue,
                    rawValue * projectMetadata.MassConversionFactor.Value / 1000d,
                    string.IsNullOrWhiteSpace(projectMetadata.MassUnit) ? "ProjectMassUnit" : projectMetadata.MassUnit,
                    "ProjectMassUnit",
                    sourceName + ".ProjectUnit",
                    null,
                    true,
                    false);
            }

            return new Resolution(
                rawValue,
                null,
                "Unspecified",
                "MissingUnit",
                sourceName + ".UnresolvedUnit",
                "The IFC property has no explicit unit and the IFC project declares no mass unit. The value is blocked from GA; no magnitude inference was applied.",
                false,
                false);
        }

        public static bool TryGetKilogramsPerUnit(IIfcNamedUnit unit, out string unitName, out double kilogramsPerUnit)
        {
            unitName = unit.ToString() ?? string.Empty;
            kilogramsPerUnit = 0d;

            if (unit is IIfcSIUnit siUnit)
            {
                unitName = BuildSiUnitName(siUnit);
                return TryGetSiMassFactor(siUnit, out kilogramsPerUnit);
            }

            if (unit is IIfcConversionBasedUnit conversionUnit)
            {
                // A conversion-based unit is trustworthy only when its declared conversion
                // factor and base unit are themselves convertible.  Never accept a friendly
                // display name (for example "tonne") as a substitute for malformed IFC
                // conversion semantics.
                unitName = conversionUnit.Name.ToString();

                var factorText = conversionUnit.ConversionFactor?.ValueComponent?.ToString();
                if (!double.TryParse(factorText, NumberStyles.Any, CultureInfo.InvariantCulture, out var factor)
                    || !double.IsFinite(factor)
                    || factor <= 0d)
                {
                    return false;
                }

                if (conversionUnit.ConversionFactor?.UnitComponent is IIfcNamedUnit baseUnit
                    && TryGetKilogramsPerUnit(baseUnit, out _, out var baseKgPerUnit))
                {
                    kilogramsPerUnit = factor * baseKgPerUnit;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetKilogramsPerUnit(string? unitText, out string normalizedName, out double kilogramsPerUnit)
        {
            normalizedName = unitText?.Trim() ?? string.Empty;
            kilogramsPerUnit = 0d;
            if (string.IsNullOrWhiteSpace(unitText))
                return false;

            var text = unitText.Trim().ToUpperInvariant();
            if (text.Contains("KILOGRAM") || text == "KG" || text.Contains(" KGS"))
            {
                normalizedName = "kg";
                kilogramsPerUnit = 1d;
                return true;
            }

            if (text == "G" || text.Contains("GRAM") && !text.Contains("KILOGRAM"))
            {
                normalizedName = "g";
                kilogramsPerUnit = 0.001d;
                return true;
            }

            if (text.Contains("TONNE") || text == "TON" || text.Contains("METRIC TON") || text == "T")
            {
                normalizedName = "t";
                kilogramsPerUnit = 1000d;
                return true;
            }

            if (text.Contains("POUND") || text == "LB" || text == "LBS" || text.Contains(" LB"))
            {
                normalizedName = "lb";
                kilogramsPerUnit = 0.45359237d;
                return true;
            }

            return false;
        }

        private static string BuildSiUnitName(IIfcSIUnit unit)
        {
            var prefix = unit.Prefix?.ToString() ?? string.Empty;
            var name = unit.Name.ToString();
            return string.IsNullOrWhiteSpace(prefix) ? name : prefix + name;
        }

        private static bool TryGetSiMassFactor(IIfcSIUnit unit, out double kilogramsPerUnit)
        {
            kilogramsPerUnit = 0d;
            var name = unit.Name.ToString();
            var prefix = unit.Prefix?.ToString() ?? string.Empty;

            // IFC typically represents kilogram as KILO + GRAM.
            if (!name.Contains("GRAM", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("KILOGRAM", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (name.Equals("KILOGRAM", StringComparison.OrdinalIgnoreCase)
                || prefix.Contains("KILO", StringComparison.OrdinalIgnoreCase))
            {
                kilogramsPerUnit = 1d;
                return true;
            }

            if (string.IsNullOrWhiteSpace(prefix))
            {
                kilogramsPerUnit = 0.001d;
                return true;
            }

            if (prefix.Contains("MILLI", StringComparison.OrdinalIgnoreCase))
            {
                kilogramsPerUnit = 0.000001d;
                return true;
            }

            return false;
        }

        private static bool NearlyEqual(double left, double right)
        {
            var scale = Math.Max(1d, Math.Max(Math.Abs(left), Math.Abs(right)));
            return Math.Abs(left - right) <= scale * 1e-9;
        }
    }
}
