using CraneOptimizer.Domain.Schemas;
using System;
using System.Globalization;
using System.Windows.Data;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Views
{
    public class ElementWeightDisplayConverter : IValueConverter
    {
        private const double ConcreteDensityKgPerCubicMeter = 2500;
        private const double KgPerTon = 1000;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not PrecastElement element)
                return "-";

            if (element.ElementWT.HasValue && element.ElementWT.Value > 0)
                return FormatWeight(element.ElementWT.Value, estimated: false, culture);

            if (element.ElementVolume.HasValue && element.ElementVolume.Value > 0)
            {
                var volumeM3 = element.ElementVolume.Value / 1_000_000_000d;
                var estimatedKg = volumeM3 * ConcreteDensityKgPerCubicMeter;
                return FormatWeight(estimatedKg, estimated: true, culture);
            }

            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        private static string FormatWeight(double kg, bool estimated, CultureInfo culture)
        {
            var prefix = estimated ? "~" : string.Empty;
            if (kg >= KgPerTon)
                return $"{prefix}{(kg / KgPerTon).ToString("N2", culture)} t";
            return $"{prefix}{kg.ToString("N0", culture)} kg";
        }
    }
}
