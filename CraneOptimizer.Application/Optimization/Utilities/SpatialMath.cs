using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.Utilities
{
    /// <summary>
    /// Stateless static utility for 2D spatial, geometric, and bits mathematics.
    /// Exposes decoupled computational formulas used across the GA crane optimization engine.
    /// </summary>
    public static class SpatialMath
    {
        /// <summary>
        /// Calculates the straight-line horizontal distance between two points.
        /// </summary>
        /// <remarks>
        /// This method works in the 2D ground plane (ignoring the Z coordinate/height).
        /// It exists in the GA/crane optimization pipeline to measure relocation distances between
        /// crane setup points and to compute horizontal separation metrics for diversity scoring.
        /// It assumes both input points are in normalized coordinate units (meters).
        /// </remarks>
        /// <param name="a">The first 3D point.</param>
        /// <param name="b">The second 3D point.</param>
        /// <returns>The Euclidean 2D distance between point a and point b in meters.</returns>
        public static double Distance2D(DomainPoint3D a, DomainPoint3D b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Determines whether a given point lies inside a closed polygon using horizontal ray-casting.
        /// </summary>
        /// <remarks>
        /// This method works in the 2D ground plane (analyzing only X and Y coordinates).
        /// It exists in the GA/crane optimization pipeline to validate whether a candidate crane
        /// setup point is located inside allowed green placement zones or overlaps forbidden obstacles.
        /// It assumes the polygon is represented as a closed sequence of vertices in meters.
        /// </remarks>
        /// <param name="point">The candidate point to test.</param>
        /// <param name="polygon">The ordered list of vertices defining the closed polygon boundary.</param>
        /// <returns>True if the point is inside the polygon; otherwise, false.</returns>
        public static bool PointInPolygon(DomainPoint3D point, IReadOnlyList<DomainPoint3D> polygon)
        {
            bool inside = false;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];

                bool intersects =
                    ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                    point.X < (pj.X - pi.X) * (point.Y - pi.Y) / ((pj.Y - pi.Y) + 1e-12) + pi.X;

                if (intersects)
                    inside = !inside;
            }

            return inside;
        }

        /// <summary>
        /// Calculates the shortest horizontal distance from a point to a finite line segment (defined by endpoints a and b).
        /// </summary>
        /// <remarks>
        /// This method works in the 2D ground plane (X/Y axis projection, ignoring Z/height).
        /// It exists in the GA/crane optimization pipeline to support circular footprint safety checks.
        /// It ensures that the circular footprint of a crane setup does not overlap any forbidden edges,
        /// or remains safely contained within allowed zone boundaries by a distance at least equal to its radius.
        /// It assumes all points are expressed in meters.
        /// </remarks>
        /// <param name="point">The query point (e.g. crane center).</param>
        /// <param name="a">The start endpoint of the line segment.</param>
        /// <param name="b">The end endpoint of the line segment.</param>
        /// <returns>The perpendicular horizontal distance from the point to segment ab in meters.</returns>
        public static double PointToSegmentDistance(DomainPoint3D point, DomainPoint3D a, DomainPoint3D b)
        {
            double vx = b.X - a.X;
            double vy = b.Y - a.Y;
            double wx = point.X - a.X;
            double wy = point.Y - a.Y;

            double c1 = vx * wx + vy * wy;
            if (c1 <= 0)
                return Distance2D(point, a);

            double c2 = vx * vx + vy * vy;
            if (c2 <= c1)
                return Distance2D(point, b);

            double t = c1 / c2;
            var projection = new DomainPoint3D(a.X + t * vx, a.Y + t * vy, point.Z);
            return Distance2D(point, projection);
        }

        /// <summary>
        /// Calculates the spatial average position (centroid) of a collection of precast elements.
        /// </summary>
        /// <remarks>
        /// This method works in the 2D ground plane (returns a point with Z snapped to 0).
        /// It exists in the GA/crane optimization pipeline to calculate the initial heuristic centers
        /// of spatially clustered elements, providing high-quality seeding coordinates for crane placements
        /// during the GA population initialization phase.
        /// It assumes all elements contain a valid center of gravity (COG) in meters.
        /// </remarks>
        /// <param name="elements">The list of elements representing a spatial cluster.</param>
        /// <returns>A new point representing the horizontal centroid of the cluster with Z set to 0.</returns>
        public static DomainPoint3D Centroid(List<PrecastElement> elements)
        {
            if (elements == null || elements.Count == 0)
                return new DomainPoint3D(0, 0, 0);

            double x = elements.Average(e => e.ElementCOG.X);
            double y = elements.Average(e => e.ElementCOG.Y);

            return new DomainPoint3D(x, y, 0);
        }

        /// <summary>
        /// Calculates the horizontal bounding box (min/max X and Y limits) of a collection of vertices.
        /// </summary>
        /// <remarks>
        /// This method works in the 2D ground plane (X and Y coordinates only).
        /// It exists in the GA/crane optimization pipeline to speed up spatial rejection checks.
        /// For example, when generating random crane points, a bounding box check quickly discards points
        /// that fall entirely outside a zone's outer limits before running more expensive ray-casting checks.
        /// It assumes the input vertices are in meters.
        /// </remarks>
        /// <param name="vertices">The list of vertices defining the polygon shape.</param>
        /// <returns>A tuple containing the minimum and maximum X and Y coordinates in meters.</returns>
        public static (double minX, double maxX, double minY, double maxY) BoundingBox(IReadOnlyList<DomainPoint3D> vertices)
        {
            if (vertices == null || vertices.Count == 0)
                return (0, 0, 0, 0);

            return (
                vertices.Min(v => v.X),
                vertices.Max(v => v.X),
                vertices.Min(v => v.Y),
                vertices.Max(v => v.Y)
            );
        }

        /// <summary>
        /// Encodes two 10-bit integers into a single 20-bit Morton code (Z-order space-filling curve) by interleaving their bits.
        /// </summary>
        /// <remarks>
        /// This method works in the 2D ground plane (mapping two independent X/Y coordinates into a single 1D index).
        /// It exists in the GA/crane optimization pipeline as a Z-order spatial encoding technique used to sort
        /// and cluster nearby elements during GA initialization. Sorting elements along this 1D Morton curve
        /// groups spatially close elements together, which lets the initializer partition the sequence into compact
        /// 2D clusters and seed highly optimal crane centers.
        /// It assumes the inputs x and y are scaled integers in the range [0, 1023] (10-bit range).
        /// </remarks>
        /// <param name="x">The scaled integer X coordinate [0, 1023].</param>
        /// <param name="y">The scaled integer Y coordinate [0, 1023].</param>
        /// <returns>A 32-bit unsigned integer containing the Z-order Morton code key.</returns>
        public static uint MortonEncode(uint x, uint y)
        {
            // Spread the 10 bits of x into even bit positions (0, 2, 4, ..., 18)
            x &= 0x3FF;                // keep only 10 bits
            x = (x | (x << 10)) & 0x000F_83FF;
            x = (x | (x <<  4)) & 0x0007_E1F;
            x = (x | (x <<  2)) & 0x0055_5555;

            // Spread the 10 bits of y into even bit positions (0, 2, 4, ..., 18)
            y &= 0x3FF;
            y = (y | (y << 10)) & 0x000F_83FF;
            y = (y | (y <<  4)) & 0x0007_E1F;
            y = (y | (y <<  2)) & 0x0055_5555;

            // Interleave by shifting y left by 1 (occupying positions 1, 3, 5, ..., 19)
            return x | (y << 1);
        }
    }
}
