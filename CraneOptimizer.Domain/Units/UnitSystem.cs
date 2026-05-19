using System.Globalization;

namespace CraneOptimizer.Domain.Units
{
    /// <summary>
    /// Central conversion and formatting helpers.
    /// Canonical storage/runtime units are meters, square meters, cubic meters, and metric tons.
    /// </summary>
    public static class UnitSystem
    {
        public const double MillimetersPerMeter = 1000d;
        public const double CentimetersPerMeter = 100d;
        public const double InchesPerMeter = 39.37007874015748d;
        public const double FeetPerMeter = 3.280839895013123d;
        public const double KilogramsPerTon = 1000d;
        public const double SquareFeetPerSquareMeter = 10.763910416709722d;
        public const double CubicMillimetersPerCubicMeter = 1_000_000_000d;

        public static double ToMeters(double value, LengthUnit sourceUnit) => sourceUnit switch
        {
            LengthUnit.Millimeter => value / MillimetersPerMeter,
            LengthUnit.Centimeter => value / CentimetersPerMeter,
            LengthUnit.Meter => value,
            LengthUnit.Inch => value / InchesPerMeter,
            LengthUnit.Foot => value / FeetPerMeter,
            _ => value
        };

        public static double FromMeters(double meters, LengthUnit targetUnit) => targetUnit switch
        {
            LengthUnit.Millimeter => meters * MillimetersPerMeter,
            LengthUnit.Centimeter => meters * CentimetersPerMeter,
            LengthUnit.Meter => meters,
            LengthUnit.Inch => meters * InchesPerMeter,
            LengthUnit.Foot => meters * FeetPerMeter,
            _ => meters
        };

        public static double? ToMeters(double? value, LengthUnit sourceUnit) =>
            value.HasValue ? ToMeters(value.Value, sourceUnit) : null;

        public static double? FromMeters(double? meters, LengthUnit targetUnit) =>
            meters.HasValue ? FromMeters(meters.Value, targetUnit) : null;

        public static double ToSquareMeters(double value, LengthUnit sourceLengthUnit) =>
            value * Math.Pow(ToMeters(1d, sourceLengthUnit), 2);

        public static double FromSquareMeters(double squareMeters, AreaUnit targetUnit) => targetUnit switch
        {
            AreaUnit.SquareFoot => squareMeters * SquareFeetPerSquareMeter,
            _ => squareMeters
        };

        public static double ToCubicMeters(double value, LengthUnit sourceLengthUnit) =>
            value * Math.Pow(ToMeters(1d, sourceLengthUnit), 3);

        public static double ToTons(double value, MassUnit sourceUnit) => sourceUnit switch
        {
            MassUnit.Kilogram => value / KilogramsPerTon,
            MassUnit.Ton => value,
            _ => value
        };

        public static double FromTons(double tons, MassUnit targetUnit) => targetUnit switch
        {
            MassUnit.Kilogram => tons * KilogramsPerTon,
            MassUnit.Ton => tons,
            _ => tons
        };

        public static double? ToTons(double? value, MassUnit sourceUnit) =>
            value.HasValue ? ToTons(value.Value, sourceUnit) : null;

        public static double? FromTons(double? tons, MassUnit targetUnit) =>
            tons.HasValue ? FromTons(tons.Value, targetUnit) : null;

        public static string LengthSymbol(LengthUnit unit) => unit switch
        {
            LengthUnit.Millimeter => "mm",
            LengthUnit.Centimeter => "cm",
            LengthUnit.Meter => "m",
            LengthUnit.Inch => "in",
            LengthUnit.Foot => "ft",
            _ => "m"
        };

        public static string MassSymbol(MassUnit unit) => unit switch
        {
            MassUnit.Kilogram => "kg",
            MassUnit.Ton => "t",
            _ => "t"
        };

        public static string AreaSymbol(AreaUnit unit) => unit switch
        {
            AreaUnit.SquareFoot => "ft²",
            _ => "m²"
        };

        public static string FormatLength(double meters, UnitSettings settings, CultureInfo? culture = null)
        {
            culture ??= CultureInfo.CurrentCulture;
            var value = FromMeters(meters, settings.DisplayLengthUnit);
            return $"{value.ToString(NumberFormat(settings.DecimalPlaces), culture)} {LengthSymbol(settings.DisplayLengthUnit)}";
        }

        public static string FormatMass(double tons, UnitSettings settings, CultureInfo? culture = null)
        {
            culture ??= CultureInfo.CurrentCulture;
            var value = FromTons(tons, settings.DisplayMassUnit);
            return $"{value.ToString(NumberFormat(settings.DecimalPlaces), culture)} {MassSymbol(settings.DisplayMassUnit)}";
        }

        public static string FormatArea(double squareMeters, UnitSettings settings, CultureInfo? culture = null)
        {
            culture ??= CultureInfo.CurrentCulture;
            var value = FromSquareMeters(squareMeters, settings.DisplayAreaUnit);
            return $"{value.ToString(NumberFormat(settings.DecimalPlaces), culture)} {AreaSymbol(settings.DisplayAreaUnit)}";
        }

        private static string NumberFormat(int decimalPlaces) =>
            "N" + Math.Clamp(decimalPlaces, 0, 6).ToString(CultureInfo.InvariantCulture);
    }
}
