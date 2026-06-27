using CraneOptimizer.Domain.Schemas;
using System;
using System.Globalization;
using System.Windows.Data;

namespace CraneOptimizer.UI.Core.Converters
{
    public class ElementDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not PrecastElement element)
                return string.Empty;

            return GetDisplayName(element);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        public static string GetDisplayName(PrecastElement element) =>
            string.IsNullOrWhiteSpace(element.ElementName) ? string.Empty : element.ElementName.Trim();
    }
}
