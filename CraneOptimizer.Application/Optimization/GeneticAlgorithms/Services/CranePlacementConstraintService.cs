using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Provides lightweight engineering constraints for GA crane setup positions.
    /// The service keeps zone, footprint, and spacing checks in one place so
    /// initialization, mutation, crossover cleanup, and final filtering use the same rules.
    /// </summary>
    public static class CranePlacementConstraintService
    {
        /// <summary>
        /// Determines whether a crane setup point is usable for optimization.
        /// Inputs are normalized scene coordinates in meters, allowed zones,
        /// forbidden zones, and GA settings containing the approximate footprint radius.
        /// The output is true only when the crane center and circular footprint fit inside
        /// at least one allowed zone and do not overlap any forbidden zone.
        /// </summary>
        public static bool IsValidSetupPoint(
            DomainPoint3D point,
            IReadOnlyList<GreenAreaZone> allowedZones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            if (allowedZones.Count == 0)
                return false;

            double footprintRadius = Math.Max(0, settings.CraneFootprintRadius);
            bool fitsAllowed = allowedZones.Any(zone => IsCircleInsidePolygon(point, footprintRadius, zone.Vertices));

            if (!fitsAllowed)
                return false;

            return !forbiddenZones.Any(zone => DoesCircleOverlapPolygon(point, footprintRadius, zone.Vertices));
        }

        /// <summary>
        /// Checks whether a candidate point is far enough from existing setup points
        /// to represent a real crane relocation rather than a small field adjustment.
        /// The output is true for an empty existing list or when every distance is at
        /// least the configured minimum relocation distance.
        /// </summary>
        public static bool IsMeaningfullySeparated(
            DomainPoint3D candidate,
            IReadOnlyList<DomainPoint3D> existingPoints,
            GASettings settings)
        {
            double minimumDistance = Math.Max(0, settings.MinimumRelocationDistance);
            if (minimumDistance <= 0 || existingPoints.Count == 0)
                return true;

            return existingPoints.All(point => Distance2D(candidate, point) >= minimumDistance);
        }

        /// <summary>
        /// Removes invalid and near-duplicate setup points while preserving the
        /// original order. The output is a practical crane relocation sequence for
        /// downstream evaluation and display.
        /// </summary>
        public static List<DomainPoint3D> FilterDistinctValidPoints(
            IEnumerable<DomainPoint3D> points,
            IReadOnlyList<GreenAreaZone> allowedZones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            var result = new List<DomainPoint3D>();

            foreach (var point in points)
            {
                if (!IsValidSetupPoint(point, allowedZones, forbiddenZones, settings))
                    continue;

                if (!IsMeaningfullySeparated(point, result, settings))
                    continue;

                result.Add(point);
            }

            return result;
        }

        /// <summary>
        /// Calculates a normalized penalty for setup points that are closer than
        /// the engineering relocation threshold. The output ranges from 0 upward,
        /// with larger values indicating stronger near-duplicate clustering.
        /// </summary>
        public static double CalculateSimilarityPenalty(
            IReadOnlyList<DomainPoint3D> points,
            GASettings settings)
        {
            if (points.Count < 2)
                return 0;

            double minimumDistance = Math.Max(0, settings.MinimumRelocationDistance);
            if (minimumDistance <= 0)
                return 0;

            double penalty = 0;
            int pairCount = 0;

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    pairCount++;
                    double distance = Distance2D(points[i], points[j]);
                    if (distance < minimumDistance)
                        penalty += (minimumDistance - distance) / minimumDistance;
                }
            }

            return pairCount == 0 ? 0 : penalty / pairCount;
        }

        /// <summary>
        /// Calculates total movement distance between sequential crane setup points.
        /// The output is normalized by crane reach and maximum setup count so it can
        /// participate in the existing weighted fitness formula.
        /// </summary>
        public static double CalculateNormalizedRelocationDistance(
            IReadOnlyList<DomainPoint3D> points,
            GASettings settings)
        {
            if (points.Count < 2)
                return 0;

            double totalDistance = 0;
            for (int i = 1; i < points.Count; i++)
                totalDistance += Distance2D(points[i - 1], points[i]);

            double normalizer = Math.Max(1, settings.MaxRadiusForNormalisation)
                * Math.Max(1, settings.MaxCranePositions - 1);

            return Math.Min(totalDistance / normalizer, 1.0);
        }

        /// <summary>
        /// Calculates the horizontal engineering distance between two setup points.
        /// The output is in meters because GA coordinates are normalized before optimization.
        /// </summary>
        public static double Distance2D(DomainPoint3D a, DomainPoint3D b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Tests whether a circular crane footprint is fully contained by a polygon.
        /// The point must be inside the polygon and at least radius away from every edge.
        /// </summary>
        private static bool IsCircleInsidePolygon(
            DomainPoint3D center,
            double radius,
            IReadOnlyList<DomainPoint3D> polygon)
        {
            if (polygon.Count < 3 || !IsPointInsidePolygon(center, polygon))
                return false;

            if (radius <= 0)
                return true;

            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];

                if (DistancePointToSegment2D(center, a, b) < radius)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Tests whether a circular crane footprint overlaps a forbidden polygon.
        /// The output is true if the crane center is inside the polygon or the footprint
        /// comes within radius of any forbidden edge.
        /// </summary>
        private static bool DoesCircleOverlapPolygon(
            DomainPoint3D center,
            double radius,
            IReadOnlyList<DomainPoint3D> polygon)
        {
            if (polygon.Count < 3)
                return false;

            if (IsPointInsidePolygon(center, polygon))
                return true;

            if (radius <= 0)
                return false;

            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];

                if (DistancePointToSegment2D(center, a, b) < radius)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Performs a ray-casting point-in-polygon check on the X/Y plane.
        /// This deterministic geometry test is used for both allowed and forbidden zones.
        /// </summary>
        private static bool IsPointInsidePolygon(
            DomainPoint3D point,
            IReadOnlyList<DomainPoint3D> polygon)
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
        /// Calculates the shortest X/Y distance from a point to a polygon edge segment.
        /// This supports circular footprint clearance checks without expensive geometry operations.
        /// </summary>
        private static double DistancePointToSegment2D(
            DomainPoint3D point,
            DomainPoint3D a,
            DomainPoint3D b)
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
    }
}
