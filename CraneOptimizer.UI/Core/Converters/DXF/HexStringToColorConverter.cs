using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CraneOptimizer.UI.Core.Converters.DXF
{
    /// <summary>
    /// Converts a hex colour string e.g. "#E74C3C" to System.Windows.Media.Color.
    /// Used in the DXF layer list to display colour swatches from DxfLayerInfo.ColorHex.
    /// WPF's type converter handles literal strings in markup but NOT data-bound strings —
    /// this explicit converter is required for bindings to Color properties.
    /// </summary>
    [ValueConversion(typeof(string), typeof(Color))]
    public class HexStringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try { return (Color)ColorConverter.ConvertFromString(hex); }
                catch { /* fall through */ }
            }
            return Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}