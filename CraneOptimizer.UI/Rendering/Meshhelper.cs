using System;
using System.Windows.Media.Media3D;

namespace WPF_IFC_test1.Utilities
{
    /// <summary>
    /// Pure WPF geometry helpers — replaces HelixToolkit MeshBuilder entirely.
    /// No HelixToolkit dependency required.
    /// </summary>
    public static class MeshHelper
    {
        // ─────────────────────────────────────────────────────────────────────
        //  BOX
        // ─────────────────────────────────────────────────────────────────────
        public static MeshGeometry3D CreateBox(Point3D center, double sizeX, double sizeY, double sizeZ)
        {
            double x = sizeX / 2, y = sizeY / 2, z = sizeZ / 2;
            var mesh = new MeshGeometry3D();

            mesh.Positions.Add(new Point3D(center.X - x, center.Y - y, center.Z - z)); // 0
            mesh.Positions.Add(new Point3D(center.X + x, center.Y - y, center.Z - z)); // 1
            mesh.Positions.Add(new Point3D(center.X + x, center.Y + y, center.Z - z)); // 2
            mesh.Positions.Add(new Point3D(center.X - x, center.Y + y, center.Z - z)); // 3
            mesh.Positions.Add(new Point3D(center.X - x, center.Y - y, center.Z + z)); // 4
            mesh.Positions.Add(new Point3D(center.X + x, center.Y - y, center.Z + z)); // 5
            mesh.Positions.Add(new Point3D(center.X + x, center.Y + y, center.Z + z)); // 6
            mesh.Positions.Add(new Point3D(center.X - x, center.Y + y, center.Z + z)); // 7

            int[] faces = {
                0,2,1, 0,3,2,   // bottom
                4,5,6, 4,6,7,   // top
                0,1,5, 0,5,4,   // front
                2,3,7, 2,7,6,   // back
                3,0,4, 3,4,7,   // left
                1,2,6, 1,6,5    // right
            };
            foreach (var i in faces) mesh.TriangleIndices.Add(i);
            mesh.Freeze();
            return mesh;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CYLINDER  (arbitrary axis)
        // ─────────────────────────────────────────────────────────────────────
        public static MeshGeometry3D CreateCylinder(Point3D start, Point3D end, double radius, int thetaDiv)
        {
            var mesh = new MeshGeometry3D();
            Vector3D axis = end - start;
            if (axis.Length < 1e-10) return mesh;
            axis.Normalize();

            // Build orthonormal basis perpendicular to axis
            Vector3D perp = Math.Abs(axis.Z) < 0.9
                ? Vector3D.CrossProduct(axis, new Vector3D(0, 0, 1))
                : Vector3D.CrossProduct(axis, new Vector3D(1, 0, 0));
            perp.Normalize();
            Vector3D perp2 = Vector3D.CrossProduct(axis, perp);

            // Ring points at start and end
            for (int i = 0; i < thetaDiv; i++)
            {
                double a = 2 * Math.PI * i / thetaDiv;
                var off = radius * (Math.Cos(a) * perp + Math.Sin(a) * perp2);
                mesh.Positions.Add(start + off); // 0..T-1
            }
            for (int i = 0; i < thetaDiv; i++)
            {
                double a = 2 * Math.PI * i / thetaDiv;
                var off = radius * (Math.Cos(a) * perp + Math.Sin(a) * perp2);
                mesh.Positions.Add(end + off);   // T..2T-1
            }
            mesh.Positions.Add(start);           // 2T   (start cap center)
            mesh.Positions.Add(end);             // 2T+1 (end cap center)

            int T = thetaDiv;
            for (int i = 0; i < T; i++)
            {
                int n = (i + 1) % T;
                // Side
                mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(T + i); mesh.TriangleIndices.Add(T + n);
                mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(T + n); mesh.TriangleIndices.Add(n);
                // Start cap
                mesh.TriangleIndices.Add(2 * T); mesh.TriangleIndices.Add(n); mesh.TriangleIndices.Add(i);
                // End cap
                mesh.TriangleIndices.Add(2 * T + 1); mesh.TriangleIndices.Add(T + i); mesh.TriangleIndices.Add(T + n);
            }

            mesh.Freeze();
            return mesh;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SPHERE
        // ─────────────────────────────────────────────────────────────────────
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
                    int i1 = i0 + 1;
                    int i2 = i0 + thetaDiv + 1;
                    int i3 = i2 + 1;
                    mesh.TriangleIndices.Add(i0); mesh.TriangleIndices.Add(i2); mesh.TriangleIndices.Add(i1);
                    mesh.TriangleIndices.Add(i1); mesh.TriangleIndices.Add(i2); mesh.TriangleIndices.Add(i3);
                }
            }

            mesh.Freeze();
            return mesh;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PIPE  (hollow cylinder / torus ring)
        // ─────────────────────────────────────────────────────────────────────
        public static MeshGeometry3D CreatePipe(Point3D start, Point3D end,
            double innerRadius, double outerRadius, int thetaDiv)
        {
            var mesh = new MeshGeometry3D();
            Vector3D axis = end - start;
            if (axis.Length < 1e-10) return mesh;
            axis.Normalize();

            Vector3D perp = Math.Abs(axis.Z) < 0.9
                ? Vector3D.CrossProduct(axis, new Vector3D(0, 0, 1))
                : Vector3D.CrossProduct(axis, new Vector3D(1, 0, 0));
            perp.Normalize();
            Vector3D perp2 = Vector3D.CrossProduct(axis, perp);

            int T = thetaDiv;

            // Positions: outerStart[0..T), outerEnd[T..2T), innerStart[2T..3T), innerEnd[3T..4T)
            for (int i = 0; i < T; i++)
            {
                double a = 2 * Math.PI * i / T;
                var oOff = outerRadius * (Math.Cos(a) * perp + Math.Sin(a) * perp2);
                var iOff = innerRadius * (Math.Cos(a) * perp + Math.Sin(a) * perp2);
                mesh.Positions.Add(start + oOff); // outerStart
                mesh.Positions.Add(end + oOff); // outerEnd   — interleaved offset below
                mesh.Positions.Add(start + iOff); // innerStart
                mesh.Positions.Add(end + iOff); // innerEnd
            }

            // Stride=4: [4i]=outerStart, [4i+1]=outerEnd, [4i+2]=innerStart, [4i+3]=innerEnd
            for (int i = 0; i < T; i++)
            {
                int n = (i + 1) % T;
                int os0 = 4 * i, oe0 = 4 * i + 1, is0 = 4 * i + 2, ie0 = 4 * i + 3;
                int os1 = 4 * n, oe1 = 4 * n + 1, is1 = 4 * n + 2, ie1 = 4 * n + 3;

                // Outer side
                mesh.TriangleIndices.Add(os0); mesh.TriangleIndices.Add(oe0); mesh.TriangleIndices.Add(oe1);
                mesh.TriangleIndices.Add(os0); mesh.TriangleIndices.Add(oe1); mesh.TriangleIndices.Add(os1);
                // Inner side (reversed winding)
                mesh.TriangleIndices.Add(is0); mesh.TriangleIndices.Add(is1); mesh.TriangleIndices.Add(ie1);
                mesh.TriangleIndices.Add(is0); mesh.TriangleIndices.Add(ie1); mesh.TriangleIndices.Add(ie0);
                // Start ring
                mesh.TriangleIndices.Add(os0); mesh.TriangleIndices.Add(os1); mesh.TriangleIndices.Add(is1);
                mesh.TriangleIndices.Add(os0); mesh.TriangleIndices.Add(is1); mesh.TriangleIndices.Add(is0);
                // End ring
                mesh.TriangleIndices.Add(oe0); mesh.TriangleIndices.Add(ie0); mesh.TriangleIndices.Add(ie1);
                mesh.TriangleIndices.Add(oe0); mesh.TriangleIndices.Add(ie1); mesh.TriangleIndices.Add(oe1);
            }

            mesh.Freeze();
            return mesh;
        }
    }
}