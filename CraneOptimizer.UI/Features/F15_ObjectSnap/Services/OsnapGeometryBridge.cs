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
        private readonly Dictionary<DxfUploader, Action> _dxfUploaders = new();
        private int _dxfCombineGeneration;
        private int _zoneRefreshGeneration;
        private HelixViewport3D? _viewport;
        private int _ifcExtractGeneration;
        private bool _disposed;

        public OsnapGeometryBridge(IOsnapService snap, ISceneService scene)
        {
            _snap  = snap  ?? throw new ArgumentNullException(nameof(snap));
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));

            _scene.ZonesChanged += RefreshZones;

            // Subscribe to IFC element changes
            _scene.ElementsChanged += OnElementsChanged;

            // Subscribe to Object Snap master enabled changes
            _snap.MasterEnabledChanged += OnMasterEnabledChanged;

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

        private async void RefreshZones()
        {
            int generation = Interlocked.Increment(ref _zoneRefreshGeneration);
            IReadOnlyList<GreenAreaZone> zones = _scene.GetZoneSnapshot();
            try
            {
                var polygons = await Task.Run(() => zones
                    .Where(zone => zone.Vertices.Count >= 2)
                    .Select(zone => (
                        Vertices: (IReadOnlyList<Point3D>)zone.Vertices
                            .Select(vertex => new Point3D(vertex.X, vertex.Y, vertex.Z))
                            .ToList(),
                        IsClosed: true))
                    .ToList());

                if (generation == _zoneRefreshGeneration && !_disposed)
                    _snap.SetZoneGeometry(polygons);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OSnap] Zone geometry refresh failed: {ex.Message}");
            }
        }

        // ─── IFC element feed ──────────────────────────────────────────────────

        private void OnElementsChanged(List<PrecastElement> elements)
        {
            if (!_snap.Settings.MasterEnabled)
            {
                System.Diagnostics.Debug.WriteLine("[OSnap] ObjectSnap disabled; snap geometry extraction skipped on load.");
                _snap.SetIfcSnapGeometry(Enumerable.Empty<IfcSnapGeometry>());
                return;
            }

            RefreshIfcSnapGeometry();
        }

        public void RefreshIfcSnapGeometry()
        {
            if (!_snap.Settings.MasterEnabled)
            {
                _snap.SetIfcSnapGeometry(Enumerable.Empty<IfcSnapGeometry>());
                return;
            }

            var elements = _scene.GetElements();
            _snap.SetIfcSnapGeometry(BuildElementSnapGeometry(elements));

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

        private void OnMasterEnabledChanged(bool enabled)
        {
            if (enabled)
            {
                System.Diagnostics.Debug.WriteLine("[OSnap] ObjectSnap enabled manually; building snap points on demand.");
                RefreshIfcSnapGeometry();
                PushCombinedDxfSnapData();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[OSnap] ObjectSnap disabled; clearing snap data.");
                _snap.SetIfcSnapGeometry(Enumerable.Empty<IfcSnapGeometry>());
                ClearDxf();
            }
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
            if (!_dxfUploaders.ContainsKey(uploader))
            {
                Action handler = PushCombinedDxfSnapData;
                _dxfUploaders.Add(uploader, handler);
                uploader.SnapDataChanged += handler;
            }

            PushCombinedDxfSnapData();
        }

        public void RemoveDxf(DxfUploader uploader)
        {
            if (_dxfUploaders.Remove(uploader, out var handler))
                uploader.SnapDataChanged -= handler;

            PushCombinedDxfSnapData();
        }

        private async void PushCombinedDxfSnapData()
        {
            int generation = Interlocked.Increment(ref _dxfCombineGeneration);
            var loaded = _dxfUploaders.Keys
                .Where(uploader => uploader.IsLoaded)
                .Select(uploader => new DxfSnapSource(
                    uploader.SnapPoints,
                    uploader.SnapSegments,
                    uploader.SnapCenters,
                    uploader.SnapQuadrants,
                    uploader.SnapMidpointSegments))
                .ToArray();
            try
            {
                PreparedDxfSnapData prepared = await Task.Run(() => CombineDxfSnapData(loaded));
                if (generation != _dxfCombineGeneration)
                    return;
                _snap.SetPreparedDxfSnapPoints(
                    prepared.Endpoints,
                    prepared.Segments,
                    prepared.Centers,
                    prepared.Quadrants,
                    prepared.MidpointSegments);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OSnap] DXF snap combination failed: {ex.Message}");
            }
        }

        /// <summary>Clears the DXF snap data (called when DXF is unloaded).</summary>
        public void ClearDxf()
        {
            Interlocked.Increment(ref _dxfCombineGeneration);
            _snap.SetPreparedDxfSnapPoints(
                new List<Point3D>(),
                new List<(Point3D, Point3D)>(),
                new List<Point3D>(),
                new List<Point3D>(),
                new List<(Point3D, Point3D)>());
        }

        private static PreparedDxfSnapData CombineDxfSnapData(IReadOnlyList<DxfSnapSource> sources)
        {
            var endpoints = new List<Point3D>();
            var segments = new List<(Point3D A, Point3D B)>();
            var centers = new List<Point3D>();
            var quadrants = new List<Point3D>();
            var midpoints = new List<(Point3D A, Point3D B)>();
            foreach (DxfSnapSource source in sources)
            {
                endpoints.AddRange(source.Endpoints.Where(IsFinite));
                segments.AddRange(source.Segments.Where(segment => IsFinite(segment.A) && IsFinite(segment.B)));
                centers.AddRange(source.Centers.Where(IsFinite));
                quadrants.AddRange(source.Quadrants.Where(IsFinite));
                midpoints.AddRange(source.MidpointSegments.Where(segment => IsFinite(segment.A) && IsFinite(segment.B)));
            }

            return new PreparedDxfSnapData(endpoints, segments, centers, quadrants, midpoints);
        }

        private static bool IsFinite(Point3D point) =>
            double.IsFinite(point.X) &&
            double.IsFinite(point.Y) &&
            double.IsFinite(point.Z);

        private sealed record DxfSnapSource(
            IReadOnlyList<Point3D> Endpoints,
            IReadOnlyList<(Point3D A, Point3D B)> Segments,
            IReadOnlyList<Point3D> Centers,
            IReadOnlyList<Point3D> Quadrants,
            IReadOnlyList<(Point3D A, Point3D B)> MidpointSegments);

        private sealed record PreparedDxfSnapData(
            List<Point3D> Endpoints,
            List<(Point3D A, Point3D B)> Segments,
            List<Point3D> Centers,
            List<Point3D> Quadrants,
            List<(Point3D A, Point3D B)> MidpointSegments);

        // ─── Dispose ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Interlocked.Increment(ref _zoneRefreshGeneration);
            _scene.ZonesChanged -= RefreshZones;
            _scene.ElementsChanged -= OnElementsChanged;
            _snap.MasterEnabledChanged -= OnMasterEnabledChanged;

            foreach (var (uploader, handler) in _dxfUploaders)
                uploader.SnapDataChanged -= handler;
            _dxfUploaders.Clear();
        }
    }
}
