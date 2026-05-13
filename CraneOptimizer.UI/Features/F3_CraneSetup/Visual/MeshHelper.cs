using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Visual
{
    internal static class MeshHelper
    {
        public static MeshGeometry3D CreateBox(Point3D center, double sizeX, double sizeY, double sizeZ)
        {
            double x = sizeX / 2.0, y = sizeY / 2.0, z = sizeZ / 2.0;
            var mesh = new MeshGeometry3D();

            mesh.Positions.Add(new Point3D(center.X - x, center.Y - y, center.Z - z));
            mesh.Positions.Add(new Point3D(center.X + x, center.Y - y, center.Z - z));
            mesh.Positions.Add(new Point3D(center.X + x, center.Y + y, center.Z - z));
            mesh.Positions.Add(new Point3D(center.X - x, center.Y + y, center.Z - z));
            mesh.Positions.Add(new Point3D(center.X - x, center.Y - y, center.Z + z));
            mesh.Positions.Add(new Point3D(center.X + x, center.Y - y, center.Z + z));
            mesh.Positions.Add(new Point3D(center.X + x, center.Y + y, center.Z + z));
            mesh.Positions.Add(new Point3D(center.X - x, center.Y + y, center.Z + z));

            int[] faces =
            {
                0,2,1, 0,3,2,
                4,5,6, 4,6,7,
                0,1,5, 0,5,4,
                2,3,7, 2,7,6,
                3,0,4, 3,4,7,
                1,2,6, 1,6,5
            };

            foreach (var i in faces)
                mesh.TriangleIndices.Add(i);

            mesh.Freeze();
            return mesh;
        }

        public static MeshGeometry3D CreateCylinder(Point3D start, Point3D end, double radius, int thetaDiv)
        {
            var mesh = new MeshGeometry3D();
            var axis = end - start;
            if (axis.Length < 1e-10)
                return mesh;

            axis.Normalize();
            var perp = Math.Abs(axis.Z) < 0.9
                ? Vector3D.CrossProduct(axis, new Vector3D(0, 0, 1))
                : Vector3D.CrossProduct(axis, new Vector3D(1, 0, 0));
            perp.Normalize();
            var perp2 = Vector3D.CrossProduct(axis, perp);

            // 1. Side vertices
            for (int i = 0; i < thetaDiv; i++)
            {
                double a = 2 * Math.PI * i / thetaDiv;
                var off = radius * (Math.Cos(a) * perp + Math.Sin(a) * perp2);
                mesh.Positions.Add(start + off); // index i
            }

            for (int i = 0; i < thetaDiv; i++)
            {
                double a = 2 * Math.PI * i / thetaDiv;
                var off = radius * (Math.Cos(a) * perp + Math.Sin(a) * perp2);
                mesh.Positions.Add(end + off); // index thetaDiv + i
            }

            int t = thetaDiv;
            // Side triangles
            for (int i = 0; i < t; i++)
            {
                int n = (i + 1) % t;
                mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(t + i); mesh.TriangleIndices.Add(t + n);
                mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(t + n); mesh.TriangleIndices.Add(n);
            }

            // 2. Bottom cap vertices (separate to prevent smooth shading across the 90-deg edge)
            int bottomStart = mesh.Positions.Count;
            for (int i = 0; i < thetaDiv; i++)
            {
                double a = 2 * Math.PI * i / thetaDiv;
                var off = radius * (Math.Cos(a) * perp + Math.Sin(a) * perp2);
                mesh.Positions.Add(start + off);
            }
            mesh.Positions.Add(start); // center
            int bottomCenter = mesh.Positions.Count - 1;

            for (int i = 0; i < t; i++)
            {
                int n = (i + 1) % t;
                mesh.TriangleIndices.Add(bottomCenter); mesh.TriangleIndices.Add(bottomStart + n); mesh.TriangleIndices.Add(bottomStart + i);
            }

            // 3. Top cap vertices
            int topStart = mesh.Positions.Count;
            for (int i = 0; i < thetaDiv; i++)
            {
                double a = 2 * Math.PI * i / thetaDiv;
                var off = radius * (Math.Cos(a) * perp + Math.Sin(a) * perp2);
                mesh.Positions.Add(end + off);
            }
            mesh.Positions.Add(end); // center
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
            for (int pi = 0; pi <= phiDiv; pi++)
            {
                double phi = Math.PI * pi / phiDiv;
                for (int ti = 0; ti <= thetaDiv; ti++)
                {
                    double theta = 2 * Math.PI * ti / thetaDiv;
                    mesh.Positions.Add(new Point3D(
                        center.X + radius * Math.Sin(phi) * Math.Cos(theta),
                        center.Y + radius * Math.Sin(phi) * Math.Sin(theta),
                        center.Z + radius * Math.Cos(phi)));
                }
            }

            for (int pi = 0; pi < phiDiv; pi++)
            {
                for (int ti = 0; ti < thetaDiv; ti++)
                {
                    int i0 = pi * (thetaDiv + 1) + ti;
                    mesh.TriangleIndices.Add(i0); mesh.TriangleIndices.Add(i0 + thetaDiv + 1); mesh.TriangleIndices.Add(i0 + 1);
                    mesh.TriangleIndices.Add(i0 + 1); mesh.TriangleIndices.Add(i0 + thetaDiv + 1); mesh.TriangleIndices.Add(i0 + thetaDiv + 2);
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
            if (axis.Length < 1e-10)
                return mesh;

            axis.Normalize();
            var perp = Math.Abs(axis.Z) < 0.9
                ? Vector3D.CrossProduct(axis, new Vector3D(0, 0, 1))
                : Vector3D.CrossProduct(axis, new Vector3D(1, 0, 0));
            perp.Normalize();
            var perp2 = Vector3D.CrossProduct(axis, perp);
            int t = thetaDiv;

            for (int i = 0; i < t; i++)
            {
                double a = 2 * Math.PI * i / t;
                var oOff = outerRadius * (Math.Cos(a) * perp + Math.Sin(a) * perp2);
                var iOff = innerRadius * (Math.Cos(a) * perp + Math.Sin(a) * perp2);
                mesh.Positions.Add(start + oOff);
                mesh.Positions.Add(end + oOff);
                mesh.Positions.Add(start + iOff);
                mesh.Positions.Add(end + iOff);
            }

            for (int i = 0; i < t; i++)
            {
                int n = (i + 1) % t;
                int os0 = 4 * i, oe0 = 4 * i + 1, is0 = 4 * i + 2, ie0 = 4 * i + 3;
                int os1 = 4 * n, oe1 = 4 * n + 1, is1 = 4 * n + 2, ie1 = 4 * n + 3;
                mesh.TriangleIndices.Add(os0); mesh.TriangleIndices.Add(oe0); mesh.TriangleIndices.Add(oe1);
                mesh.TriangleIndices.Add(os0); mesh.TriangleIndices.Add(oe1); mesh.TriangleIndices.Add(os1);
                mesh.TriangleIndices.Add(is0); mesh.TriangleIndices.Add(is1); mesh.TriangleIndices.Add(ie1);
                mesh.TriangleIndices.Add(is0); mesh.TriangleIndices.Add(ie1); mesh.TriangleIndices.Add(ie0);
                mesh.TriangleIndices.Add(os0); mesh.TriangleIndices.Add(os1); mesh.TriangleIndices.Add(is1);
                mesh.TriangleIndices.Add(os0); mesh.TriangleIndices.Add(is1); mesh.TriangleIndices.Add(is0);
                mesh.TriangleIndices.Add(oe0); mesh.TriangleIndices.Add(ie0); mesh.TriangleIndices.Add(ie1);
                mesh.TriangleIndices.Add(oe0); mesh.TriangleIndices.Add(ie1); mesh.TriangleIndices.Add(oe1);
            }

            mesh.Freeze();
            return mesh;
        }
    }
}
