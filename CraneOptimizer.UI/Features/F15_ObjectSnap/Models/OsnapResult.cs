using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Models
{
    /// <summary>
    /// Immutable result returned by <c>IOsnapService.Snap()</c>.
    /// Carries everything needed for the visual indicator and the drawing handler.
    /// </summary>
    /// <param name="SnapType">Which snap type was matched.</param>
    /// <param name="WorldPoint">The snapped 3-D world coordinate to use as the actual pick point.</param>
    /// <param name="ScreenPoint">Projected 2-D canvas position for the indicator overlay (null if projection failed).</param>
    /// <param name="Source">Geometry source that produced the snap (Zone / DXF / IFC / Grid).</param>
    public sealed record OsnapResult(
        OsnapType   SnapType,
        Point3D     WorldPoint,
        Point?      ScreenPoint,
        OsnapSource Source);
}
