using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    public sealed class IfcSnapGeometry
    {
        public IfcSnapGeometry(
            string elementId,
            IReadOnlyList<Point3D> vertices,
            IReadOnlyList<(Point3D A, Point3D B)> segments)
        {
            ElementId = elementId;
            Vertices = vertices;
            Segments = segments;
        }

        public string ElementId { get; }
        public IReadOnlyList<Point3D> Vertices { get; }
        public IReadOnlyList<(Point3D A, Point3D B)> Segments { get; }
    }
}
