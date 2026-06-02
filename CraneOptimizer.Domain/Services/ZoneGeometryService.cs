using System;
using System.Collections.Generic;
using System.Linq;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;

namespace CraneOptimizer.Domain.Services
{
    public class ZoneGeometryService : IZoneGeometryService
    {
        private const double PointEqualityTolerance = 0.001;

        // Initialize static instance holder
        static ZoneGeometryService()
        {
            ZoneGeometryServiceHolder.Instance = new ZoneGeometryService();
        }

        public List<DomainPoint3D> NormalizeVertices(IEnumerable<DomainPoint3D> vertices)
        {
            if (vertices == null) return new List<DomainPoint3D>();

            var list = new List<DomainPoint3D>();
            foreach (var pt in vertices)
            {
                if (list.Count == 0 || !PointsAreEqual(list[^1], pt))
                {
                    list.Add(pt);
                }
            }

            // Remove trailing consecutive duplicate with the start point (closure)
            while (list.Count > 1 && PointsAreEqual(list[0], list[^1]))
            {
                list.RemoveAt(list.Count - 1);
            }

            return list;
        }

        public bool ValidatePolygon(IReadOnlyList<DomainPoint3D> vertices, out string? warning)
        {
            warning = null;
            var normalized = NormalizeVertices(vertices);

            if (normalized.Count < 3)
            {
                warning = "⚠ A valid zone polygon requires at least 3 unique vertices.";
                return false;
            }

            if (HasSelfIntersection(normalized))
            {
                warning = "⚠ Self-intersecting polygons (e.g. bow-tie shapes) are not allowed.";
                return false;
            }

            return true;
        }

        public double CalculateSignedArea2D(IReadOnlyList<DomainPoint3D> vertices)
        {
            var pts = NormalizeVertices(vertices);
            if (pts.Count < 3) return 0;

            double area = 0;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                var p1 = pts[i];
                var p2 = pts[(i + 1) % n];
                area += (p1.X * p2.Y) - (p2.X * p1.Y);
            }
            return area * 0.5;
        }

        public List<DomainPoint3D> EnsureOrientation(List<DomainPoint3D> vertices, bool ccw = true)
        {
            var pts = NormalizeVertices(vertices);
            if (pts.Count < 3) return pts;

            double signedArea = CalculateSignedArea2D(pts);
            bool isCcw = signedArea > 0;

            if (isCcw != ccw)
            {
                pts.Reverse();
            }
            return pts;
        }

        public bool HasSelfIntersection(IReadOnlyList<DomainPoint3D> vertices)
        {
            var pts = NormalizeVertices(vertices);
            int n = pts.Count;
            if (n < 4) return false;

            for (int i = 0; i < n; i++)
            {
                var a1 = pts[i];
                var a2 = pts[(i + 1) % n];

                for (int j = i + 2; j < n; j++)
                {
                    if ((j + 1) % n == i) continue; // skip consecutive edges

                    var b1 = pts[j];
                    var b2 = pts[(j + 1) % n];

                    if (SegmentsIntersect2D(a1, a2, b1, b2))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool PointInPolygon(DomainPoint3D point, IReadOnlyList<DomainPoint3D> vertices)
        {
            var pts = NormalizeVertices(vertices);
            if (pts.Count < 3) return false;

            bool inside = false;
            int n = pts.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = pts[i];
                var pj = pts[j];

                bool intersect = ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                                 (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / ((pj.Y - pi.Y) + 1e-12) + pi.X);

                if (intersect)
                    inside = !inside;
            }

            return inside;
        }

        public List<int> TriangulateSimplePolygon(IReadOnlyList<DomainPoint3D> vertices)
        {
            var pts = NormalizeVertices(vertices);
            int n = pts.Count;
            if (n < 3) return new List<int>();

            // Determine if CCW
            bool isCcw = CalculateSignedArea2D(pts) > 0;
            var orientedPts = new List<DomainPoint3D>(pts);
            if (!isCcw)
            {
                orientedPts.Reverse();
            }

            var indices = new List<int>();
            var activeIndices = new List<int>();
            for (int i = 0; i < n; i++) activeIndices.Add(i);

            int iterations = 0;
            int maxIterations = n * n; // Safety cap

            while (activeIndices.Count > 3 && iterations < maxIterations)
            {
                iterations++;
                bool earFound = false;

                for (int i = 0; i < activeIndices.Count; i++)
                {
                    int prevIdx = activeIndices[(i - 1 + activeIndices.Count) % activeIndices.Count];
                    int currIdx = activeIndices[i];
                    int nextIdx = activeIndices[(i + 1) % activeIndices.Count];

                    if (IsEar(prevIdx, currIdx, nextIdx, orientedPts, activeIndices))
                    {
                        indices.Add(prevIdx);
                        indices.Add(currIdx);
                        indices.Add(nextIdx);

                        activeIndices.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }

                if (!earFound)
                {
                    break; // stop on self-intersection
                }
            }

            if (activeIndices.Count == 3)
            {
                indices.Add(activeIndices[0]);
                indices.Add(activeIndices[1]);
                indices.Add(activeIndices[2]);
            }

            // Map indices back
            var mappedIndices = new List<int>();
            for (int i = 0; i < indices.Count; i++)
            {
                int orientedIdx = indices[i];
                int originalNormalizedIdx = isCcw ? orientedIdx : (n - 1 - orientedIdx);
                mappedIndices.Add(originalNormalizedIdx);
            }

            return mappedIndices;
        }

        public bool ValidateTriangulation(IReadOnlyList<DomainPoint3D> vertices, List<int> indices, out string? error)
        {
            error = null;
            var pts = NormalizeVertices(vertices);
            if (pts.Count < 3)
            {
                error = "Too few vertices to validate triangulation.";
                return false;
            }

            double polyArea = Math.Abs(CalculateSignedArea2D(pts));
            double sumTriArea = 0;

            for (int i = 0; i < indices.Count; i += 3)
            {
                if (i + 2 >= indices.Count)
                {
                    error = "Invalid index count (not divisible by 3).";
                    return false;
                }

                int idx1 = indices[i];
                int idx2 = indices[i + 1];
                int idx3 = indices[i + 2];

                if (idx1 < 0 || idx1 >= pts.Count ||
                    idx2 < 0 || idx2 >= pts.Count ||
                    idx3 < 0 || idx3 >= pts.Count)
                {
                    error = "Triangle index out of bounds.";
                    return false;
                }

                var a = pts[idx1];
                var b = pts[idx2];
                var c = pts[idx3];

                // Calculate triangle area via 2D cross product
                double triArea = Math.Abs((a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y)) * 0.5);

                // Degenerate check
                if (triArea < 1e-6)
                {
                    error = $"Degenerate triangle found at index {i / 3} with area {triArea:E4}.";
                    return false;
                }

                sumTriArea += triArea;

                // Centroid check
                var centroid = new DomainPoint3D(
                    (a.X + b.X + c.X) / 3.0,
                    (a.Y + b.Y + c.Y) / 3.0,
                    (a.Z + b.Z + c.Z) / 3.0
                );

                if (!PointInPolygon(centroid, pts))
                {
                    error = $"Triangle centroid ({centroid.X:F3}, {centroid.Y:F3}) lies outside the polygon.";
                    return false;
                }
            }

            // Area check
            if (Math.Abs(sumTriArea - polyArea) > 0.005)
            {
                error = $"Triangulated area sum ({sumTriArea:F3}) does not match polygon area ({polyArea:F3}).";
                return false;
            }

            return true;
        }

        private static bool PointsAreEqual(DomainPoint3D a, DomainPoint3D b)
        {
            return Math.Abs(a.X - b.X) < PointEqualityTolerance &&
                   Math.Abs(a.Y - b.Y) < PointEqualityTolerance &&
                   Math.Abs(a.Z - b.Z) < PointEqualityTolerance;
        }

        private static bool SegmentsIntersect2D(DomainPoint3D a, DomainPoint3D b, DomainPoint3D c, DomainPoint3D d)
        {
            double d1 = Direction(c, d, a);
            double d2 = Direction(c, d, b);
            double d3 = Direction(a, b, c);
            double d4 = Direction(a, b, d);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                (((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))))
            {
                return true;
            }

            if (Math.Abs(d1) < 1e-9 && OnSegment(c, d, a)) return true;
            if (Math.Abs(d2) < 1e-9 && OnSegment(c, d, b)) return true;
            if (Math.Abs(d3) < 1e-9 && OnSegment(a, b, c)) return true;
            if (Math.Abs(d4) < 1e-9 && OnSegment(a, b, d)) return true;

            return false;
        }

        private static double Direction(DomainPoint3D a, DomainPoint3D b, DomainPoint3D c)
        {
            return (c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X);
        }

        private static bool OnSegment(DomainPoint3D p1, DomainPoint3D p2, DomainPoint3D pt)
        {
            return pt.X >= Math.Min(p1.X, p2.X) - PointEqualityTolerance &&
                   pt.X <= Math.Max(p1.X, p2.X) + PointEqualityTolerance &&
                   pt.Y >= Math.Min(p1.Y, p2.Y) - PointEqualityTolerance &&
                   pt.Y <= Math.Max(p1.Y, p2.Y) + PointEqualityTolerance;
        }

        private bool IsEar(int u, int v, int w, List<DomainPoint3D> pts, List<int> activeIndices)
        {
            var pU = pts[u];
            var pV = pts[v];
            var pW = pts[w];

            double cross = (pV.X - pU.X) * (pW.Y - pV.Y) - (pV.Y - pU.Y) * (pW.X - pV.X);
            if (cross <= 1e-9) return false;

            foreach (int idx in activeIndices)
            {
                if (idx == u || idx == v || idx == w) continue;

                var p = pts[idx];
                if (PointInTriangle2D(p, pU, pV, pW))
                {
                    return false;
                }
            }

            return true;
        }

        private bool PointInTriangle2D(DomainPoint3D p, DomainPoint3D a, DomainPoint3D b, DomainPoint3D c)
        {
            double d1 = Direction(p, a, b);
            double d2 = Direction(p, b, c);
            double d3 = Direction(p, c, a);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(hasNeg && hasPos);
        }
    }
}
