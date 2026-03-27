using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;
using HelixToolkit.Geometry;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WpfPoint = System.Windows.Media.Media3D.Point3D;
using DomainPoint = CraneOptimizer.Domain.Common.Point3D;
using CraneOptimizer.UI.Core.Interfaces;


namespace CraneOptimizer.UI.Core.Services
{
    /// <summary>
    /// Concrete Helix implementation of <see cref="IRenderingService"/>.
    /// Responsible for ALL 3-D rendering against a <see cref="HelixViewport3D"/>.
    /// Infrastructure-like concern that intentionally stays in the UI layer
    /// because it is purely visual and has no domain or application logic.
    /// </summary>
    public class HelixRenderingService : IRenderingService
    {
        // ── Fields ───────────────────────────────────────────────────────────

        private HelixViewport3D _viewport = null!;
        private SphereVisual3D? _bestPoint;
        /// <summary>Tracks IFC element visuals so they can be replaced cleanly.</summary>
        private readonly List<Visual3D> _ifcVisuals = new();
        private LinesVisual3D _previewLines = new LinesVisual3D
        {
            Color = Colors.Yellow,
            Thickness = 2
        };

        // ── IRenderingService ────────────────────────────────────────────────

        /// <inheritdoc/>
        public void Initialize(HelixViewport3D viewport)
        {
            if (_viewport != null) return;

            _viewport = viewport;
            _viewport.Children.Clear();

            // Default scene lights and helpers
            _viewport.Children.Add(new SunLight());
            _viewport.Children.Add(new GridLinesVisual3D { Width = 200, Length = 200 });
            _viewport.Children.Add(new CoordinateSystemVisual3D());
            _viewport.Children.Add(_previewLines);
        }

        /// <inheritdoc/>
        public void RenderElements(IEnumerable<PrecastElement> elements)
        {
            // ── 1. Clear previous IFC visuals ────────────────────────────────
            foreach (var visual in _ifcVisuals)
                _viewport.Children.Remove(visual);
            _ifcVisuals.Clear();

            // ── 2. Render each element as a bounding-box proxy ───────────────
            foreach (var element in elements)
            {
                var visual = BuildElementVisual(element);
                _viewport.Children.Add(visual);
                _ifcVisuals.Add(visual);
            }

            _viewport.ZoomExtents();
        }

        /// <inheritdoc/>
        public void RenderZone(CranePlacementZone zone)
        {
            var visual = BuildZoneVisual(zone);
            _viewport.Children.Add(visual);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Builds a grey box visual centred on the element's centre-of-gravity,
        /// sized by its bounding-box dimensions.
        /// </summary>

        public void RenderBestPosition(double x, double y)
        {
            if (_bestPoint != null)
                _viewport.Children.Remove(_bestPoint);

            _bestPoint = new SphereVisual3D
            {
                Center = new WpfPoint(x, y, 0),
                Radius = 3,
                Material = new DiffuseMaterial(
                    new SolidColorBrush(Colors.Red))
            };

            _viewport.Children.Add(_bestPoint);
        }
        private static ModelVisual3D BuildElementVisual(PrecastElement element)
        {
            var builder = new MeshBuilder(true, true);

            builder.AddBox(
                new System.Numerics.Vector3(
                    (float)element.ElementCOG.X,
                    (float)element.ElementCOG.Y,
                    (float)element.ElementCOG.Z),
                (float)(element.BBoxLength ?? 1.0),
                (float)(element.BBoxWidth ?? 1.0),
                (float)(element.BBoxHeight ?? 1.0));

            var geometry = ToWpfMesh(builder);
            var material = new DiffuseMaterial(new SolidColorBrush(Colors.Gray));

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

        /// <summary>
        /// Builds a semi-transparent green polygon visual from the zone's vertices.
        /// </summary>
        private static ModelVisual3D BuildZoneVisual(CranePlacementZone zone)
        {
            var builder = new MeshBuilder(false, false);

            // Convert DomainPoint vertices → Numerics.Vector3 for Helix
            var vectors = zone.Vertices
                .Select(v => new System.Numerics.Vector3((float)v.X, (float)v.Y, (float)v.Z))
                .ToList();

            builder.AddPolygon(vectors);

            var geometry = ToWpfMesh(builder);
            var material = new DiffuseMaterial(
                new SolidColorBrush(Color.FromArgb(120, 0, 255, 0)));

            return new ModelVisual3D
            {
                Content = new GeometryModel3D
                {
                    Geometry = geometry,
                    Material = material,
                    BackMaterial = material   // visible from below as well
                }
            };
        }

        /// <summary>
        /// Converts a <see cref="MeshBuilder"/> result into a WPF
        /// <see cref="MeshGeometry3D"/>, mapping Helix's Numerics.Vector3
        /// positions to <see cref="WpfPoint"/> explicitly to avoid ambiguity.
        /// </summary>
        private static System.Windows.Media.Media3D.MeshGeometry3D ToWpfMesh(MeshBuilder builder)
        {
            var helixMesh = builder.ToMesh();

            return new System.Windows.Media.Media3D.MeshGeometry3D
            {
                Positions = new Point3DCollection(
                    helixMesh.Positions.Select(p => new WpfPoint(p.X, p.Y, p.Z))),

                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices)
            };
        }
        public void UpdatePreview(List<DomainPoint> points)
        {
            _previewLines.Points.Clear();

            for (int i = 0; i < points.Count - 1; i++)
            {
                _previewLines.Points.Add(new WpfPoint(points[i].X, points[i].Y, points[i].Z));
                _previewLines.Points.Add(new WpfPoint(points[i + 1].X, points[i + 1].Y, points[i + 1].Z));
            }
        }

        public void ClearPreview()
        {
            _previewLines.Points.Clear();
        }
    }
}