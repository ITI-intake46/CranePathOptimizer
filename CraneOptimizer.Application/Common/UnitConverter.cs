using System;

namespace WPF_IFC_test1.Utilities
{
    public static class UnitConverter
    {
        #region Length Conversions

        /// <summary>
        /// Convert length from meters to target unit
        /// </summary>
        public static double ConvertLength(double valueInMeters, string targetUnit)
        {
            switch (targetUnit?.ToLower())
            {
                case "m":
                    return valueInMeters;
                case "mm":
                    return valueInMeters * 1000.0;
                case "cm":
                    return valueInMeters * 100.0;
                case "ft":
                case "feet":
                    return valueInMeters * 3.28084;
                case "in":
                case "inch":
                    return valueInMeters * 39.3701;
                default:
                    return valueInMeters;
            }
        }

        /// <summary>
        /// Convert length from source unit to meters
        /// </summary>
        public static double ConvertLengthToMeters(double value, string sourceUnit)
        {
            switch (sourceUnit?.ToLower())
            {
                case "m":
                    return value;
                case "mm":
                    return value / 1000.0;
                case "cm":
                    return value / 100.0;
                case "ft":
                case "feet":
                    return value / 3.28084;
                case "in":
                case "inch":
                    return value / 39.3701;
                default:
                    return value;
            }
        }

        #endregion

        #region Area Conversions

        /// <summary>
        /// Convert area from square meters to target unit
        /// </summary>
        public static double ConvertArea(double valueInSquareMeters, string targetUnit)
        {
            switch (targetUnit?.ToLower())
            {
                case "m²":
                case "m2":
                    return valueInSquareMeters;
                case "cm²":
                case "cm2":
                    return valueInSquareMeters * 10000.0;
                case "ft²":
                case "ft2":
                    return valueInSquareMeters * 10.7639;
                case "in²":
                case "in2":
                    return valueInSquareMeters * 1550.0;
                default:
                    return valueInSquareMeters;
            }
        }

        #endregion

        #region Volume Conversions

        /// <summary>
        /// Convert volume from cubic meters to target unit
        /// </summary>
        public static double ConvertVolume(double valueInCubicMeters, string targetUnit)
        {
            switch (targetUnit?.ToLower())
            {
                case "m³":
                case "m3":
                    return valueInCubicMeters;
                case "cm³":
                case "cm3":
                    return valueInCubicMeters * 1000000.0;
                case "ft³":
                case "ft3":
                    return valueInCubicMeters * 35.3147;
                case "in³":
                case "in3":
                    return valueInCubicMeters * 61023.7;
                default:
                    return valueInCubicMeters;
            }
        }

        #endregion

        #region Mass Conversions

        /// <summary>
        /// Convert mass from kilograms to target unit
        /// </summary>
        public static double ConvertMass(double valueInKilograms, string targetUnit)
        {
            switch (targetUnit?.ToLower())
            {
                case "kg":
                    return valueInKilograms;
                case "t":
                case "ton":
                case "tonne":
                    return valueInKilograms / 1000.0;
                case "lb":
                case "lbs":
                case "pound":
                    return valueInKilograms * 2.20462;
                case "ton (us)":
                    return valueInKilograms / 907.185;
                default:
                    return valueInKilograms;
            }
        }

        #endregion

        #region Pressure Conversions

        /// <summary>
        /// Convert pressure from kPa to target unit
        /// </summary>
        public static double ConvertPressure(double valueInKPa, string targetUnit)
        {
            switch (targetUnit?.ToLower())
            {
                case "kpa":
                    return valueInKPa;
                case "mpa":
                    return valueInKPa / 1000.0;
                case "psi":
                    return valueInKPa * 0.145038;
                case "bar":
                    return valueInKPa / 100.0;
                default:
                    return valueInKPa;
            }
        }

        #endregion

        #region Force Conversions

        /// <summary>
        /// Convert force from kN to target unit
        /// </summary>
        public static double ConvertForce(double valueInKN, string targetUnit)
        {
            switch (targetUnit?.ToLower())
            {
                case "kn":
                    return valueInKN;
                case "n":
                    return valueInKN * 1000.0;
                case "lbf":
                    return valueInKN * 224.809;
                case "kip":
                    return valueInKN * 0.224809;
                default:
                    return valueInKN;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get unit symbol for display
        /// </summary>
        public static string GetUnitSymbol(string unitType, string unitSystem)
        {
            if (unitSystem == "Metric")
            {
                switch (unitType?.ToLower())
                {
                    case "length": return "m";
                    case "area": return "m²";
                    case "volume": return "m³";
                    case "mass": return "kg";
                    case "pressure": return "kPa";
                    case "force": return "kN";
                    default: return "";
                }
            }
            else // Imperial
            {
                switch (unitType?.ToLower())
                {
                    case "length": return "ft";
                    case "area": return "ft²";
                    case "volume": return "ft³";
                    case "mass": return "lb";
                    case "pressure": return "psi";
                    case "force": return "lbf";
                    default: return "";
                }
            }
        }

        #endregion
    }
}