using System;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Models
{
    public enum OsnapCameraType
    {
        Perspective,
        Orthographic
    }

    [Flags]
    public enum OsnapModeMask
    {
        None = 0,
        Endpoint = 1 << 0,
        Intersection = 1 << 1,
        Midpoint = 1 << 2,
        Center = 1 << 3,
        Quadrant = 1 << 4
    }

    /// <summary>
    /// Immutable drawing-plane data captured with the pointer event.
    /// No WPF visual, camera, viewport, or dispatcher object is retained.
    /// </summary>
    public sealed record OsnapReferencePlaneSnapshot(
        Point3D Origin,
        Vector3D Normal);

    public sealed record OsnapSourceVersionSnapshot(
        long Ifc,
        long Dxf,
        long CompletedZones,
        long ActiveZone);

    public sealed record OsnapGeometryVersionState(
        long Aggregate,
        OsnapSourceVersionSnapshot Sources);

    /// <summary>
    /// Immutable camera and pointer data used by worker-side projection math.
    /// Instances are captured on the UI thread and are safe to use from workers.
    /// </summary>
    public sealed record OsnapViewportSnapshot(
        OsnapCameraType CameraType,
        Point3D CameraPosition,
        Vector3D LookDirection,
        Vector3D UpDirection,
        double FieldOfViewOrWidth,
        double ViewportWidth,
        double ViewportHeight,
        double ScreenOffsetX,
        double ScreenOffsetY,
        double DpiScaleX,
        double DpiScaleY,
        long CameraVersion,
        long GeometryVersion,
        OsnapSourceVersionSnapshot SourceVersions,
        long SettingsVersion,
        long DrawingSessionVersion,
        Point CursorScreenPoint,
        OsnapReferencePlaneSnapshot ReferencePlane,
        OsnapModeMask EnabledModes,
        double SnapRadiusPx,
        int MaxIntersectionCandidates,
        bool SnapToDxf,
        bool SnapToIfc)
    {
        public bool IsValid =>
            SourceVersions is not null &&
            ReferencePlane is not null &&
            IsFinite(CameraPosition) &&
            IsFinite(LookDirection) &&
            IsFinite(UpDirection) &&
            IsFinite(ReferencePlane.Origin) &&
            IsFinite(ReferencePlane.Normal) &&
            HasUsableLength(ReferencePlane.Normal) &&
            (CameraType == OsnapCameraType.Perspective ||
             CameraType == OsnapCameraType.Orthographic) &&
            IsPositiveFinite(ViewportWidth) &&
            IsPositiveFinite(ViewportHeight) &&
            IsPositiveFinite(FieldOfViewOrWidth) &&
            (CameraType != OsnapCameraType.Perspective ||
             FieldOfViewOrWidth < 180.0) &&
            IsPositiveFinite(DpiScaleX) &&
            IsPositiveFinite(DpiScaleY) &&
            IsPositiveFinite(SnapRadiusPx) &&
            HasUsableLength(LookDirection) &&
            HasUsableLength(UpDirection) &&
            double.IsFinite(ScreenOffsetX) &&
            double.IsFinite(ScreenOffsetY) &&
            double.IsFinite(CursorScreenPoint.X) &&
            double.IsFinite(CursorScreenPoint.Y);

        private static bool IsPositiveFinite(double value) =>
            value > 0 && double.IsFinite(value);

        private static bool HasUsableLength(Vector3D vector) =>
            vector.LengthSquared > 1e-12 &&
            double.IsFinite(vector.LengthSquared);

        private static bool IsFinite(Point3D point) =>
            double.IsFinite(point.X) &&
            double.IsFinite(point.Y) &&
            double.IsFinite(point.Z);

        private static bool IsFinite(Vector3D vector) =>
            double.IsFinite(vector.X) &&
            double.IsFinite(vector.Y) &&
            double.IsFinite(vector.Z);
    }
}
