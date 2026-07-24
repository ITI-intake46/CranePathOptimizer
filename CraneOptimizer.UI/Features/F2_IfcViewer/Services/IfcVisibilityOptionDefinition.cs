using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Services
{
    public sealed record IfcVisibilityOptionDefinition(
        string Key,
        string Label,
        IReadOnlyList<string> IfcTypes,
        Func<IfcLoadOptionsDto, bool> IsHidden)
    {
        public bool MatchesType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return false;

            return IfcTypes.Any(ifcType => type.Contains(ifcType, StringComparison.OrdinalIgnoreCase));
        }

        public bool MatchesElement(PrecastElement element)
        {
            return MatchesType(element.RawIfcType) || MatchesType(element.ElementType);
        }
    }
}
