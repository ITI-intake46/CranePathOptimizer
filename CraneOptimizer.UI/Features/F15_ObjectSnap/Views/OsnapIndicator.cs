using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Views
{
    /// <summary>
    /// Draws AutoCAD-style Object Snap markers on a transparent Canvas overlay
    /// that sits above the HelixViewport3D.
    ///
    /// Each snap type gets a distinct geometric symbol (matching AutoCAD conventions):
    ///   Endpoint     → hollow square      (yellow)
    ///   Midpoint     → hollow triangle    (yellow)
    ///   Intersection → × cross            (red)
    ///   Perpendicular→ right-angle bracket (green)
    ///   Nearest      → hollow diamond     (orange)
    ///   Grid         → + plus sign        (white)
    ///
    /// The symbol colour also shifts by source:
    ///   Zone → gold,  DXF → orange,  IFC → cyan,  Grid → light grey
    /// </summary>
    public sealed class OsnapIndicator
    {
        // ─── Canvas children ─────────────────────────────────────────────────
        private readonly Canvas      _canvas;
        private readonly Path        _marker;
        private readonly TextBlock   _label;
        private readonly Ellipse     _dot;      // tiny centre dot for precision feedback

        // ─── Geometry constants ───────────────────────────────────────────────
        private const double S  = 9.0;          // half-size of marker (px)
        private const double SW = 1.6;          // stroke width

        // ─── Colours by source ────────────────────────────────────────────────
        private static readonly Color ZoneColor = Color.FromRgb(0xFF, 0xDD, 0x00);  // gold
        private static readonly Color DxfColor  = Color.FromRgb(0xFF, 0x88, 0x00);  // orange
        private static readonly Color IfcColor  = Color.FromRgb(0x00, 0xDD, 0xFF);  // cyan
        private static readonly Color GridColor = Color.FromRgb(0xCC, 0xCC, 0xCC);  // light grey

        // ─── Constructor ─────────────────────────────────────────────────────
        public OsnapIndicator(Canvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

            _dot = new Ellipse
            {
                Width  = 3,
                Height = 3,
                Fill   = Brushes.White,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            _marker = new Path
            {
                StrokeThickness  = SW,
                Fill             = Brushes.Transparent,
                IsHitTestVisible = false,
                Visibility       = Visibility.Collapsed,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap   = PenLineCap.Round,
                StrokeLineJoin     = PenLineJoin.Round,
            };

            _label = new TextBlock
            {
                FontFamily       = new FontFamily("Consolas, Courier New"),
                FontSize         = 9,
                FontWeight       = FontWeights.Bold,
                IsHitTestVisible = false,
                Visibility       = Visibility.Collapsed,
                // subtle background behind the label for readability
                Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0))
            };

            // Render order: dot → marker → label (label is on top)
            _canvas.Children.Add(_dot);
            _canvas.Children.Add(_marker);
            _canvas.Children.Add(_label);
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Show the snap marker at the given result's projected screen position.
        /// Call from the UI thread only.
        /// </summary>
        public void Update(OsnapResult? result)
        {
            if (result == null || !result.ScreenPoint.HasValue)
            {
                Hide();
                return;
            }

            var pt    = result.ScreenPoint.Value;
            var color = SourceColor(result.Source);
            var brush = new SolidColorBrush(color);

            // ── marker symbol ────────────────────────────────────────────────
            _marker.Stroke     = brush;
            _marker.Data       = BuildGeometry(result.SnapType, pt);
            _marker.Visibility = Visibility.Visible;

            // ── centre precision dot ─────────────────────────────────────────
            _dot.Fill = brush;
            Canvas.SetLeft(_dot, pt.X - 1.5);
            Canvas.SetTop (_dot, pt.Y - 1.5);
            _dot.Visibility = Visibility.Visible;

            // ── type label (END / MID / INT …) ───────────────────────────────
            _label.Text       = SnapLabel(result.SnapType);
            _label.Foreground = brush;
            Canvas.SetLeft(_label, pt.X + S + 4);
            Canvas.SetTop (_label, pt.Y - 7);
            _label.Visibility = Visibility.Visible;
        }

        /// <summary>Hides all overlay elements.</summary>
        public void Hide()
        {
            _marker.Visibility = Visibility.Collapsed;
            _dot.Visibility    = Visibility.Collapsed;
            _label.Visibility  = Visibility.Collapsed;
        }

        // ─── Geometry builders ────────────────────────────────────────────────

        private static Geometry BuildGeometry(OsnapType type, Point c)
        {
            return type switch
            {
                OsnapType.Endpoint      => BuildSquare(c),
                OsnapType.Midpoint      => BuildTriangle(c),
                OsnapType.Intersection  => BuildCross(c),
                OsnapType.Perpendicular => BuildRightAngle(c),
                OsnapType.Nearest       => BuildDiamond(c),
                OsnapType.Grid          => BuildPlus(c),
                OsnapType.Center        => BuildCircleMarker(c),
                OsnapType.Quadrant      => BuildDiamond(c),
                _                       => BuildSquare(c),
            };
        }

        /// <summary>Endpoint → hollow square, AutoCAD style.</summary>
        private static Geometry BuildSquare(Point c)
        {
            var r = new RectangleGeometry(new Rect(c.X - S, c.Y - S, S * 2, S * 2));
            r.Freeze();
            return r;
        }

        /// <summary>Midpoint → hollow equilateral triangle pointing up.</summary>
        private static Geometry BuildTriangle(Point c)
        {
            var sg = new StreamGeometry();
            using (var ctx = sg.Open())
            {
                ctx.BeginFigure(new Point(c.X,     c.Y - S),     false, true);
                ctx.LineTo     (new Point(c.X + S, c.Y + S),     true,  false);
                ctx.LineTo     (new Point(c.X - S, c.Y + S),     true,  false);
            }
            sg.Freeze();
            return sg;
        }

        /// <summary>Intersection → × (two diagonal lines).</summary>
        private static Geometry BuildCross(Point c)
        {
            var g = new GeometryGroup { FillRule = FillRule.Nonzero };
            g.Children.Add(new LineGeometry(new Point(c.X - S, c.Y - S), new Point(c.X + S, c.Y + S)));
            g.Children.Add(new LineGeometry(new Point(c.X + S, c.Y - S), new Point(c.X - S, c.Y + S)));
            g.Freeze();
            return g;
        }

        /// <summary>Perpendicular → L-bracket with a small corner tick, AutoCAD style.</summary>
        private static Geometry BuildRightAngle(Point c)
        {
            // Outer L: vertical then horizontal
            double x0 = c.X - S, y0 = c.Y - S;
            double x1 = c.X - S, y1 = c.Y + S;
            double x2 = c.X + S, y2 = c.Y + S;
            double tick = 4.0;   // inner corner tick length

            var sg = new StreamGeometry();
            using (var ctx = sg.Open())
            {
                // Outer L
                ctx.BeginFigure(new Point(x0, y0), false, false);
                ctx.LineTo     (new Point(x1, y1), true,  false);
                ctx.LineTo     (new Point(x2, y2), true,  false);

                // Inner corner tick
                ctx.BeginFigure(new Point(x1 + tick, y1 - tick),  false, false);
                ctx.LineTo     (new Point(x1 + tick, y1),         true,  false);
                ctx.LineTo     (new Point(x1 + tick * 2, y1),     true,  false);
            }
            sg.Freeze();
            return sg;
        }

        /// <summary>Nearest → hollow diamond.</summary>
        private static Geometry BuildDiamond(Point c)
        {
            var sg = new StreamGeometry();
            using (var ctx = sg.Open())
            {
                ctx.BeginFigure(new Point(c.X,     c.Y - S),   false, true);
                ctx.LineTo     (new Point(c.X + S, c.Y),       true,  false);
                ctx.LineTo     (new Point(c.X,     c.Y + S),   true,  false);
                ctx.LineTo     (new Point(c.X - S, c.Y),       true,  false);
            }
            sg.Freeze();
            return sg;
        }

        /// <summary>Grid → + (plus sign).</summary>
        private static Geometry BuildPlus(Point c)
        {
            var g = new GeometryGroup { FillRule = FillRule.Nonzero };
            g.Children.Add(new LineGeometry(new Point(c.X - S, c.Y), new Point(c.X + S, c.Y)));
            g.Children.Add(new LineGeometry(new Point(c.X, c.Y - S), new Point(c.X, c.Y + S)));
            g.Freeze();
            return g;
        }

        /// <summary>Center → hollow circle (AutoCAD style).</summary>
        private static Geometry BuildCircleMarker(Point c)
        {
            var r = new EllipseGeometry(c, S, S);
            r.Freeze();
            return r;
        }

        // ─── Helpers ─────────────────────────────────────────────────────────
        private static Color SourceColor(OsnapSource src) => src switch
        {
            OsnapSource.Zone => ZoneColor,
            OsnapSource.Dxf  => DxfColor,
            OsnapSource.Ifc  => IfcColor,
            OsnapSource.Grid => GridColor,
            _                => ZoneColor,
        };

        private static string SnapLabel(OsnapType type) => type switch
        {
            OsnapType.Endpoint      => "END",
            OsnapType.Midpoint      => "MID",
            OsnapType.Intersection  => "INT",
            OsnapType.Perpendicular => "PER",
            OsnapType.Nearest       => "NEA",
            OsnapType.Grid          => "GRID",
            OsnapType.Center        => "CEN",
            OsnapType.Quadrant      => "QUA",
            _                       => "",
        };
    }
}
