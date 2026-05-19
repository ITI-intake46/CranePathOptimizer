using CraneOptimizer.Domain.Units;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CraneOptimizer.UI.Core.Converters
{
    public class MassDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double tons) return string.Empty;

            try
            {
                var settings = App.ServiceProvider.GetRequiredService<IUnitSettingsService>().Current;
                return UnitSystem.FormatMass(tons, settings, culture);
            }
            catch
            {
                // Fallback in design mode or if service is unavailable
                return $"{tons:F2} t";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string strValue) return DependencyProperty.UnsetValue;

            try
            {
                var settings = App.ServiceProvider.GetRequiredService<IUnitSettingsService>().Current;
                
                // Strip the unit symbol if it's there
                var symbol = UnitSystem.MassSymbol(settings.DisplayMassUnit);
                var cleanStr = strValue.Replace(symbol, "").Trim();

                if (double.TryParse(cleanStr, NumberStyles.Any, culture, out double displayValue))
                {
                    return UnitSystem.ToTons(displayValue, settings.DisplayMassUnit);
                }
            }
            catch
            {
                // Fallback
                var cleanStr = strValue.Replace("t", "").Trim();
                if (double.TryParse(cleanStr, NumberStyles.Any, culture, out double displayValue))
                {
                    return displayValue;
                }
            }

            return DependencyProperty.UnsetValue;
        }
    }
}
