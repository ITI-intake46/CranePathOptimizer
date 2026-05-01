using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CraneOptimizer.UI.Core.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool boolValue || parameter is not string paramStr)
                return Binding.DoNothing;

            var parts = paramStr.Split('|');
            if (parts.Length != 2)
                return Binding.DoNothing;

            var trueColor = parts[0].Trim();
            var falseColor = parts[1].Trim();

            var colorString = boolValue ? trueColor : falseColor;

            try
            {
                return (SolidColorBrush)new BrushConverter().ConvertFromString(colorString)!;
            }
            catch
            {
                return Binding.DoNothing;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
