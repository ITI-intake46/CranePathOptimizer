using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.Models;
using WPF_IFC_test1.Utilities;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace WPF_IFC_test1
{
    public partial class ElementDetailWindow : Window
    {
        private readonly IfcStore _model;
        private readonly IfcElementModel _element;
        private readonly string _unit;
        private readonly string _unitSystem;

        public ElementDetailWindow(
            IfcStore model,
            IfcElementModel element,
            string unit,
            string unitSystem)
        {
            InitializeComponent();
            _model = model;
            _element = element;
            _unit = unit ?? "m";
            _unitSystem = unitSystem ?? "Metric";

            TitleText.Text =
                $"Element Details — {element.DisplayName ?? element.Name ?? element.GlobalId}";

            LoadElementGeometry();
            ShowProperties();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GEOMETRY
        //
        //  FIX 1: IFC surface style colors are resolved from si.StyleLabel so
        //          geometry is rendered with its real IFC material color.
        //
        //  FIX 2: Wireframe now only draws BOUNDARY edges (edges that appear
        //          exactly once across all triangles). Internal diagonal edges
        //          that result from quad→triangle splitting are shared between
        //          two triangles and are suppressed.
        //
        //  xBIM face.Indices are ALREADY triangulated — groups of 3.
        //  Do NOT fan-triangulate on top of them.
        // ─────────────────────────────────────────────────────────────────────
        private void LoadElementGeometry()
        {
            try
            {
                if (_model == null) return;

                var context = new Xbim3DModelContext(_model);
                context.CreateContext();

                string targetGlobalId = _element.GlobalId;

                var instances = context.ShapeInstances()
                    .Where(s =>
                    {
                        var p = _model.Instances[s.IfcProductLabel] as IIfcProduct;
                        return p?.GlobalId.ToString() == targetGlobalId;
                    })
                    .ToList();

                if (instances.Count == 0)
                {
                    instances = context.ShapeInstances()
                        .Where(s => s.RepresentationType ==
                                    XbimGeometryRepresentationType.OpeningsAndAdditionsIncluded)
                        .Where(s =>
                        {
                            var p = _model.Instances[s.IfcProductLabel] as IIfcProduct;
                            return p?.GlobalId.ToString() == targetGlobalId;
                        })
                        .ToList();
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[ElementDetail] {instances.Count} instances for {targetGlobalId}");

                // Group geometry by IFC surface color so each material gets
                // its own GeometryModel3D (supports multi-material elements)
                var meshByColor = new Dictionary<Color,
                    (Point3DCollection pos, Vector3DCollection nrm, Int32Collection idx)>();

                // Edge occurrence counter — boundary edges appear exactly once
                var edgeCount = new Dictionary<string, int>();
                var edgePointMap = new Dictionary<string, (Point3D a, Point3D b)>();

                foreach (var si in instances)
                {
                    try
                    {
                        var shapeGeom = context.ShapeGeometry(si) as IXbimShapeGeometryData;
                        if (shapeGeom?.ShapeData == null || shapeGeom.ShapeData.Length == 0)
                            continue;

                        // ── RESOLVE IFC SURFACE COLOR ─────────────────────
                        Color meshColor = Color.FromRgb(180, 180, 185); // neutral fallback

                        if (si.StyleLabel > 0)
                        {
                            var surfaceStyle =
                                _model.Instances[si.StyleLabel] as IIfcSurfaceStyle;

                            if (surfaceStyle != null)
                            {
                                var rendering = surfaceStyle.Styles
                                    .OfType<IIfcSurfaceStyleRendering>()
                                    .FirstOrDefault();

                                if (rendering != null)
                                {
                                    var col = rendering.SurfaceColour;
                                    byte alpha = 255;
                                    if (rendering.Transparency.HasValue)
                                        alpha = (byte)((1.0 - (double)rendering.Transparency.Value) * 255);

                                    meshColor = Color.FromArgb(
                                        alpha,
                                        (byte)Math.Clamp(col.Red * 255, 0, 255),
                                        (byte)Math.Clamp(col.Green * 255, 0, 255),
                                        (byte)Math.Clamp(col.Blue * 255, 0, 255));
                                }
                            }
                        }

                        if (!meshByColor.ContainsKey(meshColor))
                            meshByColor[meshColor] = (
                                new Point3DCollection(),
                                new Vector3DCollection(),
                                new Int32Collection());

                        var (positions, normals, indices) = meshByColor[meshColor];

                        XbimMatrix3D matrix = si.Transformation;

                        using (var ms = new MemoryStream(shapeGeom.ShapeData))
                        using (var br = new BinaryReader(ms))
                        {
                            var xbimMesh = br.ReadShapeTriangulation();
                            if (xbimMesh == null) continue;

                            // Pre-transform all vertices into world space
                            var tv = xbimMesh.Vertices.Select(v =>
                            {
                                var wp = matrix.Transform(new XbimPoint3D(v.X, v.Y, v.Z));
                                return new Point3D(wp.X, wp.Y, wp.Z);
                            }).ToArray();

                            foreach (var face in xbimMesh.Faces)
                            {
                                // face.Indices are already triangulated — read 3 at a time
                                for (int i = 0; i < face.TriangleCount; i++)
                                {
                                    var p0 = tv[face.Indices[3 * i]];
                                    var p1 = tv[face.Indices[3 * i + 1]];
                                    var p2 = tv[face.Indices[3 * i + 2]];

                                    // Flat face normal
                                    var n = Vector3D.CrossProduct(p1 - p0, p2 - p0);
                                    n.Normalize();
                                    if (double.IsNaN(n.X) || double.IsNaN(n.Y) || double.IsNaN(n.Z))
                                        continue;

                                    // Each triangle owns its 3 verts (flat shading)
                                    int idx = positions.Count;
                                    positions.Add(p0); normals.Add(n);
                                    positions.Add(p1); normals.Add(n);
                                    positions.Add(p2); normals.Add(n);
                                    indices.Add(idx);
                                    indices.Add(idx + 1);
                                    indices.Add(idx + 2);

                                    // Count edge occurrences for boundary detection
                                    CountEdge(edgeCount, edgePointMap, p0, p1);
                                    CountEdge(edgeCount, edgePointMap, p1, p2);
                                    CountEdge(edgeCount, edgePointMap, p2, p0);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"  shape err: {ex.Message}");
                    }
                }

                bool anyGeometry = meshByColor.Any(kv => kv.Value.pos.Count > 0);

                if (anyGeometry)
                {
                    // ── SOLID MESHES — one per IFC color ──────────────────
                    foreach (var kv in meshByColor)
                    {
                        var (positions, normals, indices) = kv.Value;
                        if (positions.Count == 0) continue;

                        var mesh = new MeshGeometry3D
                        {
                            Positions = positions,
                            Normals = normals,
                            TriangleIndices = indices
                        };
                        mesh.Freeze();

                        var baseColor = kv.Key;

                        var frontMat = new MaterialGroup();
                        frontMat.Children.Add(MaterialHelper.CreateMaterial(baseColor));
                        frontMat.Children.Add(new SpecularMaterial(
                            new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 60));
                        frontMat.Freeze();

                        // Back face slightly darker than the IFC color
                        var backColor = Color.FromRgb(
                            (byte)Math.Max(0, baseColor.R - 60),
                            (byte)Math.Max(0, baseColor.G - 60),
                            (byte)Math.Max(0, baseColor.B - 60));
                        var backMat = MaterialHelper.CreateMaterial(backColor);

                        ElementViewer.Children.Add(new ModelVisual3D
                        {
                            Content = new GeometryModel3D
                            {
                                Geometry = mesh,
                                Material = frontMat,
                                BackMaterial = backMat
                            }
                        });
                    }

                    // ── BOUNDARY-ONLY WIREFRAME ────────────────────────────
                    // Edges shared by two triangles (count == 2) are internal
                    // diagonals from quad splitting — skip them.
                    // Only edges appearing exactly once are true boundaries.
                    var edgePoints = new Point3DCollection();
                    foreach (var kv in edgeCount)
                    {
                        if (kv.Value == 1)
                        {
                            var (a, b) = edgePointMap[kv.Key];
                            edgePoints.Add(a);
                            edgePoints.Add(b);
                        }
                    }

                    if (edgePoints.Count >= 2)
                    {
                        var wireframe = new LinesVisual3D
                        {
                            Points = edgePoints,
                            Color = Color.FromArgb(200, 0, 20, 50),
                            Thickness = 1.0
                        };
                        ElementViewer.Children.Add(wireframe);
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[ElementDetail] colors={meshByColor.Count} " +
                        $"boundary-edges={edgePoints.Count / 2}");
                }
                else
                {
                    // Placeholder box when no geometry is found
                    ElementViewer.Children.Add(new BoxVisual3D
                    {
                        Center = new Point3D(0, 0, 0),
                        Width = 1,
                        Height = 1,
                        Length = 1,
                        Fill = new SolidColorBrush(Color.FromRgb(80, 80, 90))
                    });
                    System.Diagnostics.Debug.WriteLine("[ElementDetail] no geometry — placeholder");
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { ElementViewer.ZoomExtents(400); } catch { }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ElementDetail] load failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EDGE HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a canonical, order-independent key for an edge between two points.
        /// </summary>
        private static string EdgeKey(Point3D a, Point3D b)
        {
            string ka = $"{a.X:F3},{a.Y:F3},{a.Z:F3}";
            string kb = $"{b.X:F3},{b.Y:F3},{b.Z:F3}";
            return string.CompareOrdinal(ka, kb) < 0
                ? $"{ka}|{kb}"
                : $"{kb}|{ka}";
        }

        /// <summary>
        /// Increments the occurrence count for an edge.
        /// Stores the point pair on first encounter.
        /// </summary>
        private static void CountEdge(
            Dictionary<string, int> edgeCount,
            Dictionary<string, (Point3D, Point3D)> edgePoints,
            Point3D a, Point3D b)
        {
            string key = EdgeKey(a, b);
            if (!edgeCount.ContainsKey(key))
            {
                edgeCount[key] = 0;
                edgePoints[key] = (a, b);
            }
            edgeCount[key]++;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PROPERTIES PANEL
        // ─────────────────────────────────────────────────────────────────────
        private void ShowProperties()
        {
            PropertiesPanel.Children.Clear();
            var el = _element;

            string hdrColor = "#2D2D30";
            string icon = "🔍";
            if (el.Weight.HasValue)
            {
                if (el.Weight.Value > 5000) { hdrColor = "#55E74C3C"; icon = "🔴"; }
                else if (el.Weight.Value > 3000) { hdrColor = "#55F39C12"; icon = "🟠"; }
            }

            var hdr = new Border
            {
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 5),
                Background = (SolidColorBrush)new BrushConverter().ConvertFromString(hdrColor)
            };
            hdr.Child = new TextBlock
            {
                Text = $"{icon} {el.DisplayName ?? el.Name ?? "Element"}",
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = Brushes.White
            };
            PropertiesPanel.Children.Add(hdr);

            void Add(string label, string value)
            {
                if (string.IsNullOrWhiteSpace(value) || value == "0" || value == "0.00") return;

                var row = new Border
                {
                    Padding = new Thickness(8, 6, 8, 6),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#3F3F46")
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });

                var lbl = new TextBlock
                {
                    Text = label,
                    Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString("#CCCCCC"),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var val = new TextBlock
                {
                    Text = value,
                    Foreground = Brushes.White,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Grid.SetColumn(lbl, 0);
                Grid.SetColumn(val, 1);
                grid.Children.Add(lbl);
                grid.Children.Add(val);
                row.Child = grid;
                PropertiesPanel.Children.Add(row);
            }

            Add("Global ID:", el.GlobalId);
            Add("Display Name:", el.DisplayName);
            Add("Type:", el.Type);
            Add("Mark:", el.Mark);

            if (el.Length.HasValue && el.Length.Value > 0)
                Add("Length:", $"{UnitConverter.ConvertLength(el.Length.Value, _unit):F2} {_unit}");
            if (el.Width.HasValue && el.Width.Value > 0)
                Add("Width:", $"{UnitConverter.ConvertLength(el.Width.Value, _unit):F2} {_unit}");
            if (el.Height.HasValue && el.Height.Value > 0)
                Add("Height:", $"{UnitConverter.ConvertLength(el.Height.Value, _unit):F2} {_unit}");

            if (el.Weight.HasValue && el.Weight.Value > 0)
            {
                string wLabel = el.Weight.Value > 5000 ? "⚠️ Weight (HEAVY):"
                              : el.Weight.Value > 3000 ? "⚠️ Weight (MEDIUM):"
                              : "Weight:";
                Add(wLabel, $"{el.DisplayWeight ?? el.Weight.Value:F2} {el.WeightUnit ?? "kg"}");
            }

            if (el.Volume.HasValue && el.Volume.Value > 0)
            {
                string vUnit = _unitSystem == "Imperial" ? "ft³" : "m³";
                double vol = _unitSystem == "Imperial"
                    ? UnitConverter.ConvertVolume(el.Volume.Value, "ft³")
                    : el.Volume.Value;
                Add("Volume:", $"{vol:F4} {vUnit}");
            }

            Add("Material:", el.Material);

            if (el.COG_X.HasValue && el.COG_Y.HasValue && el.COG_Z.HasValue)
            {
                var cogHdr = new Border
                {
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 6, 0, 2),
                    Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#2255AA")
                };
                cogHdr.Child = new TextBlock
                {
                    Text = "📍 Centre of Gravity",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    Foreground = Brushes.White
                };
                PropertiesPanel.Children.Add(cogHdr);

                Add("COG X:", $"{UnitConverter.ConvertLength(el.COG_X.Value, _unit):F3} {_unit}");
                Add("COG Y:", $"{UnitConverter.ConvertLength(el.COG_Y.Value, _unit):F3} {_unit}");
                Add("COG Z:", $"{UnitConverter.ConvertLength(el.COG_Z.Value, _unit):F3} {_unit}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  WINDOW CONTROLS
        // ─────────────────────────────────────────────────────────────────────
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

        private void ResetCamera_Click(object sender, RoutedEventArgs e)
        {
            try { ElementViewer.ZoomExtents(400); } catch { }
        }
    }
}