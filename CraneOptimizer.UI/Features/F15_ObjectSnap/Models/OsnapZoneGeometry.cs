using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Models
{
    public sealed record OsnapZoneGeometry(
        IReadOnlyList<Point3D> Vertices,
        bool IsClosed);
}
