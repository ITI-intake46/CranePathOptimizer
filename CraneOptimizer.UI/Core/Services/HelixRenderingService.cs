using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Rendering;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Core.Services
{
    public class HelixRenderingService : IRenderingService
    {
        // =========================
        // Fields
        // =========================
        private HelixViewport3D? _viewport;

        private readonly ElementVisualBuilder _elementBuilder;

        private readonly List<Visual3D> _elementVisuals = new();

        private readonly LinesVisual3D _previewLines = new LinesVisual3D
        {
            Color = Colors.Yellow,
            Thickness = 2
        };

        private SphereVisual3D? _bestPoint;

        // =========================
        // Constructor
        // =========================
        public HelixRenderingService(ElementVisualBuilder elementBuilder)
        {
            _elementBuilder = elementBuilder;
        }

        // =========================
        // Initialization
        // =========================
        public void Initialize(HelixViewport3D viewport)
        {
            if (_viewport != null) return;

            _viewport = viewport;
            _viewport.Children.Clear();

            _viewport.Children.Add(new SunLight());
            _viewport.Children.Add(new GridLinesVisual3D { Width = 200, Length = 200 });
            _viewport.Children.Add(new CoordinateSystemVisual3D());
            _viewport.Children.Add(_previewLines);
        }

        // =========================
        // Elements (Default render - Gray)
        // =========================
        public void RenderElements(IEnumerable<PrecastElement> elements)
        {
            EnsureInitialized();

            ClearElementVisuals();

            foreach (var element in elements)
            {
                var visual = _elementBuilder.Build(element, null); // بدون status
                _viewport!.Children.Add(visual);
                _elementVisuals.Add(visual);
            }

            _viewport!.ZoomExtents();
        }

        // =========================
        // Elements WITH Status (Colored)
        // =========================
        public void RenderElementsWithStatus(IEnumerable<(PrecastElement element, LiftStatus status)> data)
        {
            EnsureInitialized();

            ClearElementVisuals();

            foreach (var item in data)
            {
                var visual = _elementBuilder.Build(item.element, item.status);
                _viewport!.Children.Add(visual);
                _elementVisuals.Add(visual);
            }

            _viewport!.ZoomExtents();
        }

        // =========================
        // Zones (TEMP)
        // =========================
        public void RenderZone(GreenAreaZone zone)
        {
            EnsureInitialized();

            var visual = BuildZoneVisual(zone);
            _viewport!.Children.Add(visual);
        }

        // =========================
        // Crane (TODO)
        // =========================
        public void RenderCrane(Crane crane)
        {
            // TODO: CraneVisualBuilder
        }

        // =========================
        // GA Result
        // =========================
        public void RenderBestPosition(double x, double y)
        {
            EnsureInitialized();

            if (_bestPoint != null)
                _viewport!.Children.Remove(_bestPoint);

            _bestPoint = new SphereVisual3D
            {
                Center = new Point3D(x, y, 0),
                Radius = 3,
                Material = new DiffuseMaterial(new SolidColorBrush(Colors.Red))
            };

            _viewport!.Children.Add(_bestPoint);
        }

        // =========================
        // Preview
        // =========================
        public void UpdatePreview(List<DomainPoint3D> points)
        {
            EnsureInitialized();

            _previewLines.Points.Clear();

            for (int i = 0; i < points.Count - 1; i++)
            {
                _previewLines.Points.Add(new Point3D(points[i].X, points[i].Y, points[i].Z));
                _previewLines.Points.Add(new Point3D(points[i + 1].X, points[i + 1].Y, points[i + 1].Z));
            }
        }

        public void ClearPreview()
        {
            EnsureInitialized();
            _previewLines.Points.Clear();
        }

        // =========================
        // Scene Control
        // =========================
        public void ClearScene()
        {
            EnsureInitialized();

            _viewport!.Children.Clear();

            _viewport.Children.Add(new SunLight());
            _viewport.Children.Add(new GridLinesVisual3D { Width = 200, Length = 200 });
            _viewport.Children.Add(new CoordinateSystemVisual3D());
            _viewport.Children.Add(_previewLines);

            _elementVisuals.Clear();
            _bestPoint = null;
        }

        // =========================
        // Helpers
        // =========================
        private void ClearElementVisuals()
        {
            foreach (var v in _elementVisuals)
                _viewport!.Children.Remove(v);

            _elementVisuals.Clear();
        }

        private void EnsureInitialized()
        {
            if (_viewport == null)
                throw new InvalidOperationException("RenderingService not initialized");
        }

        // =========================
        // TEMP Zone Builder
        // =========================
        private static ModelVisual3D BuildZoneVisual(GreenAreaZone zone)
        {
            var builder = new HelixToolkit.Geometry.MeshBuilder(false, false);

            var vectors = zone.Vertices
                .Select(v => new System.Numerics.Vector3((float)v.X, (float)v.Y, (float)v.Z))
                .ToList();

            builder.AddPolygon(vectors);

            var helixMesh = builder.ToMesh();

            var geometry = new MeshGeometry3D
            {
                Positions = new Point3DCollection(
                    helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices)
            };

            var material = new DiffuseMaterial(
                new SolidColorBrush(Color.FromArgb(120, 0, 255, 0)));

            return new ModelVisual3D
            {
                Content = new GeometryModel3D
                {
                    Geometry = geometry,
                    Material = material,
                    BackMaterial = material
                }
            };
        }
    }
}