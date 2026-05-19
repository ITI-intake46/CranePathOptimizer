using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F11_DXF.Views;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Services;
using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    /// <summary>
    /// Bridges the live scene data (zones, DXF, IFC elements) into the
    /// <see cref="IOsnapService"/> geometry feeds whenever the scene changes.
    ///
    /// This is the SINGLE wiring point — no other class needs to call
    /// SetZoneGeometry / SetDxfSnapPoints / SetIfcSnapPoints.
    ///
    /// Lifecycle: created as a Singleton in DI, attached in MainWindow.
    /// </summary>
    public sealed class OsnapGeometryBridge : IDisposable
    {
        private readonly IOsnapService _snap;
        private readonly ISceneService _scene;
        private DxfUploader? _currentUploader;
        private HelixViewport3D? _viewport;
        private int _ifcExtractGeneration;
        private bool _disposed;

        public OsnapGeometryBridge(IOsnapService snap, ISceneService scene)
        {
            _snap  = snap  ?? throw new ArgumentNullException(nameof(snap));
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));

            // Subscribe to all zone mutation events
            _scene.ZoneAdded    += OnZoneChanged;
            _scene.ZoneRemoved  += OnZoneChanged;
            _scene.ZoneUpdated  += OnZoneChanged;
            _scene.ZonesCleared += OnZonesCleared;

            // Subscribe to IFC element changes
            _scene.ElementsChanged += OnElementsChanged;

            // Seed the initial state (scene may already have data at startup)
            RefreshZones();
        }

        /// <summary>
        /// Attach the live viewport so IFC snap points can be extracted from rendered meshes.
        /// Call once from MainWindow after the Helix viewport is initialized.
        /// </summary>
        public void AttachViewport(HelixViewport3D viewport)
        {
            _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        }

        // ─── Zone geometry feed ────────────────────────────────────────────────

        private void OnZoneChanged(GreenAreaZone _) => RefreshZones();
        private void OnZonesCleared()              => RefreshZones();

        private void RefreshZones()
        {
            var zones = _scene.GetZones();
            var polygons = zones
                .Where(z => z.Vertices.Count >= 2)
                .Select(z => (
                    Vertices: (IReadOnlyList<Point3D>)z.Vertices
                        .Select(v => new Point3D(v.X, v.Y, 0))
                        .ToList(),
                    IsClosed: true))
                .ToList();

            _snap.SetZoneGeometry(polygons);
        }

        // ─── IFC element feed ──────────────────────────────────────────────────

        private void OnElementsChanged(List<PrecastElement> elements)
        {
            _snap.SetIfcSnapGeometry(BuildElementSnapGeometry(elements));

            // Upgrade IFC snaps to rendered-space bounds (includes origin shifts).
            // Rendering happens in response to the same ElementsChanged event, so we defer
            // extraction to allow visuals to be created first. Keep this intentionally light:
            // dense mesh-edge extraction makes large IFC models too slow during snap prep.
            var vp = _viewport;
            if (vp == null) return;

            int gen = ++_ifcExtractGeneration;
            vp.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (gen != _ifcExtractGeneration) return; // a newer scene update arrived
                try
                {
                    var rendered = IfcMeshSnapExtractor.ExtractElementBoundsFromViewport(vp);
                    if (rendered.Count > 0)
                        _snap.SetIfcSnapGeometry(rendered);
                }
                catch
                {
                    // Best-effort: keep the coarse snap points if extraction fails.
                }
            }));
        }

        private static List<IfcSnapGeometry> BuildElementSnapGeometry(IEnumerable<PrecastElement> elements)
        {
            var result = new List<IfcSnapGeometry>();
            foreach (var element in elements)
            {
                if (string.IsNullOrWhiteSpace(element.ElementID) ||
                    element.ElementCOG == null ||
                    !element.BBoxLength.HasValue ||
                    !element.BBoxWidth.HasValue)
                {
                    continue;
                }

                double hL = element.BBoxLength.Value / 2.0;
                double hW = element.BBoxWidth.Value / 2.0;
                double hH = (element.BBoxHeight ?? 0) / 2.0;
                double z = element.ElementCOG.Z;
                var min = new Point3D(element.ElementCOG.X - hL, element.ElementCOG.Y - hW, z - hH);
                var max = new Point3D(element.ElementCOG.X + hL, element.ElementCOG.Y + hW, z + hH);
                var vertices = BuildBoxVertices(min, max);
                var segments = BuildBoxSegments(vertices);

                result.Add(new IfcSnapGeometry(element.ElementID, vertices, segments));
            }

            return result;
        }

        private static List<Point3D> BuildBoxVertices(Point3D min, Point3D max)
            => new()
            {
                new Point3D(min.X, min.Y, min.Z),
                new Point3D(max.X, min.Y, min.Z),
                new Point3D(max.X, max.Y, min.Z),
                new Point3D(min.X, max.Y, min.Z),
                new Point3D(min.X, min.Y, max.Z),
                new Point3D(max.X, min.Y, max.Z),
                new Point3D(max.X, max.Y, max.Z),
                new Point3D(min.X, max.Y, max.Z),
            };

        private static List<(Point3D A, Point3D B)> BuildBoxSegments(IReadOnlyList<Point3D> vertices)
            => new()
            {
                (vertices[0], vertices[1]),
                (vertices[1], vertices[2]),
                (vertices[2], vertices[3]),
                (vertices[3], vertices[0]),
                (vertices[4], vertices[5]),
                (vertices[5], vertices[6]),
                (vertices[6], vertices[7]),
                (vertices[7], vertices[4]),
                (vertices[0], vertices[4]),
                (vertices[1], vertices[5]),
                (vertices[2], vertices[6]),
                (vertices[3], vertices[7]),
            };

        // ─── DXF feed (called externally when DXF loads) ───────────────────────

        /// <summary>
        /// Call this once after a DXF file has finished loading so the snap engine
        /// receives the endpoint + segment data from the uploader.
        /// </summary>
        public void RefreshDxf(DxfUploader uploader)
        {
            // Unsubscribe from previous uploader if any
            if (_currentUploader != null)
                _currentUploader.SnapDataChanged -= OnDxfSnapDataChanged;

            _currentUploader = uploader;

            if (!uploader.IsLoaded)
            {
                _snap.SetDxfSnapPoints(
                    Enumerable.Empty<Point3D>(),
                    Enumerable.Empty<(Point3D, Point3D)>());
                return;
            }

            // Subscribe so we auto-refresh whenever alignment changes
            uploader.SnapDataChanged += OnDxfSnapDataChanged;

            // Push current data
            PushDxfSnapData(uploader);
        }

        private void OnDxfSnapDataChanged()
        {
            if (_currentUploader != null)
                PushDxfSnapData(_currentUploader);
        }

        private void PushDxfSnapData(DxfUploader uploader)
        {
            _snap.SetDxfSnapPoints(
                uploader.SnapPoints,
                uploader.SnapSegments,
                uploader.SnapCenters,
                uploader.SnapQuadrants,
                uploader.SnapMidpointSegments);
        }

        /// <summary>Clears the DXF snap data (called when DXF is unloaded).</summary>
        public void ClearDxf()
        {
            _snap.SetDxfSnapPoints(
                Enumerable.Empty<Point3D>(),
                Enumerable.Empty<(Point3D, Point3D)>());
        }

        // ─── Dispose ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _scene.ZoneAdded    -= OnZoneChanged;
            _scene.ZoneRemoved  -= OnZoneChanged;
            _scene.ZoneUpdated  -= OnZoneChanged;
            _scene.ZonesCleared -= OnZonesCleared;
            _scene.ElementsChanged -= OnElementsChanged;

            if (_currentUploader != null)
                _currentUploader.SnapDataChanged -= OnDxfSnapDataChanged;
        }
    }
}
