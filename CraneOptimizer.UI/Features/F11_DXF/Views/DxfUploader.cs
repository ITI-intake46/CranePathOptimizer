using HelixToolkit.Wpf;
using DxfTextAlignment = netDxf.Entities.TextAlignment;
using netDxf;
using netDxf.Entities;
using netDxf.Header;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F11_DXF.Views
{
    public class DxfUploader
    {
        private readonly HelixViewport3D _viewport;
        private readonly List<ModelVisual3D> _dxfVisuals = new();
        private readonly List<TextVisual3D> _textVisuals = new();
        private readonly List<DxfTextVisualData> _rawTextItems = new();
        private readonly List<Point3D> _rawSnapPoints = new();
        private readonly List<(Point3D A, Point3D B)> _rawSnapSegments = new();
        private readonly List<(Point3D A, Point3D B)> _rawSnapMidpointSegments = new();
        private readonly List<Point3D> _rawSnapCenters = new();
        private readonly List<Point3D> _rawSnapQuadrants = new();

        // Large survey coordinates can destabilize WPF 3D projection. Rebase the
        // rendered overlay and snap data together when the final drawing is far
        // from the origin, including meter-scaled survey files.
        private const double FarCoordinateRebaseThreshold = 1_000_000.0;
        private Matrix3D _dxfRebase = Matrix3D.Identity;
        private Matrix3D _currentRenderTransform = Matrix3D.Identity;
        private DxfDocument? _loadedDocument;

        public List<Point3D> SnapPoints { get; private set; } = new();
        public List<(Point3D A, Point3D B)> SnapSegments { get; private set; } = new();
        public List<(Point3D A, Point3D B)> SnapMidpointSegments { get; private set; } = new();
        public List<Point3D> SnapCenters { get; private set; } = new();
        public List<Point3D> SnapQuadrants { get; private set; } = new();

        public bool IsLoaded { get; private set; }
        public string? LoadedFilePath { get; private set; }
        public DxfDocument? LoadedDocument => _loadedDocument;
        public bool IsVisible { get; private set; } = true;
        public string LastLoadDiagnostic { get; private set; } = "";

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

        public Matrix3D AlignmentTransform { get; set; } = Matrix3D.Identity;

        public DxfUploader(HelixViewport3D viewport)
        {
            _viewport = viewport;
        }

        public bool LoadDxf(string filePath, bool showText)
        {
            _showText = showText;
            return LoadDxf(filePath);
        }

        public bool LoadDxf(string filePath)
        {
            LastLoadDiagnostic = "";

            try
            {
                DxfVersion version;
                try
                {
                    version = DxfDocument.CheckDxfFileVersion(filePath);
                }
                catch (Exception ex)
                {
                    LastLoadDiagnostic = $"Cannot read DXF version: {ex.Message}";
                    return false;
                }

                Log($"DXF version detected: {version}");
                if (version < DxfVersion.AutoCad2000)
                {
                    LastLoadDiagnostic =
                        $"DXF version too old: {version}\n" +
                        "netDxf requires AutoCad2000 or higher.\n" +
                        "Save your file as AutoCAD 2010 DXF and try again.";
                    Log(LastLoadDiagnostic);
                    return false;
                }

                DxfDocument? dxf;
                try
                {
                    dxf = DxfDocument.Load(filePath);
                }
                catch (Exception ex)
                {
                    LastLoadDiagnostic = $"DxfDocument.Load threw: {ex.GetType().Name}: {ex.Message}";
                    Log(LastLoadDiagnostic);
                    return false;
                }

                if (dxf == null)
                {
                    LastLoadDiagnostic = "DxfDocument.Load returned null - file may be corrupt.";
                    Log(LastLoadDiagnostic);
                    return false;
                }

                ClearVisualsAndSnapData();
                LoadedFilePath = filePath;
                _loadedDocument = dxf;
                IsLoaded = true;

                int pointCount = RenderDxf(dxf);
                Log($"Render done. Points: {pointCount}");
                RaiseSnapDataChanged();
                return true;
            }
            catch (Exception ex)
            {
                LastLoadDiagnostic = $"Unexpected: {ex.GetType().Name}: {ex.Message}";
                Log(LastLoadDiagnostic);
                IsLoaded = false;
                return false;
            }
        }

        private int RenderDxf(DxfDocument dxf)
        {
            var pts = new Point3DCollection();
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
                    try
                    {
                        var a = TransformPoint(line.StartPoint.X, line.StartPoint.Y);
                        var b = TransformPoint(line.EndPoint.X, line.EndPoint.Y);
                        AddSegment(pts, a, b, includeMidpointSnap: true);
                        _rawSnapPoints.Add(a);
                        _rawSnapPoints.Add(b);
                        lineCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Lines collection error: {ex.Message}"); }

            try
            {
                foreach (var poly in dxf.Entities.Polylines2D)
                {
                    try
                    {
                        var verts = new List<Point3D>();
                        foreach (var v in poly.Vertexes)
                            verts.Add(TransformPoint(v.Position.X, v.Position.Y));

                        if (verts.Count < 2) continue;

                        int count = poly.IsClosed ? verts.Count : verts.Count - 1;
                        for (int i = 0; i < count; i++)
                            AddSegment(pts, verts[i], verts[(i + 1) % verts.Count], includeMidpointSnap: true);

                        _rawSnapPoints.AddRange(verts);
                        polyCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Polylines2D collection error: {ex.Message}"); }

            try
            {
                foreach (var circle in dxf.Entities.Circles)
                {
                    try
                    {
                        var center = TransformPoint(circle.Center.X, circle.Center.Y);
                        _rawSnapCenters.Add(center);

                        const int seg = 36;
                        for (int i = 0; i < seg; i++)
                        {
                            double a1 = i * Math.PI * 2.0 / seg;
                            double a2 = (i + 1) * Math.PI * 2.0 / seg;
                            AddSegment(
                                pts,
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
            catch (Exception ex) { Log($"Circles collection error: {ex.Message}"); }

            try
            {
                foreach (var arc in dxf.Entities.Arcs)
                {
                    try
                    {
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
                                pts,
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
            catch (Exception ex) { Log($"Arcs collection error: {ex.Message}"); }

            try
            {
                foreach (var ellipse in dxf.Entities.Ellipses)
                {
                    try
                    {
                        if (!ellipse.IsVisible) continue;
                        AddEllipse(pts, ellipse);
                        ellipseCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Ellipses collection error: {ex.Message}"); }

            try
            {
                foreach (var spline in dxf.Entities.Splines)
                {
                    try
                    {
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
                            AddSegment(pts, ctrlPts[i], ctrlPts[i + 1], includeMidpointSnap: false);

                        _rawSnapPoints.Add(ctrlPts[0]);
                        _rawSnapPoints.Add(ctrlPts[^1]);
                        splineCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Splines collection error: {ex.Message}"); }

            textCount = CollectTextVisuals(dxf, _rawTextItems);
            LastTextEntityCount = textCount;
            LastRenderedTextCount = ShowText ? _rawTextItems.Count : 0;

            Log($"Entities - Lines:{lineCount} Polys:{polyCount} Circles:{circleCount} Arcs:{arcCount} Ellipses:{ellipseCount} Splines:{splineCount} Text:{textCount} RenderedText:{LastRenderedTextCount}");

            if (pts.Count > 0)
            {
                byte alpha = (byte)(_opacity * 255);
                var dxfLines = new LinesVisual3D
                {
                    Color = Color.FromArgb(alpha, 160, 160, 160),
                    Thickness = 1.0,
                    Points = pts
                };
                _viewport.Children.Add(dxfLines);
                _dxfVisuals.Add(dxfLines);
            }

            if (ShowText)
                EnsureTextVisualsCreated();

            ApplyCurrentTransform(raiseSnapChanged: false);

            return pts.Count;
        }

        private Point3D TransformPoint(double x, double y, double z = 0.002)
            => new(x, y, z);

        private void AddSegment(Point3DCollection? points, Point3D a, Point3D b, bool includeMidpointSnap)
        {
            if (!IsFinite(a) || !IsFinite(b)) return;

            points?.Add(a);
            points?.Add(b);
            _rawSnapSegments.Add((a, b));
            if (includeMidpointSnap)
                _rawSnapMidpointSegments.Add((a, b));
        }

        private void AddEllipse(Point3DCollection? points, Ellipse ellipse)
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
            byte alpha = (byte)(_opacity * 255);
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
                Foreground = new SolidColorBrush(Color.FromArgb(alpha, 230, 230, 230)),
                Background = Brushes.Transparent,
                IsDoubleSided = true,
                Transform = new MatrixTransform3D(_currentRenderTransform),
                Text = item.Text
            };

            _viewport.Children.Add(visual);
            _dxfVisuals.Add(visual);
            _textVisuals.Add(visual);
        }

        private static void NormalizeTextHeights(List<DxfTextVisualData> textItems, Point3DCollection linePoints)
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

        private void RebaseFarFromOrigin(Point3DCollection points, List<DxfTextVisualData> textItems)
        {
            if (points.Count == 0 && textItems.Count == 0) return;

            try
            {
                double minX = double.PositiveInfinity;
                double minY = double.PositiveInfinity;
                double maxX = double.NegativeInfinity;
                double maxY = double.NegativeInfinity;

                void Include(Point3D p)
                {
                    if (!IsFinite(p)) return;
                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                }

                foreach (var p in points)
                    Include(p);
                foreach (var text in textItems)
                    Include(text.Position);

                if (!double.IsFinite(minX) || !double.IsFinite(minY) ||
                    !double.IsFinite(maxX) || !double.IsFinite(maxY))
                {
                    return;
                }

                double centerX = (minX + maxX) / 2.0;
                double centerY = (minY + maxY) / 2.0;
                if (Math.Max(Math.Abs(centerX), Math.Abs(centerY)) <= FarCoordinateRebaseThreshold)
                    return;

                _dxfRebase = Matrix3D.Identity;
                _dxfRebase.Translate(new Vector3D(-centerX, -centerY, 0));

                for (int i = 0; i < points.Count; i++)
                    points[i] = _dxfRebase.Transform(points[i]);
                for (int i = 0; i < SnapPoints.Count; i++)
                    SnapPoints[i] = _dxfRebase.Transform(SnapPoints[i]);
                for (int i = 0; i < SnapCenters.Count; i++)
                    SnapCenters[i] = _dxfRebase.Transform(SnapCenters[i]);
                for (int i = 0; i < SnapQuadrants.Count; i++)
                    SnapQuadrants[i] = _dxfRebase.Transform(SnapQuadrants[i]);
                for (int i = 0; i < SnapSegments.Count; i++)
                    SnapSegments[i] = (_dxfRebase.Transform(SnapSegments[i].A), _dxfRebase.Transform(SnapSegments[i].B));
                for (int i = 0; i < SnapMidpointSegments.Count; i++)
                    SnapMidpointSegments[i] = (_dxfRebase.Transform(SnapMidpointSegments[i].A), _dxfRebase.Transform(SnapMidpointSegments[i].B));
                for (int i = 0; i < textItems.Count; i++)
                    textItems[i].Position = _dxfRebase.Transform(textItems[i].Position);

                Log($"Rebased DXF near origin. Center=({centerX:F2}, {centerY:F2})");
            }
            catch (Exception ex)
            {
                Log($"Rebase skipped: {ex.Message}");
            }
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

            UpdateSnapDataFromRawTransform();

            if (raiseSnapChanged)
                RaiseSnapDataChanged();
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
            double localMinX = double.PositiveInfinity;
            double localMinY = double.PositiveInfinity;
            double localMaxX = double.NegativeInfinity;
            double localMaxY = double.NegativeInfinity;
            bool hasPoint = false;

            void IncludeRaw(Point3D rawPoint)
            {
                if (!IsFinite(rawPoint)) return;

                var point = AlignmentTransform.Transform(rawPoint);
                if (!IsFinite(point)) return;

                hasPoint = true;
                localMinX = Math.Min(localMinX, point.X);
                localMinY = Math.Min(localMinY, point.Y);
                localMaxX = Math.Max(localMaxX, point.X);
                localMaxY = Math.Max(localMaxY, point.Y);
            }

            foreach (var point in _rawSnapPoints)
                IncludeRaw(point);
            foreach (var point in _rawSnapCenters)
                IncludeRaw(point);
            foreach (var point in _rawSnapQuadrants)
                IncludeRaw(point);
            foreach (var segment in _rawSnapSegments)
            {
                IncludeRaw(segment.A);
                IncludeRaw(segment.B);
            }
            foreach (var text in _rawTextItems)
                IncludeRaw(text.Position);

            minX = localMinX;
            minY = localMinY;
            maxX = localMaxX;
            maxY = localMaxY;
            return hasPoint;
        }

        private void UpdateSnapDataFromRawTransform()
        {
            SnapPoints.Clear();
            SnapSegments.Clear();
            SnapMidpointSegments.Clear();
            SnapCenters.Clear();
            SnapQuadrants.Clear();

            foreach (var point in _rawSnapPoints)
                AddTransformedPoint(SnapPoints, point);
            foreach (var center in _rawSnapCenters)
                AddTransformedPoint(SnapCenters, center);
            foreach (var quadrant in _rawSnapQuadrants)
                AddTransformedPoint(SnapQuadrants, quadrant);
            foreach (var segment in _rawSnapSegments)
                AddTransformedSegment(SnapSegments, segment);
            foreach (var segment in _rawSnapMidpointSegments)
                AddTransformedSegment(SnapMidpointSegments, segment);
        }

        private void AddTransformedPoint(List<Point3D> target, Point3D rawPoint)
        {
            var point = _currentRenderTransform.Transform(rawPoint);
            if (IsFinite(point))
                target.Add(point);
        }

        private void AddTransformedSegment(List<(Point3D A, Point3D B)> target, (Point3D A, Point3D B) rawSegment)
        {
            var a = _currentRenderTransform.Transform(rawSegment.A);
            var b = _currentRenderTransform.Transform(rawSegment.B);
            if (IsFinite(a) && IsFinite(b))
                target.Add((a, b));
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
        }

        private void RefreshOpacity()
        {
            byte alpha = (byte)(_opacity * 255);
            foreach (var visual in _dxfVisuals)
            {
                if (visual is LinesVisual3D lines)
                    lines.Color = Color.FromArgb(alpha, 160, 160, 160);

                if (visual is TextVisual3D text)
                    text.Foreground = new SolidColorBrush(Color.FromArgb(alpha, 230, 230, 230));
            }
        }

        public void ReApplyTransform()
        {
            if (!IsLoaded) return;
            ApplyCurrentTransform();
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
            foreach (var v in _dxfVisuals) _viewport.Children.Remove(v);
            _dxfVisuals.Clear();
            _textVisuals.Clear();
            _rawTextItems.Clear();
            _rawSnapPoints.Clear();
            _rawSnapSegments.Clear();
            _rawSnapMidpointSegments.Clear();
            _rawSnapCenters.Clear();
            _rawSnapQuadrants.Clear();
            SnapPoints.Clear();
            SnapSegments.Clear();
            SnapMidpointSegments.Clear();
            SnapCenters.Clear();
            SnapQuadrants.Clear();
            _dxfRebase = Matrix3D.Identity;
            _currentRenderTransform = Matrix3D.Identity;
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
