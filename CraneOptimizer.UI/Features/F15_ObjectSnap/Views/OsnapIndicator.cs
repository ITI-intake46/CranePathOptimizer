using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Views
{
    /// <summary>
    /// Reusable AutoCAD-style Object Snap overlay. Geometry, brushes, and
    /// controls are allocated once; pointer updates only change transforms,
    /// text, colour references, and visibility.
    /// </summary>
    public sealed class OsnapIndicator
    {
        private const double MarkerHalfSize = 9.0;
        private const double StrokeWidth = 1.6;

        private static readonly Brush ZoneBrush =
            CreateBrush(Color.FromRgb(0xFF, 0xDD, 0x00));
        private static readonly Brush DxfBrush =
            CreateBrush(Color.FromRgb(0xFF, 0x88, 0x00));
        private static readonly Brush IfcBrush =
            CreateBrush(Color.FromRgb(0x00, 0xDD, 0xFF));
        private static readonly Brush GridBrush =
            CreateBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
        private static readonly Brush LabelBackgroundBrush =
            CreateBrush(Color.FromArgb(140, 0, 0, 0));

        private static readonly Geometry EndpointGeometry =
            Freeze(new RectangleGeometry(
                new Rect(
                    -MarkerHalfSize,
                    -MarkerHalfSize,
                    MarkerHalfSize * 2,
                    MarkerHalfSize * 2)));
        private static readonly Geometry MidpointGeometry =
            CreatePolygon(
                new Point(0, -MarkerHalfSize),
                new Point(MarkerHalfSize, MarkerHalfSize),
                new Point(-MarkerHalfSize, MarkerHalfSize));
        private static readonly Geometry IntersectionGeometry =
            CreateLines(
                new Point(-MarkerHalfSize, -MarkerHalfSize),
                new Point(MarkerHalfSize, MarkerHalfSize),
                new Point(MarkerHalfSize, -MarkerHalfSize),
                new Point(-MarkerHalfSize, MarkerHalfSize));
        private static readonly Geometry PerpendicularGeometry =
            CreateRightAngle();
        private static readonly Geometry DiamondGeometry =
            CreatePolygon(
                new Point(0, -MarkerHalfSize),
                new Point(MarkerHalfSize, 0),
                new Point(0, MarkerHalfSize),
                new Point(-MarkerHalfSize, 0));
        private static readonly Geometry GridGeometry =
            CreateLines(
                new Point(-MarkerHalfSize, 0),
                new Point(MarkerHalfSize, 0),
                new Point(0, -MarkerHalfSize),
                new Point(0, MarkerHalfSize));
        private static readonly Geometry CenterGeometry =
            Freeze(new EllipseGeometry(
                new Point(0, 0),
                MarkerHalfSize,
                MarkerHalfSize));

        private readonly Path _marker;
        private readonly TextBlock _label;
        private readonly Ellipse _dot;
        private readonly TranslateTransform _markerTransform = new();

        public OsnapIndicator(Canvas canvas)
        {
            ArgumentNullException.ThrowIfNull(canvas);

            _dot = new Ellipse
            {
                Width = 3,
                Height = 3,
                Fill = Brushes.White,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            _marker = new Path
            {
                StrokeThickness = StrokeWidth,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                RenderTransform = _markerTransform
            };

            _label = new TextBlock
            {
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                Background = LabelBackgroundBrush
            };

            canvas.Children.Add(_dot);
            canvas.Children.Add(_marker);
            canvas.Children.Add(_label);
        }

        /// <summary>Applies one accepted result on the UI thread.</summary>
        public void Update(OsnapResult? result)
        {
            if (result?.ScreenPoint is not Point point)
            {
                Hide();
                return;
            }

            Brush brush = SourceBrush(result.Source);
            _marker.Stroke = brush;
            _marker.Data = GeometryFor(result.SnapType);
            _markerTransform.X = point.X;
            _markerTransform.Y = point.Y;
            _marker.Visibility = Visibility.Visible;

            _dot.Fill = brush;
            Canvas.SetLeft(_dot, point.X - 1.5);
            Canvas.SetTop(_dot, point.Y - 1.5);
            _dot.Visibility = Visibility.Visible;

            _label.Text = SnapLabel(result.SnapType);
            _label.Foreground = brush;
            Canvas.SetLeft(_label, point.X + MarkerHalfSize + 4);
            Canvas.SetTop(_label, point.Y - 7);
            _label.Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            _marker.Visibility = Visibility.Collapsed;
            _dot.Visibility = Visibility.Collapsed;
            _label.Visibility = Visibility.Collapsed;
        }

        private static Geometry GeometryFor(OsnapType type) => type switch
        {
            OsnapType.Endpoint => EndpointGeometry,
            OsnapType.Midpoint => MidpointGeometry,
            OsnapType.Intersection => IntersectionGeometry,
            OsnapType.Perpendicular => PerpendicularGeometry,
            OsnapType.Nearest => DiamondGeometry,
            OsnapType.Grid => GridGeometry,
            OsnapType.Center => CenterGeometry,
            OsnapType.Quadrant => DiamondGeometry,
            _ => EndpointGeometry
        };

        private static Brush SourceBrush(OsnapSource source) => source switch
        {
            OsnapSource.Zone => ZoneBrush,
            OsnapSource.Dxf => DxfBrush,
            OsnapSource.Ifc => IfcBrush,
            OsnapSource.Grid => GridBrush,
            _ => ZoneBrush
        };

        private static string SnapLabel(OsnapType type) => type switch
        {
            OsnapType.Endpoint => "END",
            OsnapType.Midpoint => "MID",
            OsnapType.Intersection => "INT",
            OsnapType.Perpendicular => "PER",
            OsnapType.Nearest => "NEA",
            OsnapType.Grid => "GRID",
            OsnapType.Center => "CEN",
            OsnapType.Quadrant => "QUA",
            _ => string.Empty
        };

        private static Brush CreateBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Geometry CreatePolygon(params Point[] points)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(points[0], false, true);
                for (int index = 1; index < points.Length; index++)
                    context.LineTo(points[index], true, false);
            }

            geometry.Freeze();
            return geometry;
        }

        private static Geometry CreateLines(params Point[] points)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                for (int index = 0; index + 1 < points.Length; index += 2)
                {
                    context.BeginFigure(points[index], false, false);
                    context.LineTo(points[index + 1], true, false);
                }
            }

            geometry.Freeze();
            return geometry;
        }

        private static Geometry CreateRightAngle()
        {
            const double tick = 4.0;
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(
                    new Point(-MarkerHalfSize, -MarkerHalfSize),
                    false,
                    false);
                context.LineTo(
                    new Point(-MarkerHalfSize, MarkerHalfSize),
                    true,
                    false);
                context.LineTo(
                    new Point(MarkerHalfSize, MarkerHalfSize),
                    true,
                    false);
                context.BeginFigure(
                    new Point(-MarkerHalfSize + tick, MarkerHalfSize - tick),
                    false,
                    false);
                context.LineTo(
                    new Point(-MarkerHalfSize + tick, MarkerHalfSize),
                    true,
                    false);
                context.LineTo(
                    new Point(-MarkerHalfSize + tick * 2, MarkerHalfSize),
                    true,
                    false);
            }

            geometry.Freeze();
            return geometry;
        }

        private static T Freeze<T>(T freezable)
            where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }
}
