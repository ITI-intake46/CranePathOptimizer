// CraneVisualBuilder.cs  (replaces the existing stub)
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Entities.Enums;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Geometry;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup
{
    public class CraneVisualBuilder : IVisualBuilder<Crane>
    {
        private readonly Action<ModelVisual3D>? _onBuilt;
        public CraneVisualBuilder() { }
        public CraneVisualBuilder(Action<ModelVisual3D> onBuilt)
        {
            _onBuilt = onBuilt;
        }
        public Visual3D Build(Crane crane)
        {
            // A ModelVisual3D is the WPF 3D container.
            // We build separate meshes for each part and combine them.
            var group = new Model3DGroup();
            var visual = new ModelVisual3D { Content = group, Transform = new TranslateTransform3D(0, 0, 0) };
            // ── Part 1: Vehicle Body ──────────────────────────────────────
            // Use the real dimensions from the database.
            // The body sits on Z=0, so its centre is at Z = VehicleHeight / 2
            double bodyX = crane.VehicleLength;   // length along X axis
            double bodyY = crane.VehicleWidth;    // width along Y axis
            double bodyZ = crane.VehicleHeight;   // height along Z axis

            var bodyMesh = BuildBox(
                centerX: 0,
                centerY: 0,
                centerZ: bodyZ / 2.0,   // sit on the ground
                sizeX: bodyX,
                sizeY: bodyY,
                sizeZ: bodyZ,
                color: Colors.DarkOrange
            );
            group.Children.Add(bodyMesh);

            // ── Part 2: Boom ──────────────────────────────────────────────
            // The boom starts at the top of the body and points upward.
            // We use MaxBoomLength from the first CraneSpecification.
            var spec = crane.Specifications.FirstOrDefault();
            if (spec != null)
            {
                double boomLength = spec.MaxBoomLength;
                double boomThickness = Math.Max(bodyY * 0.08, 0.3); // 8% of body width, min 0.3m

                // The boom is a thin vertical box on top of the body
                var boomMesh = BuildBox(
                    centerX: 0,
                    centerY: 0,
                    centerZ: bodyZ + (boomLength / 2.0),  // starts at top of body
                    sizeX: boomThickness,
                    sizeY: boomThickness,
                    sizeZ: boomLength,
                    color: Colors.Yellow
                );
                group.Children.Add(boomMesh);
            }

            // ── Part 3: Outriggers (Mobile/Crawler only) ──────────────────
            if (crane.OutriggerLength.HasValue && crane.OutriggerWidth.HasValue)
            {
                double oL = crane.OutriggerLength.Value;
                double oW = crane.OutriggerWidth.Value;
                double oH = bodyZ * 0.15;   // outrigger height = 15% of body height

                // Outriggers extend symmetrically from each side of the body
                double halfSpanX = oW / 2.0;
                double halfSpanY = oL / 2.0;

                // Four outrigger pads — one at each corner
                double[] xOffsets = { -halfSpanX, halfSpanX, -halfSpanX, halfSpanX };
                double[] yOffsets = { -halfSpanY, -halfSpanY, halfSpanY, halfSpanY };

                double padSize = Math.Max(oL * 0.15, 0.5); // 0.5m minimum pad

                for (int i = 0; i < 4; i++)
                {
                    var pad = BuildBox(
                        centerX: xOffsets[i],
                        centerY: yOffsets[i],
                        centerZ: oH / 2.0,
                        sizeX: padSize,
                        sizeY: padSize,
                        sizeZ: oH,
                        color: Colors.DarkGray
                    );
                    group.Children.Add(pad);
                }
                _onBuilt?.Invoke(visual);

                return visual;
            }



            // ── Wrap in ModelVisual3D ─────────────────────────────────────
            // ModelVisual3D is the object that actually gets added to the viewport.
            // We give it a TranslateTransform3D so dragging only updates the transform,
            // not the geometry (much faster).

            return visual;
        }

        // ─────────────────────────────────────────────────────────────────
        // PRIVATE HELPER: Builds one coloured box mesh
        // ─────────────────────────────────────────────────────────────────
        private GeometryModel3D BuildBox(
            double centerX, double centerY, double centerZ,
            double sizeX, double sizeY, double sizeZ,
            Color color)
        {
            // MeshBuilder is a HelixToolkit utility — it accumulates geometry
            // and produces a standard WPF MeshGeometry3D at the end.
            var mb = new MeshBuilder(true, true);

            mb.AddBox(
                new Vector3((float)centerX, (float)centerY, (float)centerZ),
                (float)sizeX,
                (float)sizeY,
                (float)sizeZ
            );

            var helixMesh = mb.ToMesh();

            // Convert from HelixToolkit's internal format to WPF's MeshGeometry3D
            var wpfMesh = new System.Windows.Media.Media3D.MeshGeometry3D
            {
                Positions = new Point3DCollection(helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices),
                Normals = new Vector3DCollection(helixMesh.Normals.Select(n => new Vector3D(n.X, n.Y, n.Z)))
            };

            var material = new DiffuseMaterial(new SolidColorBrush(color));

            return new GeometryModel3D
            {
                Geometry = wpfMesh,
                Material = material,
                BackMaterial = material   // visible from all angles
            };

        }
    }
}