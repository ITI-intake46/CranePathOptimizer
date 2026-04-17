using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Rendering;
using WpfPoint = System.Windows.Media.Media3D.Point3D;
using DomainPoint = CraneOptimizer.Domain.Common.DomainPoint3D;
using CraneOptimizer.Domain.Entities;

namespace CraneOptimizer.UI.Core.Services
{
    /// <summary>
    /// Concrete Helix implementation of <see cref="IRenderingService"/>.
    /// 
    /// Responsibilities:
    /// - Own and manage the HelixViewport3D
    /// - Control what gets rendered and when
    /// - Delegate geometry creation to dedicated builders
    /// 
    /// IMPORTANT:
    /// This class MUST NOT create geometry directly.
    /// It only orchestrates rendering.
    /// </summary>
    public class HelixRenderingService : IRenderingService
    {
        // ─────────────────────────────────────────────────────────────
        // Fields
        // ─────────────────────────────────────────────────────────────

        private HelixViewport3D _viewport = null!;
        private SphereVisual3D? _bestPoint;

        /// <summary>
        /// Tracks IFC element visuals to allow clean re-rendering.
        /// </summary>
        private readonly List<Visual3D> _ifcVisuals = new();

        /// <summary>
        /// Temporary preview lines (used during drawing).
        /// </summary>
        private readonly LinesVisual3D _previewLines = new LinesVisual3D
        {
            Color = Colors.Yellow,
            Thickness = 2
        };

        // Builders (Geometry Layer)
        private readonly HelixElementBuilder _elementBuilder;

        // ─────────────────────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────────────────────

        public HelixRenderingService(HelixElementBuilder elementBuilder)
        {
            _elementBuilder = elementBuilder;
        }

        // ─────────────────────────────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Initializes the Helix viewport and adds default visuals.
        /// Must be called once.
        /// </summary>
        public void Initialize(HelixViewport3D viewport)
        {
            if (_viewport != null) return;

            _viewport = viewport;
            _viewport.Children.Clear();

            // Add default scene helpers
            _viewport.Children.Add(new SunLight());
            _viewport.Children.Add(new GridLinesVisual3D { Width = 200, Length = 200 });
            _viewport.Children.Add(new CoordinateSystemVisual3D());
            _viewport.Children.Add(_previewLines);
        }

        // ─────────────────────────────────────────────────────────────
        // Elements Rendering
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Renders IFC elements as bounding boxes.
        /// Delegates geometry creation to HelixElementBuilder.
        /// </summary>
        public void RenderElements(IEnumerable<PrecastElement> elements)
        {
            // 1. Remove previous visuals
            foreach (var visual in _ifcVisuals)
                _viewport.Children.Remove(visual);

            _ifcVisuals.Clear();

            // 2. Render new elements
            foreach (var element in elements)
            {
                var visual = _elementBuilder.BuildElement(element); // ✅ FIXED
                _viewport.Children.Add(visual);
                _ifcVisuals.Add(visual);
            }

            _viewport.ZoomExtents();
        }

        // ─────────────────────────────────────────────────────────────
        // Zones Rendering
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Renders a crane placement zone (polygon).
        /// NOTE: You should later extract this into HelixZoneBuilder.
        /// </summary>
        public void RenderZone(CranePlacementZone zone)
        {
            var visual = BuildZoneVisual(zone); // TODO: move to builder
            _viewport.Children.Add(visual);
        }

        // ─────────────────────────────────────────────────────────────
        // Best Position Visualization
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Renders the best crane position as a red sphere.
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

        // ─────────────────────────────────────────────────────────────
        // Drawing Preview
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Updates preview polyline during interactive drawing.
        /// </summary>
        public void UpdatePreview(List<DomainPoint> points)
        {
            _previewLines.Points.Clear();

            for (int i = 0; i < points.Count - 1; i++)
            {
                _previewLines.Points.Add(new WpfPoint(points[i].X, points[i].Y, points[i].Z));
                _previewLines.Points.Add(new WpfPoint(points[i + 1].X, points[i + 1].Y, points[i + 1].Z));
            }
        }

        /// <summary>
        /// Clears preview lines.
        /// </summary>
        public void ClearPreview()
        {
            _previewLines.Points.Clear();
        }

        // ─────────────────────────────────────────────────────────────
        // Temporary Zone Builder (to be extracted later)
        // ─────────────────────────────────────────────────────────────

        private static ModelVisual3D BuildZoneVisual(CranePlacementZone zone)
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
                    helixMesh.Positions.Select(p => new WpfPoint(p.X, p.Y, p.Z))),

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

        public void RenderCrane(Crane crane)
        {
            throw new NotImplementedException();
        }
    }
}