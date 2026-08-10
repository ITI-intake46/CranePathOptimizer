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
            var vertexSnapshot = new Point3D[vertices.Count];
            for (int index = 0; index < vertices.Count; index++)
                vertexSnapshot[index] = vertices[index];

            var segmentSnapshot =
                new (Point3D A, Point3D B)[segments.Count];
            for (int index = 0; index < segments.Count; index++)
                segmentSnapshot[index] = segments[index];

            Vertices = vertexSnapshot;
            Segments = segmentSnapshot;
        }

        public string ElementId { get; }
        public IReadOnlyList<Point3D> Vertices { get; }
        public IReadOnlyList<(Point3D A, Point3D B)> Segments { get; }
    }
}
