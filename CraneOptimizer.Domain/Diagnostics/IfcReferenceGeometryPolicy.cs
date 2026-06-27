using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Domain.Diagnostics;

public static class IfcReferenceGeometryPolicy
{
    public static bool IsGridReference(PrecastElement? element) =>
        element != null && IsGridRawType(element.RawIfcType);

    public static bool IsGridRawType(string? rawIfcType) =>
        string.Equals(rawIfcType, "IfcGrid", StringComparison.OrdinalIgnoreCase)
        || string.Equals(rawIfcType, "IfcGridAxis", StringComparison.OrdinalIgnoreCase);
}
