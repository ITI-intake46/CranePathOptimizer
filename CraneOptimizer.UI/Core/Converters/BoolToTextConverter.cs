using System.Globalization;
using System.Windows.Data;

namespace CraneOptimizer.UI.Core.Converters
{
    public class BoolToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool boolValue || parameter is not string paramStr)
                return string.Empty;

            var parts = paramStr.Split('|');
            if (parts.Length != 2)
                return string.Empty;

            return boolValue ? parts[0].Trim() : parts[1].Trim();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
