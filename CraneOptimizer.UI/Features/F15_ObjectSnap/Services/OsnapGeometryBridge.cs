using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F11_DXF.Views;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    /// <summary>
    /// Captures UI-neutral source references and prepares immutable Object Snap
    /// geometry on workers. No viewport or DispatcherObject is retained.
    /// </summary>
    public sealed class OsnapGeometryBridge : IDisposable
    {
        private readonly IOsnapService _snap;
        private readonly ISceneService _scene;
        private readonly IOriginService _origin;
        private readonly object _refreshGate = new();
        private readonly Dictionary<DxfUploader, Action> _dxfUploaders = new();

        private IReadOnlyList<PrecastElement> _latestElements =
            Array.Empty<PrecastElement>();
        private CancellationTokenSource? _ifcRefreshCancellation;
        private CancellationTokenSource? _dxfRefreshCancellation;
        private CancellationTokenSource? _zoneRefreshCancellation;
        private int _ifcRefreshGeneration;
        private int _dxfRefreshGeneration;
        private int _zoneRefreshGeneration;
        private volatile bool _disposed;

        public OsnapGeometryBridge(
            IOsnapService snap,
            ISceneService scene,
            IOriginService origin)
        {
            _snap = snap ?? throw new ArgumentNullException(nameof(snap));
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _origin = origin ?? throw new ArgumentNullException(nameof(origin));

            _scene.ZonesChanged += RefreshZones;
            _scene.ElementsChanged += OnElementsChanged;
            _origin.OriginChanged += OnOriginChanged;
            _snap.MasterEnabledChanged += OnMasterEnabledChanged;
            ElementVisualBuilder.SpatialHitTestSnapshotChanged +=
                OnRenderedIfcBoundsChanged;

            _latestElements = _scene.GetElementSnapshot();
            if (_snap.Settings.MasterEnabled)
            {
                RefreshZones();
                RefreshIfcSnapGeometry();
            }
        }

        public void RefreshDxf(DxfUploader uploader)
        {
            ArgumentNullException.ThrowIfNull(uploader);
            lock (_refreshGate)
            {
                if (_disposed)
                    return;

                if (!_dxfUploaders.ContainsKey(uploader))
                {
                    Action handler = PushCombinedDxfSnapData;
                    _dxfUploaders.Add(uploader, handler);
                    uploader.SnapDataChanged += handler;
                }
            }

            PushCombinedDxfSnapData();
        }

        public void RemoveDxf(DxfUploader uploader)
        {
            lock (_refreshGate)
            {
                if (_dxfUploaders.Remove(
                        uploader,
                        out Action? handler))
                {
                    uploader.SnapDataChanged -= handler;
                }
            }

            PushCombinedDxfSnapData();
        }

        public void ClearDxf() => PushCombinedDxfSnapData();

        public void RefreshIfcSnapGeometry() =>
            RefreshIfcSnapGeometryCore(
                Volatile.Read(ref _latestElements),
                useRenderedBounds: true);

        public void Dispose()
        {
            lock (_refreshGate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                foreach ((DxfUploader uploader, Action handler) in
                         _dxfUploaders)
                {
                    uploader.SnapDataChanged -= handler;
                }

                _dxfUploaders.Clear();
            }

            _scene.ZonesChanged -= RefreshZones;
            _scene.ElementsChanged -= OnElementsChanged;
            _origin.OriginChanged -= OnOriginChanged;
            _snap.MasterEnabledChanged -= OnMasterEnabledChanged;
            ElementVisualBuilder.SpatialHitTestSnapshotChanged -=
                OnRenderedIfcBoundsChanged;
            CancelAllRefreshes();
        }

        private async void RefreshZones()
        {
            if (_disposed || !_snap.Settings.MasterEnabled)
                return;

            CancellationTokenSource? cancellation = BeginRefresh(
                ref _zoneRefreshCancellation,
                ref _zoneRefreshGeneration,
                out int generation);
            if (cancellation == null)
                return;

            CancellationToken cancellationToken = cancellation.Token;
            _snap.BeginGeometryPreparation();
            try
            {
                IReadOnlyList<GreenAreaZone> zones =
                    _scene.GetZoneSnapshot();
                OsnapZoneGeometry[] snapshot = await Task.Run(
                        () =>
                        {
                            ZoneSnapSource[] sources =
                                CaptureZoneSources(
                                    zones,
                                    cancellationToken);
                            return BuildZoneGeometry(
                                sources,
                                cancellationToken);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!IsRefreshCurrent(
                        ref _zoneRefreshCancellation,
                        ref _zoneRefreshGeneration,
                        cancellation,
                        generation))
                {
                    return;
                }

                await _snap.SetCompletedZoneGeometryAsync(
                        snapshot,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OSnap] Completed-zone index refresh failed: {ex.Message}");
            }
            finally
            {
                CompleteRefresh(
                    ref _zoneRefreshCancellation,
                    cancellation);
                _snap.EndGeometryPreparation();
            }
        }

        private void OnElementsChanged(List<PrecastElement> elements)
        {
            Volatile.Write(
                ref _latestElements,
                (IReadOnlyList<PrecastElement>)elements);
            RefreshIfcSnapGeometryCore(
                elements,
                useRenderedBounds: false);
        }

        private void OnOriginChanged() =>
            RefreshIfcSnapGeometryCore(
                Volatile.Read(ref _latestElements),
                useRenderedBounds: false);

        private void OnRenderedIfcBoundsChanged() =>
            RefreshIfcSnapGeometryCore(
                Volatile.Read(ref _latestElements),
                useRenderedBounds: true);

        private async void RefreshIfcSnapGeometryCore(
            IReadOnlyList<PrecastElement> elements,
            bool useRenderedBounds)
        {
            if (_disposed || !_snap.Settings.MasterEnabled)
                return;

            CancellationTokenSource? cancellation = BeginRefresh(
                ref _ifcRefreshCancellation,
                ref _ifcRefreshGeneration,
                out int generation);
            if (cancellation == null)
                return;

            CancellationToken cancellationToken = cancellation.Token;
            _snap.BeginGeometryPreparation();
            DomainPoint3D? originPoint = _origin.OriginPoint;
            double originX = originPoint?.X ?? 0;
            double originY = originPoint?.Y ?? 0;
            double originZ = originPoint?.Z ?? 0;
            try
            {
                IfcSnapGeometry[] snapshot = await Task.Run(
                        () =>
                        {
                            IfcElementSnapSource[] sources =
                                CaptureElementSources(
                                    elements,
                                    cancellationToken);
                            ElementVisualBuilder
                                .SpatialSnapBoundsSnapshot[] renderedBounds =
                                useRenderedBounds
                                    ? ElementVisualBuilder
                                        .GetSpatialSnapBoundsSnapshot(
                                            cancellationToken)
                                    : Array.Empty<ElementVisualBuilder
                                        .SpatialSnapBoundsSnapshot>();
                            return BuildElementSnapGeometry(
                                sources,
                                renderedBounds,
                                originX,
                                originY,
                                originZ,
                                cancellationToken);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!IsRefreshCurrent(
                        ref _ifcRefreshCancellation,
                        ref _ifcRefreshGeneration,
                        cancellation,
                        generation))
                {
                    return;
                }

                await _snap.SetIfcSnapGeometryAsync(
                        snapshot,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OSnap] IFC index refresh failed: {ex.Message}");
            }
            finally
            {
                CompleteRefresh(
                    ref _ifcRefreshCancellation,
                    cancellation);
                _snap.EndGeometryPreparation();
            }
        }

        private async void PushCombinedDxfSnapData()
        {
            if (_disposed || !_snap.Settings.MasterEnabled)
                return;

            CancellationTokenSource? cancellation = BeginRefresh(
                ref _dxfRefreshCancellation,
                ref _dxfRefreshGeneration,
                out int generation);
            if (cancellation == null)
                return;

            CancellationToken cancellationToken = cancellation.Token;
            _snap.BeginGeometryPreparation();
            try
            {
                DxfSnapSource[] sources = CaptureDxfSources();
                PreparedDxfSnapData prepared = await Task.Run(
                        () => CombineDxfSnapData(
                            sources,
                            cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!IsRefreshCurrent(
                        ref _dxfRefreshCancellation,
                        ref _dxfRefreshGeneration,
                        cancellation,
                        generation))
                {
                    return;
                }

                await _snap.SetDxfSnapPointsAsync(
                        prepared.Endpoints,
                        prepared.Segments,
                        prepared.Centers,
                        prepared.Quadrants,
                        prepared.MidpointSegments,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OSnap] DXF index refresh failed: {ex.Message}");
            }
            finally
            {
                CompleteRefresh(
                    ref _dxfRefreshCancellation,
                    cancellation);
                _snap.EndGeometryPreparation();
            }
        }

        private void OnMasterEnabledChanged(bool enabled)
        {
            if (!enabled)
            {
                CancelAllRefreshes();
                return;
            }

            RefreshZones();
            RefreshIfcSnapGeometryCore(
                Volatile.Read(ref _latestElements),
                useRenderedBounds: true);
            PushCombinedDxfSnapData();
        }

        private DxfSnapSource[] CaptureDxfSources()
        {
            DxfUploader[] uploaders;
            lock (_refreshGate)
            {
                uploaders =
                    new DxfUploader[_dxfUploaders.Count];
                _dxfUploaders.Keys.CopyTo(uploaders, 0);
            }

            var result = new List<DxfSnapSource>(uploaders.Length);
            foreach (DxfUploader uploader in uploaders)
            {
                if (!uploader.IsLoaded)
                    continue;

                DxfUploader.DxfSnapDataSnapshot snapshot =
                    uploader.GetSnapDataSnapshot();
                result.Add(new DxfSnapSource(
                    snapshot.Endpoints,
                    snapshot.Segments,
                    snapshot.Centers,
                    snapshot.Quadrants,
                    snapshot.MidpointSegments));
            }

            return result.ToArray();
        }

        private CancellationTokenSource? BeginRefresh(
            ref CancellationTokenSource? field,
            ref int generationField,
            out int generation)
        {
            var cancellation = new CancellationTokenSource();
            CancellationTokenSource? superseded;
            lock (_refreshGate)
            {
                if (_disposed)
                {
                    generation = generationField;
                    cancellation.Dispose();
                    return null;
                }

                superseded = field;
                field = cancellation;
                generation = unchecked(++generationField);
                CancelWithoutDisposing(superseded);
            }

            return cancellation;
        }

        private bool IsRefreshCurrent(
            ref CancellationTokenSource? field,
            ref int generationField,
            CancellationTokenSource cancellation,
            int generation)
        {
            lock (_refreshGate)
            {
                return !_disposed &&
                       ReferenceEquals(field, cancellation) &&
                       generationField == generation &&
                       !cancellation.IsCancellationRequested;
            }
        }

        private void CompleteRefresh(
            ref CancellationTokenSource? field,
            CancellationTokenSource cancellation)
        {
            lock (_refreshGate)
            {
                if (ReferenceEquals(field, cancellation))
                    field = null;
            }

            cancellation.Dispose();
        }

        private void CancelAllRefreshes()
        {
            CancellationTokenSource? ifc;
            CancellationTokenSource? dxf;
            CancellationTokenSource? zones;
            lock (_refreshGate)
            {
                ifc = TakeCancellation(
                    ref _ifcRefreshCancellation,
                    ref _ifcRefreshGeneration);
                dxf = TakeCancellation(
                    ref _dxfRefreshCancellation,
                    ref _dxfRefreshGeneration);
                zones = TakeCancellation(
                    ref _zoneRefreshCancellation,
                    ref _zoneRefreshGeneration);
                CancelWithoutDisposing(ifc);
                CancelWithoutDisposing(dxf);
                CancelWithoutDisposing(zones);
            }
        }

        private static CancellationTokenSource? TakeCancellation(
            ref CancellationTokenSource? field,
            ref int generationField)
        {
            CancellationTokenSource? cancellation = field;
            field = null;
            generationField = unchecked(generationField + 1);
            return cancellation;
        }

        private static void CancelWithoutDisposing(
            CancellationTokenSource? cancellation)
        {
            if (cancellation == null)
                return;

            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The operation completed between replacement and cancellation.
            }
            catch (AggregateException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OSnap] Refresh cancellation callback failed: {ex.Message}");
            }
        }

        private static ZoneSnapSource[] CaptureZoneSources(
            IReadOnlyList<GreenAreaZone> zones,
            CancellationToken cancellationToken)
        {
            var result = new List<ZoneSnapSource>(zones.Count);
            for (int zoneIndex = 0; zoneIndex < zones.Count; zoneIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<DomainPoint3D> vertices =
                    zones[zoneIndex].Vertices;
                if (vertices.Count < 2)
                    continue;

                var points = new ZoneVertexSource[vertices.Count];
                for (int vertexIndex = 0;
                     vertexIndex < vertices.Count;
                     vertexIndex++)
                {
                    if ((vertexIndex & 255) == 0)
                        cancellationToken.ThrowIfCancellationRequested();

                    DomainPoint3D vertex = vertices[vertexIndex];
                    points[vertexIndex] = new ZoneVertexSource(
                        vertex.X,
                        vertex.Y,
                        vertex.Z);
                }

                result.Add(new ZoneSnapSource(points));
            }

            return result.ToArray();
        }

        private static OsnapZoneGeometry[] BuildZoneGeometry(
            IReadOnlyList<ZoneSnapSource> zones,
            CancellationToken cancellationToken)
        {
            var result = new OsnapZoneGeometry[zones.Count];
            for (int zoneIndex = 0; zoneIndex < zones.Count; zoneIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ZoneVertexSource[] source = zones[zoneIndex].Vertices;
                var points = new Point3D[source.Length];
                for (int vertexIndex = 0;
                     vertexIndex < source.Length;
                     vertexIndex++)
                {
                    if ((vertexIndex & 255) == 0)
                        cancellationToken.ThrowIfCancellationRequested();

                    ZoneVertexSource vertex = source[vertexIndex];
                    points[vertexIndex] =
                        new Point3D(vertex.X, vertex.Y, vertex.Z);
                }

                result[zoneIndex] =
                    new OsnapZoneGeometry(points, IsClosed: true);
            }

            return result;
        }

        private static IfcElementSnapSource[] CaptureElementSources(
            IReadOnlyList<PrecastElement> elements,
            CancellationToken cancellationToken)
        {
            var result =
                new List<IfcElementSnapSource>(elements.Count);
            for (int elementIndex = 0;
                 elementIndex < elements.Count;
                 elementIndex++)
            {
                if ((elementIndex & 127) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                PrecastElement element = elements[elementIndex];
                DomainPoint3D? center = element.ElementCOG;
                if (string.IsNullOrWhiteSpace(element.ElementID) ||
                    center == null ||
                    !element.BBoxLength.HasValue ||
                    !element.BBoxWidth.HasValue)
                {
                    continue;
                }

                result.Add(new IfcElementSnapSource(
                    element.ElementID,
                    center.X,
                    center.Y,
                    center.Z,
                    element.BBoxLength.Value,
                    element.BBoxWidth.Value,
                    element.BBoxHeight ?? 0));
            }

            return result.ToArray();
        }

        private static IfcSnapGeometry[] BuildElementSnapGeometry(
            IReadOnlyList<IfcElementSnapSource> elements,
            IReadOnlyList<ElementVisualBuilder
                .SpatialSnapBoundsSnapshot> renderedBounds,
            double originX,
            double originY,
            double originZ,
            CancellationToken cancellationToken)
        {
            var renderedByElementId =
                new Dictionary<string, ElementVisualBuilder
                    .SpatialSnapBoundsSnapshot>(
                    renderedBounds.Count,
                    StringComparer.Ordinal);
            for (int renderedIndex = 0;
                 renderedIndex < renderedBounds.Count;
                 renderedIndex++)
            {
                if ((renderedIndex & 255) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                ElementVisualBuilder.SpatialSnapBoundsSnapshot bounds =
                    renderedBounds[renderedIndex];
                if (bounds.IsVisible &&
                    !string.IsNullOrWhiteSpace(bounds.ElementId) &&
                    IsValidBounds(bounds.Min, bounds.Max))
                {
                    renderedByElementId[bounds.ElementId] = bounds;
                }
            }

            var result = new List<IfcSnapGeometry>(elements.Count);
            for (int elementIndex = 0;
                 elementIndex < elements.Count;
                 elementIndex++)
            {
                if ((elementIndex & 127) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                IfcElementSnapSource element = elements[elementIndex];
                Point3D min;
                Point3D max;
                if (renderedByElementId.TryGetValue(
                        element.ElementId,
                        out ElementVisualBuilder
                            .SpatialSnapBoundsSnapshot rendered))
                {
                    // These are already in rendered/origin-adjusted world space.
                    min = rendered.Min;
                    max = rendered.Max;
                }
                else
                {
                    double halfLength = Math.Abs(element.Length) / 2.0;
                    double halfWidth = Math.Abs(element.Width) / 2.0;
                    double halfHeight = Math.Abs(element.Height) / 2.0;
                    double centerX = element.CenterX - originX;
                    double centerY = element.CenterY - originY;
                    double centerZ = element.CenterZ - originZ;
                    min = new Point3D(
                        centerX - halfLength,
                        centerY - halfWidth,
                        centerZ - halfHeight);
                    max = new Point3D(
                        centerX + halfLength,
                        centerY + halfWidth,
                        centerZ + halfHeight);
                }

                if (!IsValidBounds(min, max))
                    continue;

                Point3D[] vertices = BuildBoxVertices(min, max);
                (Point3D A, Point3D B)[] segments =
                    BuildBoxSegments(vertices);
                result.Add(new IfcSnapGeometry(
                    element.ElementId,
                    vertices,
                    segments));
            }

            return result.ToArray();
        }

        private static Point3D[] BuildBoxVertices(
            Point3D min,
            Point3D max) =>
            new[]
            {
                new Point3D(min.X, min.Y, min.Z),
                new Point3D(max.X, min.Y, min.Z),
                new Point3D(max.X, max.Y, min.Z),
                new Point3D(min.X, max.Y, min.Z),
                new Point3D(min.X, min.Y, max.Z),
                new Point3D(max.X, min.Y, max.Z),
                new Point3D(max.X, max.Y, max.Z),
                new Point3D(min.X, max.Y, max.Z)
            };

        private static (Point3D A, Point3D B)[] BuildBoxSegments(
            IReadOnlyList<Point3D> vertices) =>
            new[]
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
                (vertices[3], vertices[7])
            };

        private static PreparedDxfSnapData CombineDxfSnapData(
            IReadOnlyList<DxfSnapSource> sources,
            CancellationToken cancellationToken)
        {
            var endpoints = new List<Point3D>();
            var segments = new List<(Point3D A, Point3D B)>();
            var centers = new List<Point3D>();
            var quadrants = new List<Point3D>();
            var midpoints = new List<(Point3D A, Point3D B)>();
            for (int sourceIndex = 0;
                 sourceIndex < sources.Count;
                 sourceIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DxfSnapSource source = sources[sourceIndex];
                AddFinitePoints(
                    endpoints,
                    source.Endpoints,
                    cancellationToken);
                AddFiniteSegments(
                    segments,
                    source.Segments,
                    cancellationToken);
                AddFinitePoints(
                    centers,
                    source.Centers,
                    cancellationToken);
                AddFinitePoints(
                    quadrants,
                    source.Quadrants,
                    cancellationToken);
                AddFiniteSegments(
                    midpoints,
                    source.MidpointSegments,
                    cancellationToken);
            }

            return new PreparedDxfSnapData(
                endpoints.ToArray(),
                segments.ToArray(),
                centers.ToArray(),
                quadrants.ToArray(),
                midpoints.ToArray());
        }

        private static void AddFinitePoints(
            List<Point3D> sink,
            ReadOnlyMemory<Point3D> source,
            CancellationToken cancellationToken)
        {
            ReadOnlySpan<Point3D> points = source.Span;
            for (int index = 0; index < points.Length; index++)
            {
                if ((index & 1023) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                Point3D point = points[index];
                if (IsFinite(point))
                    sink.Add(point);
            }
        }

        private static void AddFiniteSegments(
            List<(Point3D A, Point3D B)> sink,
            ReadOnlyMemory<(Point3D A, Point3D B)> source,
            CancellationToken cancellationToken)
        {
            ReadOnlySpan<(Point3D A, Point3D B)> segments =
                source.Span;
            for (int index = 0; index < segments.Length; index++)
            {
                if ((index & 1023) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                (Point3D A, Point3D B) segment = segments[index];
                if (IsFinite(segment.A) && IsFinite(segment.B))
                    sink.Add(segment);
            }
        }

        private static bool IsFinite(Point3D point) =>
            double.IsFinite(point.X) &&
            double.IsFinite(point.Y) &&
            double.IsFinite(point.Z);

        private static bool IsValidBounds(Point3D min, Point3D max) =>
            IsFinite(min) &&
            IsFinite(max) &&
            min.X <= max.X &&
            min.Y <= max.Y &&
            min.Z <= max.Z;

        private readonly record struct IfcElementSnapSource(
            string ElementId,
            double CenterX,
            double CenterY,
            double CenterZ,
            double Length,
            double Width,
            double Height);

        private readonly record struct ZoneVertexSource(
            double X,
            double Y,
            double Z);

        private sealed record ZoneSnapSource(
            ZoneVertexSource[] Vertices);

        private sealed record DxfSnapSource(
            ReadOnlyMemory<Point3D> Endpoints,
            ReadOnlyMemory<(Point3D A, Point3D B)> Segments,
            ReadOnlyMemory<Point3D> Centers,
            ReadOnlyMemory<Point3D> Quadrants,
            ReadOnlyMemory<(Point3D A, Point3D B)> MidpointSegments);

        private sealed record PreparedDxfSnapData(
            Point3D[] Endpoints,
            (Point3D A, Point3D B)[] Segments,
            Point3D[] Centers,
            Point3D[] Quadrants,
            (Point3D A, Point3D B)[] MidpointSegments);
    }
}
