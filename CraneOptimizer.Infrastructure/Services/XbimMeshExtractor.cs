using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using System.IO;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;

namespace CraneOptimizer.Infrastructure.Services
{
    public sealed class XbimMeshExtractor : IIfcMeshExtractor
    {
        public IfcElementMesh? Extract(
            byte[] shapeData,
            XbimMatrix3D transform,
            double lengthToMillimetersFactor,
            IfcMeshQuality quality = IfcMeshQuality.Smooth)
        {
            using var stream = new MemoryStream(shapeData);
            using var reader = new BinaryReader(stream);

            var triangulation = reader.ReadShapeTriangulation();
            if (triangulation?.Vertices == null || triangulation.Faces == null)
                return null;

            return quality switch
            {
                IfcMeshQuality.Ultra => CreateUltraMesh(triangulation, transform, lengthToMillimetersFactor),
                IfcMeshQuality.Flat => CreateFlatMesh(triangulation, transform, lengthToMillimetersFactor),
                _ => CreateSmoothMesh(triangulation, transform, lengthToMillimetersFactor)
            };
        }

        private static IfcElementMesh? CreateFlatMesh(dynamic triangulation, XbimMatrix3D transform, double scale)
        {
            var transformedVertices = TransformVertices(triangulation, transform, scale);
            var mesh = new IfcElementMesh();

            foreach (var face in triangulation.Faces)
            {
                for (int i = 0; i < face.TriangleCount; i++)
                {
                    var i0 = face.Indices[3 * i];
                    var i1 = face.Indices[3 * i + 1];
                    var i2 = face.Indices[3 * i + 2];

                    var p0 = transformedVertices[i0];
                    var p1 = transformedVertices[i1];
                    var p2 = transformedVertices[i2];
                    var normal = Normalize(Cross(Subtract(p1, p0), Subtract(p2, p0)));
                    if (IsInvalid(normal))
                        continue;

                    var outIndex = mesh.Positions.Count;
                    mesh.Positions.Add(p0);
                    mesh.Normals.Add(ToDomainVector(normal));
                    mesh.Positions.Add(p1);
                    mesh.Normals.Add(ToDomainVector(normal));
                    mesh.Positions.Add(p2);
                    mesh.Normals.Add(ToDomainVector(normal));
                    mesh.TriangleIndices.Add(outIndex);
                    mesh.TriangleIndices.Add(outIndex + 1);
                    mesh.TriangleIndices.Add(outIndex + 2);
                }
            }

            return mesh.Positions.Count == 0 ? null : mesh;
        }

        private static IfcElementMesh? CreateSmoothMesh(dynamic triangulation, XbimMatrix3D transform, double scale)
        {
            var transformedVertices = TransformVertices(triangulation, transform, scale);
            var triangles = new List<(int I0, int I1, int I2)>();
            var faceNormals = new List<Vector3>();

            foreach (var face in triangulation.Faces)
            {
                for (int i = 0; i < face.TriangleCount; i++)
                {
                    var i0 = face.Indices[3 * i];
                    var i1 = face.Indices[3 * i + 1];
                    var i2 = face.Indices[3 * i + 2];
                    var normal = Cross(
                        Subtract(transformedVertices[i1], transformedVertices[i0]),
                        Subtract(transformedVertices[i2], transformedVertices[i0]));

                    if (IsInvalid(normal) || LengthSquared(normal) < 1e-30)
                        continue;

                    triangles.Add((i0, i1, i2));
                    faceNormals.Add(normal);
                }
            }

            if (triangles.Count == 0)
                return null;

            var accumulated = new Vector3[transformedVertices.Length];
            for (int t = 0; t < triangles.Count; t++)
            {
                accumulated[triangles[t].I0] = Add(accumulated[triangles[t].I0], faceNormals[t]);
                accumulated[triangles[t].I1] = Add(accumulated[triangles[t].I1], faceNormals[t]);
                accumulated[triangles[t].I2] = Add(accumulated[triangles[t].I2], faceNormals[t]);
            }

            for (int i = 0; i < accumulated.Length; i++)
                accumulated[i] = Length(accumulated[i]) > 1e-10
                    ? Normalize(accumulated[i])
                    : new Vector3(0, 0, 1);

            var mesh = new IfcElementMesh();
            foreach (var triangle in triangles)
            {
                AddVertex(mesh, transformedVertices[triangle.I0], accumulated[triangle.I0]);
                AddVertex(mesh, transformedVertices[triangle.I1], accumulated[triangle.I1]);
                AddVertex(mesh, transformedVertices[triangle.I2], accumulated[triangle.I2]);
            }

            return mesh;
        }

        private static IfcElementMesh? CreateUltraMesh(
            dynamic triangulation,
            XbimMatrix3D transform,
            double scale,
            double creaseAngleDegrees = 80d)
        {
            var transformedVertices = TransformVertices(triangulation, transform, scale);
            var triangleCount = 0;
            foreach (var face in triangulation.Faces)
                triangleCount += face.TriangleCount;

            var triangles = new (int I0, int I1, int I2)[triangleCount];
            var triangleNormals = new Vector3[triangleCount];

            var t = 0;
            foreach (var face in triangulation.Faces)
            {
                for (int i = 0; i < face.TriangleCount; i++, t++)
                {
                    var i0 = face.Indices[3 * i];
                    var i1 = face.Indices[3 * i + 1];
                    var i2 = face.Indices[3 * i + 2];
                    triangles[t] = (i0, i1, i2);

                    var normal = Cross(
                        Subtract(transformedVertices[i1], transformedVertices[i0]),
                        Subtract(transformedVertices[i2], transformedVertices[i0]));

                    triangleNormals[t] = Length(normal) > 1e-30
                        ? Normalize(normal)
                        : new Vector3(0, 0, 1);
                }
            }

            var adjacentTriangles = new List<int>[transformedVertices.Length];
            for (int i = 0; i < adjacentTriangles.Length; i++)
                adjacentTriangles[i] = new List<int>(8);

            for (int i = 0; i < triangles.Length; i++)
            {
                adjacentTriangles[triangles[i].I0].Add(i);
                adjacentTriangles[triangles[i].I1].Add(i);
                adjacentTriangles[triangles[i].I2].Add(i);
            }

            var creaseAngle = creaseAngleDegrees * Math.PI / 180d;
            var mesh = new IfcElementMesh();

            for (int i = 0; i < triangles.Length; i++)
            {
                var (i0, i1, i2) = triangles[i];
                var faceNormal = triangleNormals[i];

                var a0 = AngleBetween(transformedVertices[i0], transformedVertices[i1], transformedVertices[i2]);
                var a1 = AngleBetween(transformedVertices[i1], transformedVertices[i0], transformedVertices[i2]);
                var a2 = AngleBetween(transformedVertices[i2], transformedVertices[i0], transformedVertices[i1]);

                if (double.IsNaN(a0) || double.IsNaN(a1) || double.IsNaN(a2))
                    continue;

                AddVertex(mesh, transformedVertices[i0], SmoothNormal(faceNormal, adjacentTriangles[i0], triangleNormals, creaseAngle, a0));
                AddVertex(mesh, transformedVertices[i1], SmoothNormal(faceNormal, adjacentTriangles[i1], triangleNormals, creaseAngle, a1));
                AddVertex(mesh, transformedVertices[i2], SmoothNormal(faceNormal, adjacentTriangles[i2], triangleNormals, creaseAngle, a2));
            }

            return mesh.Positions.Count == 0 ? null : mesh;
        }

        private static DomainPoint3D[] TransformVertices(dynamic triangulation, XbimMatrix3D transform, double scale)
        {
            var vertices = triangulation.Vertices;
            var transformed = new DomainPoint3D[vertices.Count];

            for (int i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];
                var world = transform.Transform(new XbimPoint3D(vertex.X, vertex.Y, vertex.Z));
                transformed[i] = new DomainPoint3D(
                    world.X * scale,
                    world.Y * scale,
                    world.Z * scale);
            }

            return transformed;
        }

        private static void AddVertex(IfcElementMesh mesh, DomainPoint3D position, Vector3 normal)
        {
            mesh.Positions.Add(position);
            mesh.Normals.Add(ToDomainVector(normal));
            mesh.TriangleIndices.Add(mesh.TriangleIndices.Count);
        }

        private static double AngleBetween(DomainPoint3D apex, DomainPoint3D a, DomainPoint3D b)
        {
            var u = Normalize(Subtract(a, apex));
            var v = Normalize(Subtract(b, apex));
            return Math.Acos(Math.Clamp(Dot(u, v), -1d, 1d));
        }

        private static Vector3 SmoothNormal(
            Vector3 faceNormal,
            List<int> adjacent,
            Vector3[] triangleNormals,
            double creaseAngle,
            double angle)
        {
            var accumulated = Multiply(faceNormal, angle);

            foreach (var adjacentIndex in adjacent)
            {
                var dot = Math.Clamp(Dot(faceNormal, triangleNormals[adjacentIndex]), -1d, 1d);
                if (Math.Acos(dot) > creaseAngle)
                    continue;

                accumulated = Add(accumulated, Multiply(triangleNormals[adjacentIndex], angle));
            }

            return Length(accumulated) > 1e-10 ? Normalize(accumulated) : faceNormal;
        }

        private static Vector3 Subtract(DomainPoint3D a, DomainPoint3D b) =>
            new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        private static Vector3 Cross(Vector3 a, Vector3 b) =>
            new(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);

        private static double Dot(Vector3 a, Vector3 b) =>
            a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        private static Vector3 Add(Vector3 a, Vector3 b) =>
            new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        private static Vector3 Multiply(Vector3 vector, double factor) =>
            new(vector.X * factor, vector.Y * factor, vector.Z * factor);

        private static double Length(Vector3 vector) =>
            Math.Sqrt(LengthSquared(vector));

        private static double LengthSquared(Vector3 vector) =>
            vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z;

        private static Vector3 Normalize(Vector3 vector)
        {
            var length = Length(vector);
            return length > 1e-30
                ? new Vector3(vector.X / length, vector.Y / length, vector.Z / length)
                : new Vector3(double.NaN, double.NaN, double.NaN);
        }

        private static bool IsInvalid(Vector3 vector) =>
            double.IsNaN(vector.X) || double.IsNaN(vector.Y) || double.IsNaN(vector.Z);

        private static DomainVector3D ToDomainVector(Vector3 vector) =>
            new(vector.X, vector.Y, vector.Z);

        private readonly struct Vector3
        {
            public Vector3(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public double X { get; }
            public double Y { get; }
            public double Z { get; }
        }
    }
}
