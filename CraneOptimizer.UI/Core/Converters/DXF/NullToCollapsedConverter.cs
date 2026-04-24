using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CraneOptimizer.UI.Core.Converters.DXF
{
    /// <summary>
    /// null / empty string → Collapsed  |  any non-null / non-empty value → Visible
    /// </summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NullToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;

            if (value is string s)
                return string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;

            // For int / collection Count — collapse on zero
            if (value is int i)
                return i == 0 ? Visibility.Collapsed : Visibility.Visible;

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}