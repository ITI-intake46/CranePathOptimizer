using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Features.F16_Filter.Services
{
    /// <summary>
    /// Single source of truth for the engineering element scope displayed by the
    /// IFC Element Filter. AI context contributors must use these rules rather
    /// than recreating the predicate or category label independently.
    /// </summary>
    public static class ElementFilterScopeRules
    {
        public const string UnknownTypeLabel = "Unknown type";

        public static bool IsPlanningFilterable(PrecastElement element)
        {
            ArgumentNullException.ThrowIfNull(element);
            return element.Role == IfcElementRole.LiftAssembly && element.IncludeInGa;
        }

        public static string GetTypeLabel(PrecastElement element)
        {
            ArgumentNullException.ThrowIfNull(element);
            return string.IsNullOrWhiteSpace(element.ElementType)
                ? UnknownTypeLabel
                : element.ElementType.Trim();
        }
    }
}
