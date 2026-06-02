using System.Collections.Generic;
using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IZoneGeometryService
    {
        List<DomainPoint3D> NormalizeVertices(IEnumerable<DomainPoint3D> vertices);
        bool ValidatePolygon(IReadOnlyList<DomainPoint3D> vertices, out string? warning);
        double CalculateSignedArea2D(IReadOnlyList<DomainPoint3D> vertices);
        List<DomainPoint3D> EnsureOrientation(List<DomainPoint3D> vertices, bool ccw = true);
        bool HasSelfIntersection(IReadOnlyList<DomainPoint3D> vertices);
        bool PointInPolygon(DomainPoint3D point, IReadOnlyList<DomainPoint3D> vertices);
        List<int> TriangulateSimplePolygon(IReadOnlyList<DomainPoint3D> vertices);
        bool ValidateTriangulation(IReadOnlyList<DomainPoint3D> vertices, List<int> indices, out string? error);
    }

    /// <summary>
    /// Transitional static fallback for components that cannot easily use DI (e.g. legacy tests, static helpers).
    /// Prefer constructor dependency injection over this static locator where possible.
    /// </summary>
    public static class ZoneGeometryServiceHolder
    {
        public static IZoneGeometryService Instance { get; set; } = null!;
    }
}
