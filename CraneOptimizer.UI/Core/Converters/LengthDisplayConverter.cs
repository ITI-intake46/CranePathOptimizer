using CraneOptimizer.Domain.Units;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CraneOptimizer.UI.Core.Converters
{
    public class LengthDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double meters) return string.Empty;

            try
            {
                var settings = App.ServiceProvider.GetRequiredService<IUnitSettingsService>().Current;
                return UnitSystem.FormatLength(meters, settings, culture);
            }
            catch
            {
                // Fallback in design mode or if service is unavailable
                return $"{meters:F2} m";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string strValue) return DependencyProperty.UnsetValue;

            try
            {
                var settings = App.ServiceProvider.GetRequiredService<IUnitSettingsService>().Current;
                
                // Strip the unit symbol if it's there
                var symbol = UnitSystem.LengthSymbol(settings.DisplayLengthUnit);
                var cleanStr = strValue.Replace(symbol, "").Trim();

                if (double.TryParse(cleanStr, NumberStyles.Any, culture, out double displayValue))
                {
                    return UnitSystem.ToMeters(displayValue, settings.DisplayLengthUnit);
                }
            }
            catch
            {
                // Fallback
                var cleanStr = strValue.Replace("m", "").Trim();
                if (double.TryParse(cleanStr, NumberStyles.Any, culture, out double displayValue))
                {
                    return displayValue;
                }
            }

            return DependencyProperty.UnsetValue;
        }
    }
}
