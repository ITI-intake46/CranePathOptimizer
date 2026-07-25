using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using HelixToolkit.Wpf;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    public interface IOsnapService
    {
        OsnapSettings Settings { get; }
        event Action<bool>? MasterEnabledChanged;

        int SnapCacheBuiltCount { get; }
        int SnapPointCount { get; }
        double SnapBuildDurationMs { get; }
        double SnapMemoryDeltaMB { get; }

        void SetMasterEnabled(bool enabled);
        void SetSnapEnabled(OsnapType type, bool enabled);

        void SetZoneGeometry(IEnumerable<(IReadOnlyList<Point3D> Vertices, bool IsClosed)> polygons);
        void SetDxfSnapPoints(IEnumerable<Point3D> endpoints,
                              IEnumerable<(Point3D A, Point3D B)> segments,
                              IEnumerable<Point3D>? centers = null,
                              IEnumerable<Point3D>? quadrants = null,
                              IEnumerable<(Point3D A, Point3D B)>? midpointSegments = null);
        void SetPreparedDxfSnapPoints(
            List<Point3D> endpoints,
            List<(Point3D A, Point3D B)> segments,
            List<Point3D> centers,
            List<Point3D> quadrants,
            List<(Point3D A, Point3D B)> midpointSegments) =>
            SetDxfSnapPoints(endpoints, segments, centers, quadrants, midpointSegments);
        void SetIfcSnapPoints(IEnumerable<Point3D> vertices,
                              IEnumerable<(Point3D A, Point3D B)>? segments = null);
        void SetIfcSnapGeometry(IEnumerable<IfcSnapGeometry> geometry);

        /// <summary>
        /// Returns the best snap result for the given screen-space cursor position,
        /// or null when nothing is within the snap aperture (SnapRadiusPx pixels).
        /// </summary>
        OsnapResult? Snap(Point screenCursor, HelixViewport3D viewport);
    }
}
