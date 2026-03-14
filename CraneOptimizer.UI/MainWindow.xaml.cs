using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Rendering;
using HelixToolkit.Geometry;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CrC = CraneOptimizer.Domain.Common;

namespace CraneOptimizer.UI
{
    public partial class MainWindow : Window
    {
        private readonly IIfcReader _ifcReader;

        #region CreateGreenArea and save it to list
        private List<Point3D> polygonPoints = new();
        private LinesVisual3D previewLines = new LinesVisual3D();
        private bool isDrawing = false;
        // save the zones in list to use it later in optimization process
        private List<CranePlacementZone> zones = new(); 
        #endregion
        public MainWindow(IIfcReader ifcReader)
        {
            InitializeComponent();

            _ifcReader = ifcReader;

            Loaded += MainWindow_Loaded;
            MainViewport.MouseDown += Viewport_MouseDown;
            MainViewport.MouseUp += Viewport_MouseUp;
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetupScene();
            MainViewport.Children.Add(previewLines);

            var path = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "sample.ifc"
            );

            if (System.IO.File.Exists(path))
            {
                var elements = _ifcReader.ReadElements(path);
                RenderElements(elements);
            }
        }

        #region DrawPolygon events
        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isDrawing) return;
            var pos = e.GetPosition(MainViewport);

            var point = GetGroundPoint(pos);

            if (point == null)
                return;

            if (e.RightButton == MouseButtonState.Pressed)
            {
                CreatePolygonSurface();
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                polygonPoints.Add(point.Value);
                UpdatePreview();
            }
        }
        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDrawing) return;

            var pos = e.GetPosition(MainViewport);
            var point = GetGroundPoint(pos);
            if (point == null) return;

            if (e.ChangedButton == MouseButton.Right && e.RightButton == MouseButtonState.Released)
            {
                CreatePolygonSurface();
                e.Handled = true;           // مهم جدًا عشان تمنع الـ context menu
            }
        }
        private Point3D? GetGroundPoint(Point mouse)
        {
            var ray = Viewport3DHelper.Point2DtoRay3D(MainViewport.Viewport, mouse);

            var origin = ray.Origin;
            var dir = ray.Direction;

            if (Math.Abs(dir.Z) < 0.0001)
                return null;

            double t = -origin.Z / dir.Z;

            if (t < 0)
                return null;

            return new Point3D(
                origin.X + dir.X * t,
                origin.Y + dir.Y * t,
                0);
        }
        private void UpdatePreview()
        {
            previewLines.Points.Clear();

            for (int i = 0; i < polygonPoints.Count - 1; i++)
            {
                previewLines.Points.Add(polygonPoints[i]);
                previewLines.Points.Add(polygonPoints[i + 1]);
            }
        }
        private void CreatePolygonSurface()
        {
            if (polygonPoints.Count < 3)
                return;

            var builder = new MeshBuilder(false, false);

            // ✅ Error 1: حوّل Point3D → Vector3
            var vectors = polygonPoints
                .Select(p => new System.Numerics.Vector3((float)p.X, (float)p.Y, (float)p.Z))
                .ToList();

            builder.AddPolygon(vectors);

            // ✅ Error 2 & 3: ToMesh() من غير arguments، وحوّل الـ mesh يدوياً
            var helixMesh = builder.ToMesh();

            var mesh = new System.Windows.Media.Media3D.MeshGeometry3D
            {
                Positions = new Point3DCollection(
                    helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),

                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices)
            };

            if (helixMesh.Normals != null)
            {
                mesh.Normals = new Vector3DCollection(
                    helixMesh.Normals.Select(n => new Vector3D(n.X, n.Y, n.Z)));
            }

            var material = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(120, 0, 255, 0)));

            var model = new GeometryModel3D
            {
                Geometry = mesh,
                Material = material,
                BackMaterial = material
            };

            var visual = new ModelVisual3D { Content = model };
            MainViewport.Children.Add(visual);

            // save area to class here *************
            var zone = new CranePlacementZone
            {
                Vertices = polygonPoints
                    .Select(p => new CrC.Point3D(p.X, p.Y, p.Z))
                    .ToList()
            };
            zones.Add(zone);
            //////////////////////
            ///
            polygonPoints.Clear();
            previewLines.Points.Clear();
            isDrawing = false;
        }
        private void BtnDrawGreenArea_Click(object sender, RoutedEventArgs e)
        {
            isDrawing = true;
        }
        #endregion

        #region insert and create render for IFC

        private void BtnLoadIfc_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "IFC files (*.ifc)|*.ifc"
            };

            if (dialog.ShowDialog() == true)
            {
                var elements = _ifcReader.ReadElements(dialog.FileName);
                RenderElements(elements);
            }
        }

        private void SetupScene()
        {
            MainViewport.Children.Clear();

            // Light
            MainViewport.Children.Add(new SunLight());

            // Ground Grid
            var grid = new GridLinesVisual3D
            {
                Width = 200,
                Length = 200,
                MajorDistance = 10,
                MinorDistance = 2,
                Thickness = 0.1
            };

            MainViewport.Children.Add(grid);
            // Coordinate system
            var axis = new CoordinateSystemVisual3D
            {
                ArrowLengths = 5
            };

            MainViewport.Children.Add(axis);
        }

        private void RenderElements(IEnumerable<PrecastElement> elements)
        {
            SetupScene();

            var builder = new HelixElementBuilder();

            foreach (var element in elements)
            {
                var visual = builder.BuildElement(element);

                if (visual != null)
                {
                    MainViewport.Children.Add(visual);
                }
            }

            MainViewport.ZoomExtents();
        }
        #endregion

    }
}