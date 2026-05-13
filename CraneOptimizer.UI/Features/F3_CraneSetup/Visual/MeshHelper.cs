using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Visual
{
    internal static class MeshHelper
    {
        private const int MinimumRadialSegments = 24;
        private const double DegenerateLength = 1e-10;

        public static MeshGeometry3D CreateBox(Point3D center, double sizeX, double sizeY, double sizeZ)
        {
            double x = sizeX / 2.0, y = sizeY / 2.0, z = sizeZ / 2.0;
            var mesh = new MeshGeometry3D();

            var p000 = new Point3D(center.X - x, center.Y - y, center.Z - z);
            var p100 = new Point3D(center.X + x, center.Y - y, center.Z - z);
            var p110 = new Point3D(center.X + x, center.Y + y, center.Z - z);
            var p010 = new Point3D(center.X - x, center.Y + y, center.Z - z);
            var p001 = new Point3D(center.X - x, center.Y - y, center.Z + z);
            var p101 = new Point3D(center.X + x, center.Y - y, center.Z + z);
            var p111 = new Point3D(center.X + x, center.Y + y, center.Z + z);
            var p011 = new Point3D(center.X - x, center.Y + y, center.Z + z);

            // Boxes need hard edges, so every face owns its four vertices and a flat normal.
            AddQuad(mesh, new Vector3D(0, 0, -1), p000, p100, p110, p010, reverseWinding: true);
            AddQuad(mesh, new Vector3D(0, 0, 1), p001, p101, p111, p011);
            AddQuad(mesh, new Vector3D(0, -1, 0), p000, p100, p101, p001);
            AddQuad(mesh, new Vector3D(0, 1, 0), p110, p010, p011, p111);
            AddQuad(mesh, new Vector3D(-1, 0, 0), p010, p000, p001, p011);
            AddQuad(mesh, new Vector3D(1, 0, 0), p100, p110, p111, p101);

            mesh.Freeze();
            return mesh;
        }

        public static MeshGeometry3D CreateCylinder(Point3D start, Point3D end, double radius, int thetaDiv)
        {
            var mesh = new MeshGeometry3D();
            var axis = end - start;
            if (axis.Length < DegenerateLength || radius <= 0)
                return mesh;

            axis.Normalize();
            CreateBasis(axis, out var perp, out var perp2);
            int t = NormalizeRadialSegments(thetaDiv);

            for (int i = 0; i < t; i++)
            {
                var radial = GetRadialVector(i, t, perp, perp2);
                var off = radius * radial;
                mesh.Positions.Add(start + off);
                mesh.Normals.Add(radial);
            }

            for (int i = 0; i < t; i++)
            {
                var radial = GetRadialVector(i, t, perp, perp2);
                var off = radius * radial;
                mesh.Positions.Add(end + off);
                mesh.Normals.Add(radial);
            }

            for (int i = 0; i < t; i++)
            {
                int n = (i + 1) % t;
                mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(t + i); mesh.TriangleIndices.Add(t + n);
                mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(t + n); mesh.TriangleIndices.Add(n);
            }

            int bottomStart = mesh.Positions.Count;
            for (int i = 0; i < t; i++)
            {
                var off = radius * GetRadialVector(i, t, perp, perp2);
                mesh.Positions.Add(start + off);
                mesh.Normals.Add(-axis);
            }
            mesh.Positions.Add(start);
            mesh.Normals.Add(-axis);
            int bottomCenter = mesh.Positions.Count - 1;

            for (int i = 0; i < t; i++)
            {
                int n = (i + 1) % t;
                mesh.TriangleIndices.Add(bottomCenter); mesh.TriangleIndices.Add(bottomStart + n); mesh.TriangleIndices.Add(bottomStart + i);
            }

            int topStart = mesh.Positions.Count;
            for (int i = 0; i < t; i++)
            {
                var off = radius * GetRadialVector(i, t, perp, perp2);
                mesh.Positions.Add(end + off);
                mesh.Normals.Add(axis);
            }
            mesh.Positions.Add(end);
            mesh.Normals.Add(axis);
            int topCenter = mesh.Positions.Count - 1;

            for (int i = 0; i < t; i++)
            {
                int n = (i + 1) % t;
                mesh.TriangleIndices.Add(topCenter); mesh.TriangleIndices.Add(topStart + i); mesh.TriangleIndices.Add(topStart + n);
            }

            mesh.Freeze();
            return mesh;
        }

        public static MeshGeometry3D CreateSphere(Point3D center, double radius, int thetaDiv, int phiDiv)
        {
            var mesh = new MeshGeometry3D();
            if (radius <= 0)
                return mesh;

            int t = NormalizeRadialSegments(thetaDiv);
            int p = Math.Max(phiDiv, t / 2);

            for (int pi = 0; pi <= p; pi++)
            {
                double phi = Math.PI * pi / p;
                for (int ti = 0; ti <= t; ti++)
                {
                    double theta = 2 * Math.PI * ti / t;
                    var normal = new Vector3D(
                        Math.Sin(phi) * Math.Cos(theta),
                        Math.Sin(phi) * Math.Sin(theta),
                        Math.Cos(phi));

                    mesh.Positions.Add(new Point3D(
                        center.X + radius * normal.X,
                        center.Y + radius * normal.Y,
                        center.Z + radius * normal.Z));
                    mesh.Normals.Add(normal);
                }
            }

            for (int pi = 0; pi < p; pi++)
            {
                for (int ti = 0; ti < t; ti++)
                {
                    int i0 = pi * (t + 1) + ti;
                    mesh.TriangleIndices.Add(i0); mesh.TriangleIndices.Add(i0 + t + 1); mesh.TriangleIndices.Add(i0 + 1);
                    mesh.TriangleIndices.Add(i0 + 1); mesh.TriangleIndices.Add(i0 + t + 1); mesh.TriangleIndices.Add(i0 + t + 2);
                }
            }

            mesh.Freeze();
            return mesh;
        }

        public static MeshGeometry3D CreatePipe(
            Point3D start, Point3D end,
            double innerRadius, double outerRadius, int thetaDiv)
        {
            var mesh = new MeshGeometry3D();
            var axis = end - start;
            if (axis.Length < DegenerateLength || outerRadius <= 0 || innerRadius < 0 || innerRadius >= outerRadius)
                return mesh;

            axis.Normalize();
            CreateBasis(axis, out var perp, out var perp2);
            int t = NormalizeRadialSegments(thetaDiv);

            int outerSideStart = mesh.Positions.Count;
            for (int i = 0; i < t; i++)
            {
                var radial = GetRadialVector(i, t, perp, perp2);
                var oOff = outerRadius * radial;
                mesh.Positions.Add(start + oOff);
                mesh.Normals.Add(radial);
                mesh.Positions.Add(end + oOff);
                mesh.Normals.Add(radial);
            }

            for (int i = 0; i < t; i++)
            {
                int n = (i + 1) % t;
                int os0 = outerSideStart + 2 * i;
                int oe0 = outerSideStart + 2 * i + 1;
                int os1 = outerSideStart + 2 * n;
                int oe1 = outerSideStart + 2 * n + 1;
                mesh.TriangleIndices.Add(os0); mesh.TriangleIndices.Add(oe0); mesh.TriangleIndices.Add(oe1);
                mesh.TriangleIndices.Add(os0); mesh.TriangleIndices.Add(oe1); mesh.TriangleIndices.Add(os1);
            }

            int innerSideStart = mesh.Positions.Count;
            for (int i = 0; i < t; i++)
            {
                var radial = GetRadialVector(i, t, perp, perp2);
                var iOff = innerRadius * radial;
                mesh.Positions.Add(start + iOff);
                mesh.Normals.Add(-radial);
                mesh.Positions.Add(end + iOff);
                mesh.Normals.Add(-radial);
            }

            for (int i = 0; i < t; i++)
            {
                int n = (i + 1) % t;
                int is0 = innerSideStart + 2 * i;
                int ie0 = innerSideStart + 2 * i + 1;
                int is1 = innerSideStart + 2 * n;
                int ie1 = innerSideStart + 2 * n + 1;
                mesh.TriangleIndices.Add(is0); mesh.TriangleIndices.Add(is1); mesh.TriangleIndices.Add(ie1);
                mesh.TriangleIndices.Add(is0); mesh.TriangleIndices.Add(ie1); mesh.TriangleIndices.Add(ie0);
            }

            int startCapStart = mesh.Positions.Count;
            for (int i = 0; i < t; i++)
            {
                var radial = GetRadialVector(i, t, perp, perp2);
                mesh.Positions.Add(start + outerRadius * radial);
                mesh.Normals.Add(-axis);
                mesh.Positions.Add(start + innerRadius * radial);
                mesh.Normals.Add(-axis);
            }

            for (int i = 0; i < t; i++)
            {
                int n = (i + 1) % t;
                int os0 = startCapStart + 2 * i;
                int is0 = startCapStart + 2 * i + 1;
                int os1 = startCapStart + 2 * n;
                int is1 = startCapStart + 2 * n + 1;
                mesh.TriangleIndices.Add(os0); mesh.TriangleIndices.Add(os1); mesh.TriangleIndices.Add(is1);
                mesh.TriangleIndices.Add(os0); mesh.TriangleIndices.Add(is1); mesh.TriangleIndices.Add(is0);
            }

            int endCapStart = mesh.Positions.Count;
            for (int i = 0; i < t; i++)
            {
                var radial = GetRadialVector(i, t, perp, perp2);
                mesh.Positions.Add(end + outerRadius * radial);
                mesh.Normals.Add(axis);
                mesh.Positions.Add(end + innerRadius * radial);
                mesh.Normals.Add(axis);
            }

            for (int i = 0; i < t; i++)
            {
                int n = (i + 1) % t;
                int oe0 = endCapStart + 2 * i;
                int ie0 = endCapStart + 2 * i + 1;
                int oe1 = endCapStart + 2 * n;
                int ie1 = endCapStart + 2 * n + 1;
                mesh.TriangleIndices.Add(oe0); mesh.TriangleIndices.Add(ie0); mesh.TriangleIndices.Add(ie1);
                mesh.TriangleIndices.Add(oe0); mesh.TriangleIndices.Add(ie1); mesh.TriangleIndices.Add(oe1);
            }

            mesh.Freeze();
            return mesh;
        }

        private static void AddQuad(
            MeshGeometry3D mesh,
            Vector3D normal,
            Point3D p0,
            Point3D p1,
            Point3D p2,
            Point3D p3,
            bool reverseWinding = false)
        {
            int start = mesh.Positions.Count;
            mesh.Positions.Add(p0);
            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);
            mesh.Positions.Add(p3);

            for (int i = 0; i < 4; i++)
                mesh.Normals.Add(normal);

            if (reverseWinding)
            {
                mesh.TriangleIndices.Add(start);
                mesh.TriangleIndices.Add(start + 2);
                mesh.TriangleIndices.Add(start + 1);
                mesh.TriangleIndices.Add(start);
                mesh.TriangleIndices.Add(start + 3);
                mesh.TriangleIndices.Add(start + 2);
                return;
            }

            mesh.TriangleIndices.Add(start);
            mesh.TriangleIndices.Add(start + 1);
            mesh.TriangleIndices.Add(start + 2);
            mesh.TriangleIndices.Add(start);
            mesh.TriangleIndices.Add(start + 2);
            mesh.TriangleIndices.Add(start + 3);
        }

        private static int NormalizeRadialSegments(int requested)
            => Math.Max(MinimumRadialSegments, requested);

        private static void CreateBasis(Vector3D axis, out Vector3D perp, out Vector3D perp2)
        {
            perp = Math.Abs(axis.Z) < 0.9
                ? Vector3D.CrossProduct(axis, new Vector3D(0, 0, 1))
                : Vector3D.CrossProduct(axis, new Vector3D(1, 0, 0));
            perp.Normalize();

            perp2 = Vector3D.CrossProduct(axis, perp);
            perp2.Normalize();
        }

        private static Vector3D GetRadialVector(int index, int radialSegments, Vector3D perp, Vector3D perp2)
        {
            double angle = 2 * Math.PI * index / radialSegments;
            var radial = Math.Cos(angle) * perp + Math.Sin(angle) * perp2;
            radial.Normalize();
            return radial;
        }
    }
}
