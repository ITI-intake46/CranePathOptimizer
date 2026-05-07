using CraneOptimizer.Domain.Schemas;
using System;
using System.Globalization;
using System.Windows.Data;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Views
{
    public class ElementWeightDisplayConverter : IValueConverter
    {
        private const double ConcreteDensityKgPerCubicMeter = 2500;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not PrecastElement element)
                return "-";

            if (element.ElementWT.HasValue && element.ElementWT.Value > 0)
                return $"{element.ElementWT.Value.ToString("N0", culture)} kg";

            if (element.ElementVolume.HasValue && element.ElementVolume.Value > 0)
            {
                var estimatedWeight = element.ElementVolume.Value * ConcreteDensityKgPerCubicMeter;
                return $"{estimatedWeight.ToString("N0", culture)} kg";
            }

            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
