using CraneOptimizer.Domain.Schemas;
using System;
using System.Globalization;
using System.Reflection;
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

        private static string GetDisplayName(PrecastElement element)
        {
            var name = GetStringProperty(element, "Name");
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            var tag = GetStringProperty(element, "Tag");
            if (!string.IsNullOrWhiteSpace(tag))
                return tag;

            if (!string.IsNullOrWhiteSpace(element.ElementMaterial))
                return element.ElementMaterial;

            if (!string.IsNullOrWhiteSpace(element.ElementType))
                return element.ElementType;

            return "Unnamed element";
        }

        private static string? GetStringProperty(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            return property?.GetValue(source)?.ToString();
        }
    }
}
