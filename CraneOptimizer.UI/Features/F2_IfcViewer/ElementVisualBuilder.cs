using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Geometry;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F2_IfcViewer
{
    /// <summary>
    /// Builds visual representation of IFC elements (no viewport logic).
    /// </summary>
    public class ElementVisualBuilder : IVisualBuilder<(PrecastElement element, LiftStatus? status)>
    {
        private readonly DomainPoint3D? _origin;

        public ElementVisualBuilder(DomainPoint3D? origin = null)
        {
            _origin = origin;
        }

        public Visual3D Build((PrecastElement element, LiftStatus? status) item)
        {
            return BuildInternal(item.element, item.status);
        }

        private ModelVisual3D BuildInternal(PrecastElement element, LiftStatus? status)
        {
            var meshBuilder = new MeshBuilder(true, true);
            var origin = _origin;
            var x = element.ElementCOG.X - (origin?.X ?? 0d);
            var y = element.ElementCOG.Y - (origin?.Y ?? 0d);
            var z = element.ElementCOG.Z - (origin?.Z ?? 0d);

            meshBuilder.AddBox(
                new Vector3(
                    (float)x,
                    (float)y,
                    (float)z),

                (float)(element.BBoxLength ?? 1),
                (float)(element.BBoxWidth ?? 1),
                (float)(element.BBoxHeight ?? 1)
            );

            var helixMesh = meshBuilder.ToMesh();

            var mesh = new System.Windows.Media.Media3D.MeshGeometry3D
            {
                Positions = new Point3DCollection(
                    helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),

                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices),

                Normals = new Vector3DCollection(
                    helixMesh.Normals.Select(n => new Vector3D(n.X, n.Y, n.Z)))
            };

            var color = GetColor(status);

            var material = new DiffuseMaterial(
                new SolidColorBrush(color));

            var model = new GeometryModel3D
            {
                Geometry = mesh,
                Material = material,
                BackMaterial = material
            };

            return new ModelVisual3D { Content = model };
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
