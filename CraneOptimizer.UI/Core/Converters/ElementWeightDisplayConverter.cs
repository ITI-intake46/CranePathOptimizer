using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Windows.Data;

namespace CraneOptimizer.UI.Core.Converters
{
    public class ElementWeightDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not PrecastElement element)
                return "-";

            var weight = ElementWeightCalculator.GetWeightTons(element);
            var settings = App.ServiceProvider.GetService<IUnitSettingsService>()?.Current ?? UnitSettings.Default;
            return weight > 0d
                ? ElementWeightCalculator.FormatWeight(weight, settings, ElementWeightCalculator.IsEstimated(element), culture)
                : "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
