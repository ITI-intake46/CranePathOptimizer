using HelixToolkit.Wpf;
using DxfTextAlignment = netDxf.Entities.TextAlignment;
using netDxf;
using netDxf.Entities;
using netDxf.Header;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F7_Zones;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace CraneOptimizer.UI.Features.F11_DXF.Views
{
    public class DxfUploader
    {
        private readonly HelixViewport3D _viewport;
        private readonly Dispatcher _uiDispatcher;
        private readonly List<ModelVisual3D> _dxfVisuals = new();
        private readonly List<TextVisual3D> _textVisuals = new();
        private readonly HashSet<TextVisual3D> _zoneContrastTextVisuals = new();
        private readonly Dictionary<LinesVisual3D, Color> _lineVisualColors = new();
        private readonly List<DxfTextVisualData> _rawTextItems = new();
        private readonly List<Point3D> _rawSnapPoints = new();
        private readonly List<(Point3D A, Point3D B)> _rawSnapSegments = new();
        private readonly List<(Point3D A, Point3D B)> _rawSnapMidpointSegments = new();
        private readonly List<Point3D> _rawSnapCenters = new();
        private readonly List<Point3D> _rawSnapQuadrants = new();
        private Point3D[] _snapPointSource = Array.Empty<Point3D>();
        private (Point3D A, Point3D B)[] _snapSegmentSource = Array.Empty<(Point3D, Point3D)>();
        private (Point3D A, Point3D B)[] _snapMidpointSource = Array.Empty<(Point3D, Point3D)>();
        private Point3D[] _snapCenterSource = Array.Empty<Point3D>();
        private Point3D[] _snapQuadrantSource = Array.Empty<Point3D>();
        private DxfSnapDataSnapshot _snapDataSnapshot =
            DxfSnapDataSnapshot.Empty;
        private Rect _rawBounds = Rect.Empty;
        private int _snapRefreshGeneration;
        private CancellationTokenSource? _snapRefreshCancellation;
        private int _zoneContrastRefreshGeneration;
        private readonly SceneDxf? _selectionTarget;
        private bool _isSelectionHighlighted;
        private LinesVisual3D? _zoneContrastVisual;
        private IReadOnlyList<ZoneOverlayRegion> _zoneContrastRegions =
            Array.Empty<ZoneOverlayRegion>();

        // Large survey coordinates can destabilize WPF 3D projection. Rebase the
        // rendered overlay and snap data together when the final drawing is far
        // from the origin, including meter-scaled survey files.
        private const double FarCoordinateRebaseThreshold = 1_000_000.0;
        private Matrix3D _dxfRebase = Matrix3D.Identity;
        private Matrix3D _currentRenderTransform = Matrix3D.Identity;
        private DxfDocument? _loadedDocument;

        public IReadOnlyList<Point3D> SnapPoints { get; private set; } =
            Array.Empty<Point3D>();
        public IReadOnlyList<(Point3D A, Point3D B)> SnapSegments { get; private set; } =
            Array.Empty<(Point3D, Point3D)>();
        public IReadOnlyList<(Point3D A, Point3D B)> SnapMidpointSegments { get; private set; } =
            Array.Empty<(Point3D, Point3D)>();
        public IReadOnlyList<Point3D> SnapCenters { get; private set; } =
            Array.Empty<Point3D>();
        public IReadOnlyList<Point3D> SnapQuadrants { get; private set; } =
            Array.Empty<Point3D>();

        public DxfSnapDataSnapshot GetSnapDataSnapshot() =>
            Volatile.Read(ref _snapDataSnapshot);

        public bool IsLoaded { get; private set; }
        public string? LoadedFilePath { get; private set; }
        public DxfDocument? LoadedDocument => _loadedDocument;
        public bool IsVisible { get; private set; } = true;
        public string LastLoadDiagnostic { get; private set; } = "";

        private bool _useDxfColors;
        private Color _zoneContrastColor = Colors.White;
        public bool UseDxfColors
        {
            get => _useDxfColors;
            set
            {
                if (_useDxfColors == value) return;
                _useDxfColors = value;
                if (IsLoaded)
                    RefreshDxfColors();
            }
        }

        private bool _showText = true;
        public bool ShowText
        {
            get => _showText;
            set
            {
                if (_showText == value) return;
                _showText = value;
                ApplyTextVisibility();
            }
        }
        public int LastTextEntityCount { get; private set; }
        public int LastRenderedTextCount { get; private set; }

        public event Action? SnapDataChanged;

        private double _opacity = 0.7;
        public double Opacity
        {
            get => _opacity;
            set
            {
                double clamped = Math.Clamp(value, 0.1, 1.0);
                if (Math.Abs(_opacity - clamped) < 1e-9) return;
                _opacity = clamped;
                RefreshOpacity();
            }
        }

        public void SetZoneContrast(
            IReadOnlyList<GreenAreaZone> zones,
            Color contrastColor)
        {
            List<ZoneOverlayRegion> regions =
                ZoneOverlayGeometry.CreateRegions(zones);
            bool geometryChanged = !ZoneOverlayGeometry.AreEquivalent(
                _zoneContrastRegions,
                regions);
            bool colorChanged = _zoneContrastColor != contrastColor;
            _zoneContrastRegions = regions;
            _zoneContrastColor = contrastColor;

            if (!IsLoaded)
                return;

            if (geometryChanged)
                ScheduleZoneContrastRefresh();
            else if (colorChanged)
                RefreshOpacity();
        }

        public Matrix3D AlignmentTransform { get; set; } = Matrix3D.Identity;

        public DxfUploader(HelixViewport3D viewport, SceneDxf? selectionTarget = null)
        {
            _viewport = viewport;
            _uiDispatcher = viewport.Dispatcher;
            _selectionTarget = selectionTarget;
        }

        public async Task<bool> LoadDxfAsync(
            string filePath,
            bool showText,
            CancellationToken cancellationToken = default)
        {
            _uiDispatcher.VerifyAccess();
            _showText = showText;
            LastLoadDiagnostic = "";

            DxfLoadResult loadResult = await Task.Run(
                () => LoadDocument(filePath, cancellationToken),
                cancellationToken);
            if (loadResult.Document == null)
            {
                LastLoadDiagnostic = loadResult.Diagnostic;
                return false;
            }

            ClearVisualsAndSnapData();
            LoadedFilePath = filePath;
            _loadedDocument = loadResult.Document;
            PreparedDxfGeometry prepared;
            try
            {
                prepared = await Task.Run(
                    () => PrepareDxfGeometry(
                        loadResult.Document,
                        cancellationToken),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                await CommitPreparedGeometryAsync(
                    prepared,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ClearVisualsAndSnapData();
                _loadedDocument = null;
                LoadedFilePath = null;
                throw;
            }
            catch (Exception ex)
            {
                ClearVisualsAndSnapData();
                LastLoadDiagnostic = $"DXF geometry preparation failed: {ex.Message}";
                _loadedDocument = null;
                LoadedFilePath = null;
                return false;
            }

            IsLoaded = true;
            Log($"Render done. Points: {prepared.PointCount}");
            RaiseSnapDataChanged();
            return true;
        }

        private static DxfLoadResult LoadDocument(string filePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                DxfVersion version = DxfDocument.CheckDxfFileVersion(filePath);
                if (version < DxfVersion.AutoCad2000)
                {
                    return new DxfLoadResult(
                        null,
                        $"DXF version too old: {version}. Save the file as AutoCAD 2010 DXF and try again.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                DxfDocument? document = DxfDocument.Load(filePath);
                return document == null
                    ? new DxfLoadResult(null, "DxfDocument.Load returned null; the file may be corrupt.")
                    : new DxfLoadResult(document, "");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new DxfLoadResult(
                    null,
                    $"Cannot parse DXF: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private PreparedDxfGeometry PrepareDxfGeometry(
            DxfDocument dxf,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var linePointsByColor =
                new Dictionary<Color, List<Point3D>>();
            _dxfRebase = Matrix3D.Identity;
            _currentRenderTransform = Matrix3D.Identity;
            _rawTextItems.Clear();
            LastTextEntityCount = 0;
            LastRenderedTextCount = 0;
            int lineCount = 0, polyCount = 0, circleCount = 0, arcCount = 0, ellipseCount = 0, splineCount = 0, textCount = 0;

            try
            {
                foreach (var line in dxf.Entities.Lines)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var points = GetSegmentPoints(linePointsByColor, ResolveColor(line.Color, line.Layer));
                        var a = TransformPoint(line.StartPoint.X, line.StartPoint.Y);
                        var b = TransformPoint(line.EndPoint.X, line.EndPoint.Y);
                        AddSegment(points, a, b, includeMidpointSnap: true);
                        _rawSnapPoints.Add(a);
                        _rawSnapPoints.Add(b);
                        lineCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"Lines collection error: {ex.Message}");
            }

            try
            {
                foreach (var poly in dxf.Entities.Polylines2D)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var points = GetSegmentPoints(linePointsByColor, ResolveColor(poly.Color, poly.Layer));
                        var verts = new List<Point3D>();
                        foreach (var v in poly.Vertexes)
                            verts.Add(TransformPoint(v.Position.X, v.Position.Y));

                        if (verts.Count < 2) continue;

                        int count = poly.IsClosed ? verts.Count : verts.Count - 1;
                        for (int i = 0; i < count; i++)
                            AddSegment(points, verts[i], verts[(i + 1) % verts.Count], includeMidpointSnap: true);

                        _rawSnapPoints.AddRange(verts);
                        polyCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"Polylines2D collection error: {ex.Message}");
            }

            try
            {
                foreach (var circle in dxf.Entities.Circles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var points = GetSegmentPoints(linePointsByColor, ResolveColor(circle.Color, circle.Layer));
                        var center = TransformPoint(circle.Center.X, circle.Center.Y);
                        _rawSnapCenters.Add(center);

                        const int seg = 36;
                        for (int i = 0; i < seg; i++)
                        {
                            double a1 = i * Math.PI * 2.0 / seg;
                            double a2 = (i + 1) * Math.PI * 2.0 / seg;
                            AddSegment(
                                points,
                                TransformPoint(circle.Center.X + circle.Radius * Math.Cos(a1), circle.Center.Y + circle.Radius * Math.Sin(a1)),
                                TransformPoint(circle.Center.X + circle.Radius * Math.Cos(a2), circle.Center.Y + circle.Radius * Math.Sin(a2)),
                                includeMidpointSnap: false);
                        }

                        _rawSnapQuadrants.Add(TransformPoint(circle.Center.X + circle.Radius, circle.Center.Y));
                        _rawSnapQuadrants.Add(TransformPoint(circle.Center.X, circle.Center.Y + circle.Radius));
                        _rawSnapQuadrants.Add(TransformPoint(circle.Center.X - circle.Radius, circle.Center.Y));
                        _rawSnapQuadrants.Add(TransformPoint(circle.Center.X, circle.Center.Y - circle.Radius));
                        circleCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"Circles collection error: {ex.Message}");
            }

            try
            {
                foreach (var arc in dxf.Entities.Arcs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var points = GetSegmentPoints(linePointsByColor, ResolveColor(arc.Color, arc.Layer));
                        double startDeg = arc.StartAngle;
                        double endDeg = arc.EndAngle;
                        if (endDeg <= startDeg) endDeg += 360;

                        double span = endDeg - startDeg;
                        int seg = Math.Max(3, (int)(span / 5));

                        var center = TransformPoint(arc.Center.X, arc.Center.Y);
                        _rawSnapCenters.Add(center);

                        for (int i = 0; i < seg; i++)
                        {
                            double a1 = (startDeg + i * span / seg) * Math.PI / 180.0;
                            double a2 = (startDeg + (i + 1) * span / seg) * Math.PI / 180.0;
                            AddSegment(
                                points,
                                TransformPoint(arc.Center.X + arc.Radius * Math.Cos(a1), arc.Center.Y + arc.Radius * Math.Sin(a1)),
                                TransformPoint(arc.Center.X + arc.Radius * Math.Cos(a2), arc.Center.Y + arc.Radius * Math.Sin(a2)),
                                includeMidpointSnap: false);
                        }

                        double startRad = startDeg * Math.PI / 180.0;
                        double endRad = (startDeg + span) * Math.PI / 180.0;
                        _rawSnapPoints.Add(TransformPoint(arc.Center.X + arc.Radius * Math.Cos(startRad), arc.Center.Y + arc.Radius * Math.Sin(startRad)));
                        _rawSnapPoints.Add(TransformPoint(arc.Center.X + arc.Radius * Math.Cos(endRad), arc.Center.Y + arc.Radius * Math.Sin(endRad)));

                        for (int qi = 0; qi < 4; qi++)
                        {
                            double qDeg = qi * 90.0;
                            double qNorm = qDeg >= startDeg ? qDeg - startDeg : qDeg + 360 - startDeg;
                            if (qNorm < 0 || qNorm > span) continue;

                            double qRad = qDeg * Math.PI / 180.0;
                            _rawSnapQuadrants.Add(TransformPoint(arc.Center.X + arc.Radius * Math.Cos(qRad), arc.Center.Y + arc.Radius * Math.Sin(qRad)));
                        }

                        arcCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"Arcs collection error: {ex.Message}");
            }

            try
            {
                foreach (var ellipse in dxf.Entities.Ellipses)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if (!ellipse.IsVisible) continue;
                        AddEllipse(GetSegmentPoints(linePointsByColor, ResolveColor(ellipse.Color, ellipse.Layer)), ellipse);
                        ellipseCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"Ellipses collection error: {ex.Message}");
            }

            try
            {
                foreach (var spline in dxf.Entities.Splines)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var points = GetSegmentPoints(linePointsByColor, ResolveColor(spline.Color, spline.Layer));
                        var ctrlPts = new List<Point3D>();
                        foreach (var cp in spline.ControlPoints)
                        {
                            double cx = 0, cy = 0;
                            bool ok = false;

                            try { var pos = ((dynamic)cp).Position; cx = (double)pos.X; cy = (double)pos.Y; ok = true; }
                            catch { }

                            if (!ok)
                            {
                                try { cx = (double)((dynamic)cp).X; cy = (double)((dynamic)cp).Y; ok = true; }
                                catch { }
                            }

                            if (ok) ctrlPts.Add(TransformPoint(cx, cy));
                        }

                        if (ctrlPts.Count < 2) continue;
                        for (int i = 0; i < ctrlPts.Count - 1; i++)
                            AddSegment(points, ctrlPts[i], ctrlPts[i + 1], includeMidpointSnap: false);

                        _rawSnapPoints.Add(ctrlPts[0]);
                        _rawSnapPoints.Add(ctrlPts[^1]);
                        splineCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"Splines collection error: {ex.Message}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            textCount = CollectTextVisuals(dxf, _rawTextItems);
            cancellationToken.ThrowIfCancellationRequested();
            LastTextEntityCount = textCount;
            LastRenderedTextCount = ShowText ? _rawTextItems.Count : 0;

            Log($"Entities - Lines:{lineCount} Polys:{polyCount} Circles:{circleCount} Arcs:{arcCount} Ellipses:{ellipseCount} Splines:{splineCount} Text:{textCount} RenderedText:{LastRenderedTextCount}");

            var preparedLinePoints =
                new Dictionary<Color, Point3D[]>(
                    linePointsByColor.Count);
            int pointCount = 0;
            foreach ((Color color, List<Point3D> points) in
                     linePointsByColor)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Point3D[] pointSnapshot = points.ToArray();
                preparedLinePoints[color] = pointSnapshot;
                pointCount += pointSnapshot.Length;
            }

            _snapPointSource = _rawSnapPoints.ToArray();
            _snapSegmentSource = _rawSnapSegments.ToArray();
            _snapMidpointSource = _rawSnapMidpointSegments.ToArray();
            _snapCenterSource = _rawSnapCenters.ToArray();
            _snapQuadrantSource = _rawSnapQuadrants.ToArray();
            UpdateRawBounds();
            _currentRenderTransform = ComputeRenderTransform();
            UpdateSnapDataFromRawTransform(cancellationToken);
            return new PreparedDxfGeometry(
                preparedLinePoints,
                pointCount);
        }

        private async Task CommitPreparedGeometryAsync(
            PreparedDxfGeometry prepared,
            CancellationToken cancellationToken)
        {
            byte alpha = ResolveDisplayAlpha();
            foreach ((Color baseColor, Point3D[] pointSnapshot) in
                     prepared.LinePointsByColor)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var points =
                    new Point3DCollection(pointSnapshot.Length);
                for (int index = 0;
                     index < pointSnapshot.Length;
                     index++)
                {
                    points.Add(pointSnapshot[index]);
                    if ((index & 1023) == 1023)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Dispatcher.Yield(
                            DispatcherPriority.Background);
                    }
                }

                if (points.CanFreeze)
                    points.Freeze();

                Color displayColor = ResolveLineDisplayColor(baseColor);
                var dxfLines = new LinesVisual3D
                {
                    Color = Color.FromArgb(alpha, displayColor.R, displayColor.G, displayColor.B),
                    Thickness = _isSelectionHighlighted ? 2.5 : 1.0,
                    Points = points,
                    Transform = new MatrixTransform3D(_currentRenderTransform)
                };
                TagVisual(dxfLines);
                _viewport.Children.Add(dxfLines);
                _dxfVisuals.Add(dxfLines);
                _lineVisualColors[dxfLines] = baseColor;
            }

            if (ShowText)
                EnsureTextVisualsCreated();
        }

        private List<Point3D> GetSegmentPoints(
            Dictionary<Color, List<Point3D>> linePointsByColor,
            Color color)
        {
            var baseColor = color;
            if (!linePointsByColor.TryGetValue(baseColor, out var points))
            {
                points = new List<Point3D>();
                linePointsByColor[baseColor] = points;
            }

            return points;
        }

        private static Color ResolveColor(AciColor color, netDxf.Tables.Layer layer)
        {
            try
            {
                var resolved = color.IsByLayer ? layer.Color : color;
                if (!resolved.IsByLayer && !resolved.IsByBlock)
                    return Color.FromRgb(resolved.R, resolved.G, resolved.B);
            }
            catch
            {
            }

            return Color.FromRgb(160, 160, 160);
        }

        private Point3D TransformPoint(double x, double y, double z = 0.002)
            => new(x, y, z);

        private void AddSegment(
            List<Point3D>? points,
            Point3D a,
            Point3D b,
            bool includeMidpointSnap)
        {
            if (!IsFinite(a) || !IsFinite(b)) return;

            points?.Add(a);
            points?.Add(b);
            _rawSnapSegments.Add((a, b));
            if (includeMidpointSnap)
                _rawSnapMidpointSegments.Add((a, b));
        }

        private void AddEllipse(List<Point3D>? points, Ellipse ellipse)
        {
            double majorRadius = Math.Abs(ellipse.MajorAxis) / 2.0;
            double minorRadius = Math.Abs(ellipse.MinorAxis) / 2.0;
            if (majorRadius <= 1e-9 || minorRadius <= 1e-9) return;

            double startDeg = NormalizeDegrees(ellipse.StartAngle);
            double endDeg = NormalizeDegrees(ellipse.EndAngle);
            double spanDeg = ellipse.IsFullEllipse ? 360.0 : endDeg - startDeg;
            if (spanDeg <= 1e-9) spanDeg += 360.0;

            int seg = Math.Clamp((int)Math.Ceiling(spanDeg / 5.0), 8, 144);
            var center = TransformPoint(ellipse.Center.X, ellipse.Center.Y);
            _rawSnapCenters.Add(center);

            var previous = TransformEllipsePoint(ellipse, startDeg);
            for (int i = 1; i <= seg; i++)
            {
                double angle = startDeg + spanDeg * i / seg;
                var current = TransformEllipsePoint(ellipse, angle);
                AddSegment(points, previous, current, includeMidpointSnap: false);
                previous = current;
            }

            if (!ellipse.IsFullEllipse)
            {
                _rawSnapPoints.Add(TransformEllipsePoint(ellipse, startDeg));
                _rawSnapPoints.Add(TransformEllipsePoint(ellipse, startDeg + spanDeg));
            }

            for (int qi = 0; qi < 4; qi++)
            {
                double quadrant = qi * 90.0;
                if (!ellipse.IsFullEllipse && !IsAngleWithinSweep(quadrant, startDeg, spanDeg))
                    continue;

                _rawSnapQuadrants.Add(TransformEllipsePoint(ellipse, quadrant));
            }
        }

        private Point3D TransformEllipsePoint(Ellipse ellipse, double angleDegrees)
        {
            double majorRadius = ellipse.MajorAxis / 2.0;
            double minorRadius = ellipse.MinorAxis / 2.0;
            double angle = angleDegrees * Math.PI / 180.0;
            double rotation = ellipse.Rotation * Math.PI / 180.0;
            double localX = majorRadius * Math.Cos(angle);
            double localY = minorRadius * Math.Sin(angle);
            double cos = Math.Cos(rotation);
            double sin = Math.Sin(rotation);

            return TransformPoint(
                ellipse.Center.X + localX * cos - localY * sin,
                ellipse.Center.Y + localX * sin + localY * cos);
        }

        private int CollectTextVisuals(DxfDocument dxf, List<DxfTextVisualData>? textItems)
        {
            int count = 0;

            try
            {
                foreach (var text in dxf.Entities.Texts)
                {
                    try
                    {
                        if (!text.IsVisible) continue;
                        count += TryAddTextVisualData(
                            textItems,
                            text.Value,
                            text.Position.X,
                            text.Position.Y,
                            text.Height,
                            text.Rotation,
                            text.IsBackward,
                            text.IsUpsideDown,
                            ToHorizontalAlignment(text.Alignment),
                            ToVerticalAlignment(text.Alignment));
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Texts collection error: {ex.Message}"); }

            try
            {
                foreach (var text in dxf.Entities.MTexts)
                {
                    try
                    {
                        if (!text.IsVisible) continue;
                        count += TryAddTextVisualData(
                            textItems,
                            text.Value,
                            text.Position.X,
                            text.Position.Y,
                            text.Height,
                            text.Rotation,
                            false,
                            false,
                            ToHorizontalAlignment(text.AttachmentPoint),
                            ToVerticalAlignment(text.AttachmentPoint));
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"MTexts collection error: {ex.Message}"); }

            try
            {
                foreach (var insert in dxf.Entities.Inserts)
                {
                    try
                    {
                        if (!insert.IsVisible) continue;
                        count += CollectInsertTextVisuals(insert, textItems, Dxf2DTransform.Identity, depth: 0);
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Insert text collection error: {ex.Message}"); }

            try
            {
                foreach (var dimension in dxf.Entities.Dimensions)
                {
                    try
                    {
                        if (!dimension.IsVisible) continue;
                        count += CollectDimensionTextVisuals(dimension, textItems);
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Dimension text collection error: {ex.Message}"); }

            return count;
        }

        private int CollectDimensionTextVisuals(Dimension dimension, List<DxfTextVisualData>? textItems)
        {
            int count = 0;

            if (dimension.Block != null)
            {
                foreach (var text in dimension.Block.Entities.OfType<Text>())
                {
                    if (!text.IsVisible) continue;
                    count += TryAddTextVisualData(
                        textItems,
                        text.Value,
                        text.Position.X,
                        text.Position.Y,
                        text.Height,
                        text.Rotation,
                        text.IsBackward,
                        text.IsUpsideDown,
                        ToHorizontalAlignment(text.Alignment),
                        ToVerticalAlignment(text.Alignment));
                }

                foreach (var text in dimension.Block.Entities.OfType<MText>())
                {
                    if (!text.IsVisible) continue;
                    count += TryAddTextVisualData(
                        textItems,
                        text.Value,
                        text.Position.X,
                        text.Position.Y,
                        text.Height,
                        text.Rotation,
                        false,
                        false,
                        ToHorizontalAlignment(text.AttachmentPoint),
                        ToVerticalAlignment(text.AttachmentPoint));
                }
            }

            if (count > 0)
                return count;

            string? textValue = dimension.UserText;
            if (string.IsNullOrWhiteSpace(textValue) || textValue == "<>")
                textValue = dimension.Measurement.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            if (textValue == " ")
                return 0;

            return TryAddTextVisualData(
                textItems,
                textValue,
                dimension.TextReferencePoint.X,
                dimension.TextReferencePoint.Y,
                dimension.Style.TextHeight,
                dimension.TextRotation,
                false,
                false,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }

        private int CollectInsertTextVisuals(
            Insert insert,
            List<DxfTextVisualData>? textItems,
            Dxf2DTransform parentTransform,
            int depth)
        {
            if (depth > 8)
                return 0;

            int count = 0;

            try
            {
                foreach (var attribute in insert.Attributes)
                {
                    try
                    {
                        if (!attribute.IsVisible) continue;
                        var position = parentTransform.Transform(attribute.Position.X, attribute.Position.Y);
                        count += TryAddTextVisualData(
                            textItems,
                            attribute.Value,
                            position.X,
                            position.Y,
                            parentTransform.TransformLength(attribute.Height),
                            parentTransform.TransformAngle(attribute.Rotation),
                            attribute.IsBackward,
                            attribute.IsUpsideDown,
                            ToHorizontalAlignment(attribute.Alignment),
                            ToVerticalAlignment(attribute.Alignment));
                    }
                    catch { }
                }
            }
            catch { }

            var insertTransform = parentTransform.Compose(Dxf2DTransform.FromInsert(insert));
            try
            {
                foreach (var text in insert.Block.Entities.OfType<Text>())
                {
                    try
                    {
                        if (!text.IsVisible) continue;
                        var position = insertTransform.Transform(text.Position.X, text.Position.Y);
                        count += TryAddTextVisualData(
                            textItems,
                            text.Value,
                            position.X,
                            position.Y,
                            insertTransform.TransformLength(text.Height),
                            insertTransform.TransformAngle(text.Rotation),
                            text.IsBackward,
                            text.IsUpsideDown,
                            ToHorizontalAlignment(text.Alignment),
                            ToVerticalAlignment(text.Alignment));
                    }
                    catch { }
                }

                foreach (var text in insert.Block.Entities.OfType<MText>())
                {
                    try
                    {
                        if (!text.IsVisible) continue;
                        var position = insertTransform.Transform(text.Position.X, text.Position.Y);
                        count += TryAddTextVisualData(
                            textItems,
                            text.Value,
                            position.X,
                            position.Y,
                            insertTransform.TransformLength(text.Height),
                            insertTransform.TransformAngle(text.Rotation),
                            false,
                            false,
                            ToHorizontalAlignment(text.AttachmentPoint),
                            ToVerticalAlignment(text.AttachmentPoint));
                    }
                    catch { }
                }

                foreach (var nestedInsert in insert.Block.Entities.OfType<Insert>())
                {
                    try
                    {
                        if (!nestedInsert.IsVisible) continue;
                        count += CollectInsertTextVisuals(nestedInsert, textItems, insertTransform, depth + 1);
                    }
                    catch { }
                }
            }
            catch { }

            return count;
        }

        private int TryAddTextVisualData(
            List<DxfTextVisualData>? textItems,
            string? value,
            double x,
            double y,
            double height,
            double rotationDegrees,
            bool isBackward,
            bool isUpsideDown,
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment verticalAlignment = VerticalAlignment.Bottom)
        {
            string text = CleanDxfText(value);
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            if (textItems != null &&
                TryCreateTextVisualData(
                    text,
                    x,
                    y,
                    height,
                    rotationDegrees,
                    isBackward,
                    isUpsideDown,
                    horizontalAlignment,
                    verticalAlignment,
                    out var item))
            {
                textItems.Add(item);
            }

            return 1;
        }

        private bool TryCreateTextVisualData(
            string? value,
            double x,
            double y,
            double height,
            double rotationDegrees,
            bool isBackward,
            bool isUpsideDown,
            HorizontalAlignment horizontalAlignment,
            VerticalAlignment verticalAlignment,
            out DxfTextVisualData item)
        {
            item = new DxfTextVisualData();
            string text = CleanDxfText(value);
            if (string.IsNullOrWhiteSpace(text)) return false;

            item.Text = text;
            item.Position = TransformPoint(x, y, 0.006);
            item.Height = Math.Abs(height);
            if (!double.IsFinite(item.Height) || item.Height <= 1e-9)
                item.Height = 1.0;

            double angle = rotationDegrees * Math.PI / 180.0;
            var textDirection = TransformDirection(Math.Cos(angle), Math.Sin(angle));
            var upDirection = TransformDirection(-Math.Sin(angle), Math.Cos(angle));
            if (isBackward) textDirection *= -1.0;
            if (isUpsideDown) upDirection *= -1.0;
            item.TextDirection = textDirection;
            item.UpDirection = upDirection;
            item.HorizontalAlignment = horizontalAlignment;
            item.VerticalAlignment = verticalAlignment;
            return true;
        }

        private void AddTextVisual(DxfTextVisualData item)
        {
            byte alpha = ResolveDisplayAlpha();
            Color textColor = ResolveTextDisplayColor();
            var visual = new TextVisual3D
            {
                Position = item.Position,
                TextDirection = item.TextDirection,
                UpDirection = item.UpDirection,
                Height = item.Height,
                HorizontalAlignment = item.HorizontalAlignment,
                VerticalAlignment = item.VerticalAlignment,
                FontFamily = new FontFamily("Arial"),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(
                    Color.FromArgb(alpha, textColor.R, textColor.G, textColor.B)),
                Background = Brushes.Transparent,
                IsDoubleSided = true,
                Transform = new MatrixTransform3D(_currentRenderTransform),
                Text = item.Text
            };

            TagVisual(visual);
            _viewport.Children.Add(visual);
            _dxfVisuals.Add(visual);
            _textVisuals.Add(visual);
        }

        private static void NormalizeTextHeights(
            List<DxfTextVisualData> textItems,
            IReadOnlyList<Point3D> linePoints)
        {
            if (textItems.Count == 0)
                return;

            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;

            void Include(Point3D point)
            {
                if (!IsFinite(point)) return;
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            foreach (var point in linePoints)
                Include(point);
            foreach (var item in textItems)
                Include(item.Position);

            double diagonal = double.IsFinite(minX) && double.IsFinite(minY) && double.IsFinite(maxX) && double.IsFinite(maxY)
                ? Math.Sqrt((maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY))
                : 0.0;

            var heights = textItems
                .Select(item => item.Height)
                .Where(height => double.IsFinite(height) && height > 0)
                .OrderBy(height => height)
                .ToArray();

            double median = heights.Length > 0 ? heights[heights.Length / 2] : 1.0;
            double minAllowed = diagonal > 0 ? Math.Max(1.0, diagonal * 0.0025) : 1.0;
            double maxAllowed = diagonal > 0 ? Math.Max(minAllowed, diagonal * 0.018) : Math.Max(minAllowed, median);
            double fixedHeight = Math.Clamp(median, minAllowed, maxAllowed);

            foreach (var item in textItems)
                item.Height = fixedHeight;
        }

        private static HorizontalAlignment ToHorizontalAlignment(DxfTextAlignment alignment)
            => alignment switch
            {
                DxfTextAlignment.TopCenter or
                DxfTextAlignment.MiddleCenter or
                DxfTextAlignment.BottomCenter or
                DxfTextAlignment.BaselineCenter or
                DxfTextAlignment.Middle => HorizontalAlignment.Center,

                DxfTextAlignment.TopRight or
                DxfTextAlignment.MiddleRight or
                DxfTextAlignment.BottomRight or
                DxfTextAlignment.BaselineRight => HorizontalAlignment.Right,

                _ => HorizontalAlignment.Left
            };

        private static VerticalAlignment ToVerticalAlignment(DxfTextAlignment alignment)
            => alignment switch
            {
                DxfTextAlignment.TopLeft or
                DxfTextAlignment.TopCenter or
                DxfTextAlignment.TopRight => VerticalAlignment.Top,

                DxfTextAlignment.MiddleLeft or
                DxfTextAlignment.MiddleCenter or
                DxfTextAlignment.MiddleRight or
                DxfTextAlignment.Middle => VerticalAlignment.Center,

                _ => VerticalAlignment.Bottom
            };

        private static HorizontalAlignment ToHorizontalAlignment(MTextAttachmentPoint attachmentPoint)
            => attachmentPoint switch
            {
                MTextAttachmentPoint.TopCenter or
                MTextAttachmentPoint.MiddleCenter or
                MTextAttachmentPoint.BottomCenter => HorizontalAlignment.Center,

                MTextAttachmentPoint.TopRight or
                MTextAttachmentPoint.MiddleRight or
                MTextAttachmentPoint.BottomRight => HorizontalAlignment.Right,

                _ => HorizontalAlignment.Left
            };

        private static VerticalAlignment ToVerticalAlignment(MTextAttachmentPoint attachmentPoint)
            => attachmentPoint switch
            {
                MTextAttachmentPoint.TopLeft or
                MTextAttachmentPoint.TopCenter or
                MTextAttachmentPoint.TopRight => VerticalAlignment.Top,

                MTextAttachmentPoint.MiddleLeft or
                MTextAttachmentPoint.MiddleCenter or
                MTextAttachmentPoint.MiddleRight => VerticalAlignment.Center,

                _ => VerticalAlignment.Bottom
            };

        private Vector3D TransformDirection(double x, double y)
        {
            var direction = new Vector3D(x, y, 0);
            if (!IsFinite(direction) || direction.LengthSquared <= 1e-18)
                return new Vector3D(1, 0, 0);

            direction.Normalize();
            return direction;
        }

        private double TransformLength(double x, double y)
        {
            var direction = new Vector3D(x, y, 0);
            return IsFinite(direction) ? direction.Length : 1.0;
        }

        private static bool IsFinite(Point3D point)
            => double.IsFinite(point.X) &&
               double.IsFinite(point.Y) &&
               double.IsFinite(point.Z);

        private static bool IsFinite(Vector3D vector)
            => double.IsFinite(vector.X) &&
               double.IsFinite(vector.Y) &&
               double.IsFinite(vector.Z);

        private static double NormalizeDegrees(double angle)
            => ((angle % 360.0) + 360.0) % 360.0;

        private static bool IsAngleWithinSweep(double angle, double start, double span)
        {
            double offset = NormalizeDegrees(angle - start);
            return offset >= -1e-9 && offset <= span + 1e-9;
        }

        private static string CleanDxfText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            return value
                .Replace("\\P", Environment.NewLine, StringComparison.OrdinalIgnoreCase)
                .Replace("\\~", " ", StringComparison.Ordinal)
                .Replace("\\L", "", StringComparison.OrdinalIgnoreCase)
                .Replace("\\O", "", StringComparison.OrdinalIgnoreCase)
                .Replace("\\K", "", StringComparison.OrdinalIgnoreCase)
                .Replace("{", "", StringComparison.Ordinal)
                .Replace("}", "", StringComparison.Ordinal)
                .Trim();
        }

        public void ApplyDisplayState(Matrix3D alignmentTransform, double opacity, bool showText)
        {
            AlignmentTransform = alignmentTransform;
            Opacity = opacity;
            ShowText = showText;
            ApplyCurrentTransform();
        }

        public void ApplyDisplayState(Matrix3D alignmentTransform, double opacity, bool showText, bool useDxfColors)
        {
            AlignmentTransform = alignmentTransform;
            Opacity = opacity;
            ShowText = showText;
            UseDxfColors = useDxfColors;
            ApplyCurrentTransform();
        }

        private void EnsureTextVisualsCreated()
        {
            if (_textVisuals.Count > 0 || _rawTextItems.Count == 0)
                return;

            foreach (var text in _rawTextItems)
                AddTextVisual(text);
        }

        private void ApplyTextVisibility()
        {
            if (!IsLoaded)
                return;

            if (ShowText)
            {
                EnsureTextVisualsCreated();
                foreach (var visual in _textVisuals)
                {
                    if (IsVisible && !_viewport.Children.Contains(visual))
                        _viewport.Children.Add(visual);
                }

                LastRenderedTextCount = _textVisuals.Count;
                ApplyCurrentTransform(raiseSnapChanged: false);
            }
            else
            {
                foreach (var visual in _textVisuals)
                    _viewport.Children.Remove(visual);

                LastRenderedTextCount = 0;
            }
        }

        private void ApplyCurrentTransform(bool raiseSnapChanged = true)
        {
            _currentRenderTransform = ComputeRenderTransform();
            var visualTransform = new MatrixTransform3D(_currentRenderTransform);

            foreach (var visual in _dxfVisuals)
                visual.Transform = visualTransform;
            if (_zoneContrastVisual != null)
                _zoneContrastVisual.Transform = visualTransform;

            ScheduleZoneContrastRefresh();
            ScheduleSnapDataRefresh(_currentRenderTransform, raiseSnapChanged);
        }

        private async void ScheduleZoneContrastRefresh()
        {
            int generation =
                Interlocked.Increment(ref _zoneContrastRefreshGeneration);
            if (!IsLoaded)
                return;

            ZoneOverlayRegion[] regions = _zoneContrastRegions.ToArray();
            if (regions.Length == 0)
            {
                ApplyPreparedZoneContrast(
                    new PreparedZoneContrast(
                        Array.Empty<Point3D>(),
                        Array.Empty<bool>()),
                    generation);
                return;
            }

            (Point3D A, Point3D B)[] segments =
                _rawSnapSegments.ToArray();
            Point3D[] textPositions = _rawTextItems
                .Select(item => item.Position)
                .ToArray();
            Matrix3D transform = _currentRenderTransform;
            try
            {
                PreparedZoneContrast prepared = await Task.Run(() =>
                    PrepareZoneContrast(
                        segments,
                        textPositions,
                        transform,
                        regions));
                ApplyPreparedZoneContrast(prepared, generation);
            }
            catch (Exception exception)
            {
                Log($"Zone contrast preparation failed: {exception.Message}");
            }
        }

        private static PreparedZoneContrast PrepareZoneContrast(
            IReadOnlyList<(Point3D A, Point3D B)> segments,
            IReadOnlyList<Point3D> textPositions,
            Matrix3D transform,
            IReadOnlyList<ZoneOverlayRegion> regions)
        {
            var linePoints = new List<Point3D>();
            foreach ((Point3D A, Point3D B) segment in segments)
            {
                Point3D worldStart = transform.Transform(segment.A);
                Point3D worldEnd = transform.Transform(segment.B);
                if (!IsFinite(worldStart) ||
                    !IsFinite(worldEnd) ||
                    !ZoneOverlayGeometry.TouchesAny(
                        worldStart,
                        worldEnd,
                        regions))
                {
                    continue;
                }

                linePoints.Add(new Point3D(
                    segment.A.X,
                    segment.A.Y,
                    segment.A.Z + ZoneOverlayDepth.DxfContrast));
                linePoints.Add(new Point3D(
                    segment.B.X,
                    segment.B.Y,
                    segment.B.Z + ZoneOverlayDepth.DxfContrast));
            }

            var contrastedText = new bool[textPositions.Count];
            for (int index = 0; index < textPositions.Count; index++)
            {
                Point3D worldPosition =
                    transform.Transform(textPositions[index]);
                contrastedText[index] =
                    IsFinite(worldPosition) &&
                    ZoneOverlayGeometry.TouchesAny(
                        worldPosition,
                        regions);
            }

            return new PreparedZoneContrast(
                linePoints.ToArray(),
                contrastedText);
        }

        private void ApplyPreparedZoneContrast(
            PreparedZoneContrast prepared,
            int generation)
        {
            if (generation != _zoneContrastRefreshGeneration ||
                !IsLoaded)
            {
                return;
            }

            if (_zoneContrastVisual != null)
            {
                _viewport.Children.Remove(_zoneContrastVisual);
                _zoneContrastVisual = null;
            }

            _zoneContrastTextVisuals.Clear();
            int textCount = Math.Min(_rawTextItems.Count, _textVisuals.Count);
            for (int index = 0; index < textCount; index++)
            {
                bool hasContrast =
                    index < prepared.ContrastedText.Length &&
                    prepared.ContrastedText[index];
                DxfTextVisualData source = _rawTextItems[index];
                _textVisuals[index].Position = hasContrast
                    ? new Point3D(
                        source.Position.X,
                        source.Position.Y,
                        Math.Max(
                            source.Position.Z,
                            ZoneOverlayDepth.DxfContrast))
                    : source.Position;
                if (hasContrast)
                    _zoneContrastTextVisuals.Add(_textVisuals[index]);
            }

            if (prepared.LinePoints.Length > 0)
            {
                var points = new Point3DCollection(prepared.LinePoints);
                if (points.CanFreeze)
                    points.Freeze();

                _zoneContrastVisual = new LinesVisual3D
                {
                    Color = _zoneContrastColor,
                    Thickness = _isSelectionHighlighted ? 3.0 : 1.6,
                    Points = points,
                    Transform = new MatrixTransform3D(
                        _currentRenderTransform)
                };
                TagVisual(_zoneContrastVisual);
                if (IsVisible)
                    _viewport.Children.Add(_zoneContrastVisual);
            }

            RefreshOpacity();
        }

        private Matrix3D ComputeRenderTransform()
        {
            _dxfRebase = Matrix3D.Identity;

            if (TryGetAlignedBounds(out double minX, out double minY, out double maxX, out double maxY))
            {
                double centerX = (minX + maxX) / 2.0;
                double centerY = (minY + maxY) / 2.0;
                if (Math.Max(Math.Abs(centerX), Math.Abs(centerY)) > FarCoordinateRebaseThreshold)
                {
                    _dxfRebase.Translate(new Vector3D(-centerX, -centerY, 0));
                    Log($"Rebased DXF near origin. Center=({centerX:F2}, {centerY:F2})");
                }
            }

            var renderTransform = AlignmentTransform;
            renderTransform.Append(_dxfRebase);
            return renderTransform;
        }

        private bool TryGetAlignedBounds(out double minX, out double minY, out double maxX, out double maxY)
        {
            if (_rawBounds.IsEmpty)
            {
                minX = minY = maxX = maxY = 0;
                return false;
            }

            Point3D[] corners =
            {
                new(_rawBounds.Left, _rawBounds.Top, 0),
                new(_rawBounds.Right, _rawBounds.Top, 0),
                new(_rawBounds.Right, _rawBounds.Bottom, 0),
                new(_rawBounds.Left, _rawBounds.Bottom, 0)
            };
            Point3D[] transformed = corners.Select(AlignmentTransform.Transform).ToArray();
            minX = transformed.Min(point => point.X);
            minY = transformed.Min(point => point.Y);
            maxX = transformed.Max(point => point.X);
            maxY = transformed.Max(point => point.Y);
            return true;
        }

        private void UpdateSnapDataFromRawTransform(
            CancellationToken cancellationToken)
        {
            PublishSnapData(TransformSnapData(
                _currentRenderTransform,
                _snapPointSource,
                _snapSegmentSource,
                _snapMidpointSource,
                _snapCenterSource,
                _snapQuadrantSource,
                cancellationToken));
        }

        private void UpdateRawBounds()
        {
            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;

            void Include(Point3D point)
            {
                if (!IsFinite(point))
                    return;
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            foreach (Point3D point in _snapPointSource)
                Include(point);
            foreach (Point3D point in _snapCenterSource)
                Include(point);
            foreach (Point3D point in _snapQuadrantSource)
                Include(point);
            foreach ((Point3D A, Point3D B) segment in _snapSegmentSource)
            {
                Include(segment.A);
                Include(segment.B);
            }
            foreach (DxfTextVisualData text in _rawTextItems)
                Include(text.Position);

            _rawBounds = double.IsFinite(minX)
                ? new Rect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY))
                : Rect.Empty;
        }

        private async void ScheduleSnapDataRefresh(Matrix3D transform, bool raiseSnapChanged)
        {
            int generation = Interlocked.Increment(ref _snapRefreshGeneration);
            var cancellation = new CancellationTokenSource();
            CancellationTokenSource? superseded =
                Interlocked.Exchange(
                    ref _snapRefreshCancellation,
                    cancellation);
            CancelRefresh(superseded);
            Point3D[] points = _snapPointSource;
            Point3D[] centers = _snapCenterSource;
            Point3D[] quadrants = _snapQuadrantSource;
            (Point3D A, Point3D B)[] segments = _snapSegmentSource;
            (Point3D A, Point3D B)[] midpoints = _snapMidpointSource;
            try
            {
                PreparedSnapData prepared = await Task.Run(
                    () => TransformSnapData(
                        transform,
                        points,
                        segments,
                        midpoints,
                        centers,
                        quadrants,
                        cancellation.Token),
                    cancellation.Token);
                if (generation != _snapRefreshGeneration ||
                    cancellation.IsCancellationRequested)
                    return;

                PublishSnapData(prepared);
                if (raiseSnapChanged)
                    RaiseSnapDataChanged();
            }
            catch (OperationCanceledException)
                when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Log($"Snap refresh skipped: {ex.Message}");
            }
            finally
            {
                Interlocked.CompareExchange(
                    ref _snapRefreshCancellation,
                    null,
                    cancellation);
                cancellation.Dispose();
            }
        }

        private static PreparedSnapData TransformSnapData(
            Matrix3D transform,
            IReadOnlyList<Point3D> points,
            IReadOnlyList<(Point3D A, Point3D B)> segments,
            IReadOnlyList<(Point3D A, Point3D B)> midpoints,
            IReadOnlyList<Point3D> centers,
            IReadOnlyList<Point3D> quadrants,
            CancellationToken cancellationToken)
        {
            Point3D[] transformedPoints =
                TransformPoints(points, transform, cancellationToken);
            Point3D[] transformedCenters =
                TransformPoints(centers, transform, cancellationToken);
            Point3D[] transformedQuadrants =
                TransformPoints(quadrants, transform, cancellationToken);
            (Point3D A, Point3D B)[] transformedSegments =
                TransformSegments(
                    segments,
                    transform,
                    cancellationToken);
            (Point3D A, Point3D B)[] transformedMidpoints =
                TransformSegments(
                    midpoints,
                    transform,
                    cancellationToken);
            return new PreparedSnapData(
                transformedPoints,
                transformedSegments,
                transformedMidpoints,
                transformedCenters,
                transformedQuadrants);
        }

        private static Point3D[] TransformPoints(
            IReadOnlyList<Point3D> source,
            Matrix3D transform,
            CancellationToken cancellationToken)
        {
            var result = new List<Point3D>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                if ((index & 1023) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                Point3D raw = source[index];
                Point3D point = transform.Transform(raw);
                if (IsFinite(point))
                    result.Add(point);
            }
            return result.ToArray();
        }

        private static (Point3D A, Point3D B)[] TransformSegments(
            IReadOnlyList<(Point3D A, Point3D B)> source,
            Matrix3D transform,
            CancellationToken cancellationToken)
        {
            var result = new List<(Point3D A, Point3D B)>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                if ((index & 1023) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                (Point3D A, Point3D B) raw = source[index];
                Point3D a = transform.Transform(raw.A);
                Point3D b = transform.Transform(raw.B);
                if (IsFinite(a) && IsFinite(b))
                    result.Add((a, b));
            }
            return result.ToArray();
        }

        private void PublishSnapData(PreparedSnapData prepared)
        {
            SnapPoints = prepared.Points;
            SnapSegments = prepared.Segments;
            SnapMidpointSegments = prepared.MidpointSegments;
            SnapCenters = prepared.Centers;
            SnapQuadrants = prepared.Quadrants;
            Volatile.Write(
                ref _snapDataSnapshot,
                new DxfSnapDataSnapshot(
                    prepared.Points,
                    prepared.Segments,
                    prepared.Centers,
                    prepared.Quadrants,
                    prepared.MidpointSegments));
        }

        private void RaiseSnapDataChanged() => SnapDataChanged?.Invoke();

        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
            foreach (var v in _dxfVisuals)
            {
                if (v is TextVisual3D && !ShowText)
                {
                    _viewport.Children.Remove(v);
                    continue;
                }

                if (IsVisible)
                {
                    if (!_viewport.Children.Contains(v)) _viewport.Children.Add(v);
                }
                else
                {
                    _viewport.Children.Remove(v);
                }
            }

            if (_zoneContrastVisual != null)
            {
                if (IsVisible)
                {
                    if (!_viewport.Children.Contains(_zoneContrastVisual))
                        _viewport.Children.Add(_zoneContrastVisual);
                }
                else
                {
                    _viewport.Children.Remove(_zoneContrastVisual);
                }
            }
        }

        private void RefreshOpacity()
        {
            byte alpha = ResolveDisplayAlpha();
            foreach (var visual in _dxfVisuals)
            {
                if (visual is LinesVisual3D lines)
                {
                    var baseColor = _lineVisualColors.TryGetValue(lines, out var color)
                        ? color
                        : Color.FromRgb(160, 160, 160);
                    Color displayColor = ResolveLineDisplayColor(baseColor);
                    lines.Color = Color.FromArgb(alpha, displayColor.R, displayColor.G, displayColor.B);
                }

                if (visual is TextVisual3D text)
                {
                    bool hasZoneContrast =
                        _zoneContrastTextVisuals.Contains(text);
                    Color textColor = hasZoneContrast
                        ? _zoneContrastColor
                        : ResolveTextDisplayColor();
                    byte textAlpha = hasZoneContrast
                        ? byte.MaxValue
                        : alpha;
                    text.Foreground = new SolidColorBrush(
                        Color.FromArgb(
                            textAlpha,
                            textColor.R,
                            textColor.G,
                            textColor.B));
                }
            }

            if (_zoneContrastVisual != null)
                _zoneContrastVisual.Color = _zoneContrastColor;
        }

        private void RefreshDxfColors()
        {
            byte alpha = ResolveDisplayAlpha();
            foreach (LinesVisual3D lines in _dxfVisuals.OfType<LinesVisual3D>())
            {
                Color baseColor = _lineVisualColors.TryGetValue(lines, out Color color)
                    ? color
                    : Color.FromRgb(160, 160, 160);
                Color displayColor = ResolveLineDisplayColor(baseColor);
                lines.Color = Color.FromArgb(alpha, displayColor.R, displayColor.G, displayColor.B);
            }
        }

        private byte ResolveDisplayAlpha() => (byte)(_opacity * 255);

        private Color ResolveLineDisplayColor(Color baseColor) =>
            _useDxfColors
                ? baseColor
                : Color.FromRgb(160, 160, 160);

        private static Color ResolveTextDisplayColor() =>
            Color.FromRgb(230, 230, 230);

        public void ReApplyTransform()
        {
            if (!IsLoaded) return;
            ApplyCurrentTransform();
        }

        public void SetSelectionHighlight(bool isSelected)
        {
            if (_isSelectionHighlighted == isSelected)
                return;

            _isSelectionHighlighted = isSelected;
            foreach (var visual in _dxfVisuals.OfType<LinesVisual3D>())
                visual.Thickness = isSelected ? 2.5 : 1.0;
            if (_zoneContrastVisual != null)
                _zoneContrastVisual.Thickness = isSelected ? 3.0 : 1.6;
        }

        private void TagVisual(ModelVisual3D visual)
        {
            if (_selectionTarget != null)
                visual.SetValue(FrameworkElement.TagProperty, _selectionTarget);
        }

        public void Clear()
        {
            ClearVisualsAndSnapData();
            LoadedFilePath = null;
            _loadedDocument = null;
            IsLoaded = false;
            RaiseSnapDataChanged();
        }

        private void ClearVisualsAndSnapData()
        {
            Interlocked.Increment(ref _zoneContrastRefreshGeneration);
            if (_zoneContrastVisual != null)
            {
                _viewport.Children.Remove(_zoneContrastVisual);
                _zoneContrastVisual = null;
            }

            foreach (var v in _dxfVisuals) _viewport.Children.Remove(v);
            _dxfVisuals.Clear();
            _textVisuals.Clear();
            _zoneContrastTextVisuals.Clear();
            _zoneContrastRegions = Array.Empty<ZoneOverlayRegion>();
            _lineVisualColors.Clear();
            _rawTextItems.Clear();
            _rawSnapPoints.Clear();
            _rawSnapSegments.Clear();
            _rawSnapMidpointSegments.Clear();
            _rawSnapCenters.Clear();
            _rawSnapQuadrants.Clear();
            _snapPointSource = Array.Empty<Point3D>();
            _snapSegmentSource = Array.Empty<(Point3D, Point3D)>();
            _snapMidpointSource = Array.Empty<(Point3D, Point3D)>();
            _snapCenterSource = Array.Empty<Point3D>();
            _snapQuadrantSource = Array.Empty<Point3D>();
            _rawBounds = Rect.Empty;
            Interlocked.Increment(ref _snapRefreshGeneration);
            CancelRefresh(Interlocked.Exchange(
                    ref _snapRefreshCancellation,
                    null));
            PublishSnapData(PreparedSnapData.Empty);
            _dxfRebase = Matrix3D.Identity;
            _currentRenderTransform = Matrix3D.Identity;
        }

        private static void CancelRefresh(
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
                // The completed refresh won the exchange and disposed its token.
            }
            catch (AggregateException exception)
            {
                Log($"Snap refresh cancellation callback failed: {exception.Message}");
            }
        }

        private static void Log(string msg)
            => System.Diagnostics.Debug.WriteLine($"[DxfUploader] {msg}");

        private sealed class DxfTextVisualData
        {
            public string Text { get; set; } = "";
            public Point3D Position { get; set; }
            public Vector3D TextDirection { get; set; }
            public Vector3D UpDirection { get; set; }
            public double Height { get; set; }
            public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
            public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Bottom;
        }

        private sealed record PreparedDxfGeometry(
            IReadOnlyDictionary<Color, Point3D[]> LinePointsByColor,
            int PointCount);

        private sealed record DxfLoadResult(DxfDocument? Document, string Diagnostic);

        public sealed class DxfSnapDataSnapshot
        {
            internal static DxfSnapDataSnapshot Empty { get; } =
                new(
                    Array.Empty<Point3D>(),
                    Array.Empty<(Point3D, Point3D)>(),
                    Array.Empty<Point3D>(),
                    Array.Empty<Point3D>(),
                    Array.Empty<(Point3D, Point3D)>());

            internal DxfSnapDataSnapshot(
                Point3D[] endpoints,
                (Point3D A, Point3D B)[] segments,
                Point3D[] centers,
                Point3D[] quadrants,
                (Point3D A, Point3D B)[] midpointSegments)
            {
                Endpoints = endpoints;
                Segments = segments;
                Centers = centers;
                Quadrants = quadrants;
                MidpointSegments = midpointSegments;
            }

            public ReadOnlyMemory<Point3D> Endpoints { get; }
            public ReadOnlyMemory<(Point3D A, Point3D B)> Segments { get; }
            public ReadOnlyMemory<Point3D> Centers { get; }
            public ReadOnlyMemory<Point3D> Quadrants { get; }
            public ReadOnlyMemory<(Point3D A, Point3D B)> MidpointSegments { get; }
        }

        private sealed record PreparedSnapData(
            Point3D[] Points,
            (Point3D A, Point3D B)[] Segments,
            (Point3D A, Point3D B)[] MidpointSegments,
            Point3D[] Centers,
            Point3D[] Quadrants)
        {
            public static PreparedSnapData Empty { get; } =
                new(
                    Array.Empty<Point3D>(),
                    Array.Empty<(Point3D, Point3D)>(),
                    Array.Empty<(Point3D, Point3D)>(),
                    Array.Empty<Point3D>(),
                    Array.Empty<Point3D>());
        }

        private sealed record PreparedZoneContrast(
            Point3D[] LinePoints,
            bool[] ContrastedText);

        private readonly record struct Dxf2DTransform(
            double A,
            double B,
            double C,
            double D,
            double Tx,
            double Ty)
        {
            public static Dxf2DTransform Identity { get; } = new(1, 0, 0, 1, 0, 0);

            public static Dxf2DTransform FromInsert(Insert insert)
            {
                double rotation = insert.Rotation * Math.PI / 180.0;
                double cos = Math.Cos(rotation);
                double sin = Math.Sin(rotation);
                double sx = insert.Scale.X;
                double sy = insert.Scale.Y;
                double a = sx * cos;
                double b = -sy * sin;
                double c = sx * sin;
                double d = sy * cos;
                double originX = insert.Block?.Origin.X ?? 0.0;
                double originY = insert.Block?.Origin.Y ?? 0.0;
                double tx = insert.Position.X - (a * originX + b * originY);
                double ty = insert.Position.Y - (c * originX + d * originY);
                return new Dxf2DTransform(a, b, c, d, tx, ty);
            }

            public Dxf2DTransform Compose(Dxf2DTransform child)
                => new(
                    A * child.A + B * child.C,
                    A * child.B + B * child.D,
                    C * child.A + D * child.C,
                    C * child.B + D * child.D,
                    A * child.Tx + B * child.Ty + Tx,
                    C * child.Tx + D * child.Ty + Ty);

            public (double X, double Y) Transform(double x, double y)
                => (A * x + B * y + Tx, C * x + D * y + Ty);

            public double TransformAngle(double angleDegrees)
            {
                double angle = angleDegrees * Math.PI / 180.0;
                double x = Math.Cos(angle);
                double y = Math.Sin(angle);
                return Math.Atan2(C * x + D * y, A * x + B * y) * 180.0 / Math.PI;
            }

            public double TransformLength(double length)
            {
                double xScale = Math.Sqrt(A * A + C * C);
                double yScale = Math.Sqrt(B * B + D * D);
                double scale = (xScale + yScale) / 2.0;
                return Math.Abs(length * scale);
            }
        }
    }
}
