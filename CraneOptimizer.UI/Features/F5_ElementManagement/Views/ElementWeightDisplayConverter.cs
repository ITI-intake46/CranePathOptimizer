using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using System;
using System.Globalization;
using System.Windows.Data;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Views
{
    public class ElementWeightDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not PrecastElement element)
                return "-";

            var weight = ElementWeightCalculator.GetWeightKg(element);
            return weight > 0d
                ? ElementWeightCalculator.FormatWeight(weight, ElementWeightCalculator.IsEstimated(element), culture)
                : "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
