using CraneOptimizer.Domain.Schemas;
using System;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
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

        public static string GetDisplayName(PrecastElement element)
        {
            var type = CleanIfcType(element.ElementType);
            var rawName = string.Empty;

            if (!string.IsNullOrWhiteSpace(element.ElementName))
                rawName = element.ElementName;
            else if (!string.IsNullOrWhiteSpace(element.ElementMark))
                rawName = $"{element.ElementMark} - {type}";
            else if (!string.IsNullOrWhiteSpace(element.ElementRevitId))
                rawName = string.IsNullOrWhiteSpace(type) ? element.ElementRevitId : $"{type}-{element.ElementRevitId}";
            else
                rawName = string.IsNullOrWhiteSpace(type) ? GetFallbackName(element) : type;

            return CleanElementName(rawName);
        }

        private static string GetFallbackName(PrecastElement element)
        {
            var name = GetStringProperty(element, "Name");
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            var tag = GetStringProperty(element, "Tag");
            if (!string.IsNullOrWhiteSpace(tag))
                return tag;

            return "Precast Element";
        }

        private static string CleanElementName(string rawName)
        {
            return rawName.Replace("- Assembly", "")
                          .Replace(" Assembly", "")
                          .Replace(" : ", " - ")
                          .Trim();
        }

        private static string CleanIfcType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var type = value.Trim();
            if (type.Equals("IfcElementAssembly", StringComparison.OrdinalIgnoreCase))
                return "Assembly";

            if (type.StartsWith("Ifc", StringComparison.OrdinalIgnoreCase))
                type = type[3..];

            type = Regex.Replace(type, "([a-z])([A-Z])", "$1 $2");
            type = type.Replace("Standard Case", "", StringComparison.OrdinalIgnoreCase).Trim();

            return string.IsNullOrWhiteSpace(type) ? "Precast Element" : type;
        }

        private static string? GetStringProperty(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            return property?.GetValue(source)?.ToString();
        }
    }
}
