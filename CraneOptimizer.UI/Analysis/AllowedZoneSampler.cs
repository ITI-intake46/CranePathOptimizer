using System;
using System.Collections.Generic;
using System.Windows;
using WPF_IFC_test1.ZoneModule.Models;

namespace WPF_IFC_test1.Analysis
{
    /// <summary>
    /// Builds the set of valid crane candidate positions from the Allowed (green) zones.
    ///
    /// Algorithm:
    ///  1. Compute AABB of all Allowed zone polygons.
    ///  2. Lay a uniform grid at <see cref="gridStepM"/> spacing.
    ///  3. Keep only grid points that lie inside at least one Allowed zone.
    ///  4. Further filter: the entire exclusion-zone disk of radius
    ///     <paramref name="exclusionRadiusM"/> must fit inside the allowed area.
    ///     → sample 36 boundary points (every 10°) and test each.
    /// </summary>
    public static class AllowedZoneSampler
    {
        /// <summary>
        /// Returns valid candidate crane positions (world X, Y).
        /// </summary>
        /// <param name="zones">All zones — only Allowed type are used.</param>
        /// <param name="gridStepM">Grid spacing in metres.</param>
        /// <param name="exclusionRadiusM">Crane exclusion zone radius in metres.</param>
        public static List<Point> Sample(
            IEnumerable<CraneZone> zones,
            double gridStepM,
            double exclusionRadiusM)
        {
            var allowed  = new List<CraneZone>();
            foreach (var z in zones)
                if (z.ZoneType == ZoneType.Allowed && z.Vertices.Count >= 3)
                    allowed.Add(z);

            if (allowed.Count == 0)
                return new List<Point>();

            // ── 1. AABB ────────────────────────────────────────────────────────
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var z in allowed)
                foreach (var v in z.Vertices)
                {
                    if (v.X < minX) minX = v.X;
                    if (v.X > maxX) maxX = v.X;
                    if (v.Y < minY) minY = v.Y;
                    if (v.Y > maxY) maxY = v.Y;
                }

            if (gridStepM <= 0) gridStepM = 2.0;

            // ── 2 & 3. Grid + point-in-polygon ────────────────────────────────
            var candidates = new List<Point>();

            for (double x = minX; x <= maxX; x += gridStepM)
            for (double y = minY; y <= maxY; y += gridStepM)
            {
                if (!IsInsideAnyAllowed(x, y, allowed)) continue;

                // ── 4. Exclusion-disk check ────────────────────────────────────
                if (exclusionRadiusM > 0 && !DiskFitsInAllowed(x, y, exclusionRadiusM, allowed))
                    continue;

                candidates.Add(new Point(x, y));
            }

            return candidates;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static bool IsInsideAnyAllowed(double x, double y, List<CraneZone> allowed)
        {
            foreach (var z in allowed)
                if (z.PointInPolygon(x, y)) return true;
            return false;
        }

        private static bool DiskFitsInAllowed(
            double x, double y,
            double radius,
            List<CraneZone> allowed)
        {
            // Sample 36 boundary points (every 10°).
            for (int i = 0; i < 36; i++)
            {
                double angle = i * 10.0 * Math.PI / 180.0;
                double bx    = x + radius * Math.Cos(angle);
                double by    = y + radius * Math.Sin(angle);
                if (!IsInsideAnyAllowed(bx, by, allowed))
                    return false;
            }
            return true;
        }
    }
}
