using System.Text.RegularExpressions;

namespace CraneOptimizer.Application.Reports.Services;

public sealed record CanonicalMaterialInfo(
    string MaterialType,
    string MaterialGrade,
    string RawMaterialValue,
    string MaterialSource);

public static partial class CanonicalMaterialResolver
{
    private const string Unknown = "Unknown";
    private const string EmptyGrade = "\u2014";

    public static CanonicalMaterialInfo Resolve(string? rawMaterial)
    {
        var raw = string.IsNullOrWhiteSpace(rawMaterial) || rawMaterial.Trim() == "-"
            ? string.Empty
            : rawMaterial.Trim();

        if (string.IsNullOrWhiteSpace(raw))
            return new CanonicalMaterialInfo(Unknown, EmptyGrade, string.Empty, "Missing");

        var grade = ConcreteGradeRegex().Match(raw);
        if (grade.Success)
            return new CanonicalMaterialInfo("Concrete", grade.Value.ToUpperInvariant(), raw, "IFC material");

        if (raw.Contains("concrete", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("precast", StringComparison.OrdinalIgnoreCase))
        {
            return new CanonicalMaterialInfo("Concrete", EmptyGrade, raw, "IFC material");
        }

        if (raw.Contains("rubber", StringComparison.OrdinalIgnoreCase))
            return new CanonicalMaterialInfo("Rubber", EmptyGrade, raw, "IFC material");

        if (raw.Contains("steel", StringComparison.OrdinalIgnoreCase))
            return new CanonicalMaterialInfo("Steel", EmptyGrade, raw, "IFC material");

        return new CanonicalMaterialInfo(raw, EmptyGrade, raw, "IFC material");
    }

    [GeneratedRegex(@"\bC\d{2,3}(?:/\d{2,3})?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConcreteGradeRegex();
}
