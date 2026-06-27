using CraneOptimizer.Domain.Schemas;
using System;
using System.Globalization;
using System.Windows.Data;

namespace CraneOptimizer.UI.Core.Converters
{
    public class CanonicalMarkConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not PrecastElement element)
                return string.Empty;

            var identity = CraneOptimizer.Application.Services.Identity.CanonicalMarkResolver.Resolve(element);
            return identity.DisplayLabel;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
