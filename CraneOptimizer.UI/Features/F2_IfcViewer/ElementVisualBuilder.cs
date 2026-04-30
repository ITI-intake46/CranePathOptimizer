using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using HelixToolkit.Geometry;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F2_IfcViewer
{
    /// <summary>
    /// Builds visual representation of IFC elements (no viewport logic).
    /// </summary>
    public class ElementVisualBuilder : IVisualBuilder<(PrecastElement element, LiftStatus? status)>
    {
        private static readonly Dictionary<string, System.Windows.Media.Media3D.MeshGeometry3D> MeshCache = new();
        private static readonly Dictionary<Color, Material> MaterialCache = new();

        private readonly DomainPoint3D? _origin;
        private readonly HashSet<IfcElementVisualRef> _selectedElements;

        public ElementVisualBuilder(
            DomainPoint3D? origin = null,
            IEnumerable<IfcElementVisualRef>? selectedElements = null)
        {
            _origin = origin;
            _selectedElements = selectedElements?.ToHashSet() ?? new HashSet<IfcElementVisualRef>();
        }

        public Visual3D Build((PrecastElement element, LiftStatus? status) item)
        {
            return BuildInternal(item.element, item.status);
        }

        private ModelVisual3D BuildInternal(PrecastElement element, LiftStatus? status)
        {
            var visualRef = new IfcElementVisualRef(element.ModelInstanceId, element.ElementID);
            var mesh = GetOrCreateMesh(element, visualRef);

            var color = GetColor(status);
            if (_selectedElements.Contains(visualRef))
                color = Colors.Yellow;

            var material = GetMaterial(color);

            var model = new GeometryModel3D
            {
                Geometry = mesh,
                Material = material,
                BackMaterial = material
            };

            if (model.CanFreeze)
                model.Freeze();

            var visual = new ModelVisual3D
            {
                Content = model,
                Transform = CreatePlacementTransform(element)
            };
            visual.SetValue(FrameworkElement.TagProperty, visualRef);
            return visual;
        }

        public Transform3D CreatePlacementTransform(PrecastElement element)
        {
            var origin = _origin;
            return new TranslateTransform3D(
                element.ElementCOG.X - element.OriginalElementCOG.X - (origin?.X ?? 0d),
                element.ElementCOG.Y - element.OriginalElementCOG.Y - (origin?.Y ?? 0d),
                element.ElementCOG.Z - element.OriginalElementCOG.Z - (origin?.Z ?? 0d));
        }

        private static Material GetMaterial(Color color)
        {
            if (MaterialCache.TryGetValue(color, out var cached))
                return cached;

            var brush = new SolidColorBrush(color);
            if (brush.CanFreeze)
                brush.Freeze();

            var material = new DiffuseMaterial(brush);
            if (material.CanFreeze)
                material.Freeze();

            MaterialCache[color] = material;
            return material;
        }

        private System.Windows.Media.Media3D.MeshGeometry3D GetOrCreateMesh(
            PrecastElement element,
            IfcElementVisualRef visualRef)
        {
            if (MeshCache.TryGetValue(visualRef.Key, out var cached))
                return cached;

            var mesh = element.Meshes.Count > 0
                ? BuildIfcMesh(element)
                : BuildFallbackBox(element);

            if (mesh.Normals.Count != mesh.Positions.Count)
                GenerateNormals(mesh);

            if (mesh.CanFreeze)
                mesh.Freeze();

            MeshCache[visualRef.Key] = mesh;
            return mesh;
        }

        private System.Windows.Media.Media3D.MeshGeometry3D BuildIfcMesh(PrecastElement element)
        {
            var mesh = new System.Windows.Media.Media3D.MeshGeometry3D();

            foreach (var sourceMesh in element.Meshes)
            {
                var indexOffset = mesh.Positions.Count;

                foreach (var position in sourceMesh.Positions)
                    mesh.Positions.Add(new Point3D(position.X, position.Y, position.Z));

                foreach (var normal in sourceMesh.Normals)
                    mesh.Normals.Add(new Vector3D(normal.X, normal.Y, normal.Z));

                foreach (var index in sourceMesh.TriangleIndices)
                    mesh.TriangleIndices.Add(indexOffset + index);
            }

            return mesh;
        }

        private System.Windows.Media.Media3D.MeshGeometry3D BuildFallbackBox(PrecastElement element)
        {
            var meshBuilder = new MeshBuilder(true, true);

            meshBuilder.AddBox(
                new Vector3(
                    (float)element.OriginalElementCOG.X,
                    (float)element.OriginalElementCOG.Y,
                    (float)element.OriginalElementCOG.Z),
                (float)(element.BBoxLength ?? 1),
                (float)(element.BBoxWidth ?? 1),
                (float)(element.BBoxHeight ?? 1));

            var helixMesh = meshBuilder.ToMesh();

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

            return mesh;
        }

        private static void GenerateNormals(System.Windows.Media.Media3D.MeshGeometry3D mesh)
        {
            var normals = new Vector3D[mesh.Positions.Count];

            for (int i = 0; i + 2 < mesh.TriangleIndices.Count; i += 3)
            {
                var i0 = mesh.TriangleIndices[i];
                var i1 = mesh.TriangleIndices[i + 1];
                var i2 = mesh.TriangleIndices[i + 2];

                if (i0 < 0 || i1 < 0 || i2 < 0 ||
                    i0 >= mesh.Positions.Count ||
                    i1 >= mesh.Positions.Count ||
                    i2 >= mesh.Positions.Count)
                {
                    continue;
                }

                var p0 = mesh.Positions[i0];
                var p1 = mesh.Positions[i1];
                var p2 = mesh.Positions[i2];

                var normal = Vector3D.CrossProduct(p1 - p0, p2 - p0);
                if (normal.LengthSquared <= 1e-12)
                    continue;

                normal.Normalize();
                normals[i0] += normal;
                normals[i1] += normal;
                normals[i2] += normal;
            }

            var normalCollection = new Vector3DCollection(mesh.Positions.Count);
            foreach (var normal in normals)
            {
                var normalized = normal;
                if (normalized.LengthSquared <= 1e-12)
                    normalized = new Vector3D(0, 0, 1);
                else
                    normalized.Normalize();

                normalCollection.Add(normalized);
            }

            mesh.Normals = normalCollection;
        }

        private Color GetColor(LiftStatus? status)
        {
            if (status == null)
                return Colors.Gray;

            return status switch
            {
                LiftStatus.Liftable => Colors.Green,

                LiftStatus.OutOfRadius => Colors.Red,
                LiftStatus.Overweight => Colors.Red,
                LiftStatus.HeightFail => Colors.Red,

                LiftStatus.SpecialCase => Colors.Yellow,

                _ => Colors.Gray
            };
        }
    }
}
