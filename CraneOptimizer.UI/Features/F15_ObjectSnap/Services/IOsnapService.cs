using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    public interface IOsnapService
    {
        OsnapSettings Settings { get; }
        event Action<bool>? MasterEnabledChanged;
        event Action<long>? GeometryVersionChanged;
        event Action<long>? SettingsVersionChanged;
        event Action<bool>? PreparingChanged;

        OsnapGeometryVersionState GeometryVersions { get; }
        long SettingsVersion { get; }
        OsnapModeMask EnabledModes { get; }
        bool IsPreparing { get; }
        double SceneScale { get; }

        int SnapCacheBuiltCount { get; }
        int SnapPointCount { get; }
        double SnapBuildDurationMs { get; }
        double SnapMemoryDeltaMB { get; }

        void SetMasterEnabled(bool enabled);
        void SetSnapEnabled(OsnapType type, bool enabled);
        void SetSourceEnabled(OsnapSource source, bool enabled);
        void SetSnapRadius(double radiusPixels);
        void SetGridSpacing(double spacing);

        Task SetCompletedZoneGeometryAsync(
            IReadOnlyList<OsnapZoneGeometry> polygons,
            CancellationToken cancellationToken = default);

        Task SetDxfSnapPointsAsync(
            IReadOnlyList<Point3D> endpoints,
            IReadOnlyList<(Point3D A, Point3D B)> segments,
            IReadOnlyList<Point3D> centers,
            IReadOnlyList<Point3D> quadrants,
            IReadOnlyList<(Point3D A, Point3D B)> midpointSegments,
            CancellationToken cancellationToken = default);

        Task SetIfcSnapGeometryAsync(
            IReadOnlyList<IfcSnapGeometry> geometry,
            CancellationToken cancellationToken = default);

        Task SetTransientZoneGeometryAsync(
            long drawingSessionVersion,
            IReadOnlyList<Point3D> vertices,
            CancellationToken cancellationToken = default);

        void ClearTransientZoneGeometry(long drawingSessionVersion);
        void CancelPendingQueries();
        void BeginGeometryPreparation() { }
        void EndGeometryPreparation() { }

        Task<OsnapResult?> SnapAsync(
            OsnapQuery query,
            CancellationToken cancellationToken);

        OsnapDiagnosticsSnapshot GetDiagnostics();
    }
}
