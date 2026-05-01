using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Features.F3_CraneSetup
{
    /// <summary>
    /// Enforces green zone boundary rules during crane dragging.
    ///
    /// Rule:
    ///   • If no zones exist → any position is allowed (return as-is).
    ///   • If zones exist    → position must be inside at least one zone.
    ///                         If it is outside, CLAMP it to the nearest valid point.
    /// </summary>
    public static class GreenZoneConstraint
    {
        /// <summary>
        /// Returns the nearest allowed position for the given candidate point.
        /// </summary>
        public static DomainPoint3D Clamp(DomainPoint3D candidate, List<GreenAreaZone> zones)
        {
            // No zones → free movement
            if (zones == null || zones.Count == 0)
                return candidate;

            // Already inside a zone → accept as-is
            if (IsInsideAnyZone(candidate, zones))
                return candidate;

            // Outside all zones → find the closest point on any zone boundary
            // This is the "clamp" behaviour: the crane snaps to the edge.
            return FindNearestBoundaryPoint(candidate, zones);
        }

        /// <summary>
        /// Returns true if the point is inside any green zone polygon.
        /// Uses the Ray Casting algorithm (same as your existing GA code).
        /// </summary>
        public static bool IsInsideAnyZone(DomainPoint3D point, List<GreenAreaZone> zones)
        {
            return zones.Any(zone => IsPointInsidePolygon(point, zone.Vertices));
        }

        // ─────────────────────────────────────────────────────────────────
        // RAY CASTING — point-in-polygon test
        // ─────────────────────────────────────────────────────────────────
        // This is the same algorithm already used in your GAInitializer and
        // MutationOperator — extracted here so the UI can reuse it too.
        //
        // How it works: cast a horizontal ray from the point to the right.
        // Count how many times it crosses a polygon edge.
        // Odd count → inside. Even count → outside.
        private static bool IsPointInsidePolygon(DomainPoint3D point, List<DomainPoint3D> polygon)
        {
            bool inside = false;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];

                bool crossesY = (pi.Y > point.Y) != (pj.Y > point.Y);
                bool crossesX = point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X;

                if (crossesY && crossesX)
                    inside = !inside;
            }

            return inside;
        }

        // ─────────────────────────────────────────────────────────────────
        // CLAMPING — find closest point on zone boundary
        // ─────────────────────────────────────────────────────────────────
        private static DomainPoint3D FindNearestBoundaryPoint(
            DomainPoint3D candidate,
            List<GreenAreaZone> zones)
        {
            DomainPoint3D? best = null;
            double bestDist = double.MaxValue;

            foreach (var zone in zones)
            {
                var verts = zone.Vertices;

                for (int i = 0, j = verts.Count - 1; i < verts.Count; j = i++)
                {
                    // Project candidate onto this edge; clamp to segment endpoints
                    var closest = ClosestPointOnSegment(candidate, verts[j], verts[i]);
                    double dist = Distance2D(candidate, closest);

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = closest;
                    }
                }
            }

            return best ?? candidate;
        }

        private static DomainPoint3D ClosestPointOnSegment(
            DomainPoint3D p, DomainPoint3D a, DomainPoint3D b)
        {
            double abX = b.X - a.X;
            double abY = b.Y - a.Y;
            double len2 = abX * abX + abY * abY;

            if (len2 < 1e-10) return a; // degenerate segment

            // t is the 0→1 parameter along the segment
            double t = ((p.X - a.X) * abX + (p.Y - a.Y) * abY) / len2;
            t = Math.Clamp(t, 0.0, 1.0);

            return new DomainPoint3D(
                a.X + t * abX,
                a.Y + t * abY,
                0   // ground plane Z
            );
        }

        private static double Distance2D(DomainPoint3D a, DomainPoint3D b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}