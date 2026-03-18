using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WPF_IFC_test1.Converters
{
    /// <summary>
    /// Converts a hex colour string (e.g. "#E74C3C") to a
    /// System.Windows.Media.Color struct so it can be bound to
    /// SolidColorBrush.Color in XAML data templates.
    ///
    /// WPF's type converter handles literal strings in markup, but does NOT
    /// run type converters for data bindings — so this explicit converter is
    /// required whenever ElementColor (string) is bound to a Color property.
    /// </summary>
    [ValueConversion(typeof(string), typeof(Color))]
    public class HexStringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    return (Color)ColorConverter.ConvertFromString(hex);
                }
                catch
                {
                    // fall through to default
                }
            }
            return Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}