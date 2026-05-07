using CraneOptimizer.Domain.Schemas;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Views
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
            var explicitName = GetStringProperty(element, "ElementName")
                ?? GetStringProperty(element, "Name")
                ?? GetStringProperty(element, "Tag");

            if (IsMeaningfulName(explicitName))
                return explicitName!;

            var typeName = CleanIfcType(element.ElementType);
            var materialName = CleanMaterial(element.ElementMaterial);

            if (!string.IsNullOrWhiteSpace(materialName) && !string.IsNullOrWhiteSpace(typeName))
                return $"{materialName} {typeName}";

            if (!string.IsNullOrWhiteSpace(typeName))
                return typeName;

            if (!string.IsNullOrWhiteSpace(materialName))
                return materialName;

            return "Precast Element";
        }

        private static string? GetStringProperty(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            var value = property?.GetValue(source)?.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static bool IsMeaningfulName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            if (trimmed.Length > 18 && Regex.IsMatch(trimmed, "^[A-Za-z0-9_$-]+$"))
                return false;

            return !trimmed.StartsWith("IfcElement", StringComparison.OrdinalIgnoreCase);
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

        private static string CleanMaterial(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var material = value.Trim()
                .Replace("_", " ")
                .Replace("-", " ");

            material = Regex.Replace(material, @"\s+", " ").Trim();
            if (material.Length == 0)
                return string.Empty;

            var words = material.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(word => !word.Equals("QCC", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            material = string.Join(" ", words);
            if (material.Equals("Concrete Precast", StringComparison.OrdinalIgnoreCase))
                return "Precast Concrete";

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(material.ToLower(CultureInfo.CurrentCulture));
        }
    }
}
