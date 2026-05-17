using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Calculations
{
    /// <summary>
    /// Contains engineering calculations used for crane lift evaluation.
    /// This class is stateless and provides only static helper methods.
    /// </summary>
    public static class LiftCalculation
    {
        // ══════════════════════════════════════════════════════════════
        // RADIUS CALCULATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Calculates the working radius between the crane position
        /// and the element center of gravity (COG).
        ///
        /// For all elements, the working radius is measured to the
        /// element center of gravity (COG).
        ///
        /// This matches actual hook radius conventions used by crane load
        /// charts: the lift point is referenced to the object center instead
        /// of the far edge of the object geometry.
        ///
        /// Note: WireLength is NOT added here.
        ///       Hoist wire/sling length runs vertically, so it affects
        ///       lift height only — see CalculateLiftHeight.
        /// </summary>
        public static double CalculateRadius(
            DomainPoint3D cranePosition,
            PrecastElement element)
        {
            double dx = cranePosition.X - element.ElementCOG.X;
            double dy = cranePosition.Y - element.ElementCOG.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        // ══════════════════════════════════════════════════════════════
        // HEIGHT CALCULATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Calculates the required vertical lift height.
        ///
        /// Formula:
        ///   height = COG.Z + halfElementHeight + WireLength
        ///
        ///   COG.Z            → elevation of element center
        ///   halfElementHeight → from COG to the top of the element
        ///   WireLength       → vertical wire/sling length allowance above the element
        ///
        /// This gives the required hook height above ground.
        /// </summary>
        public static double CalculateLiftHeight(
            PrecastElement element,
            GASettings settings)
        {
            double elementHalfHeight = (element.ElementHeight ?? 0) / 2.0;

            return element.ElementCOG.Z
                   + elementHalfHeight
                   + settings.WireLength;
        }

        // ══════════════════════════════════════════════════════════════
        // BOOM LENGTH CALCULATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Calculates the required boom length using the Pythagorean theorem.
        ///
        ///   BoomLength = √(Radius² + Height²)
        ///
        /// This represents the straight-line distance from the crane pivot
        /// to the hook position in 3D space.
        /// </summary>
        public static double CalculateRequiredBoomLength(
            double radius,
            double height)
        {
            return Math.Sqrt(radius * radius + height * height);
        }

        // ══════════════════════════════════════════════════════════════
        // LOAD CHART LOOKUP
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Looks up the available crane capacity from the load chart
        /// for the given radius and boom length.
        ///
        /// Strategy — conservative interpolation:
        ///   Use the smallest chart radius row that covers the required radius,
        ///   then interpolate capacity along boom length for the requested boom.
        ///
        /// This keeps the estimate conservative while avoiding abrupt drops
        /// when the requested boom length falls between chart rows.
        ///
        /// Returns 0 if no valid entry exists in the chart.
        /// Callers must distinguish between 0 (no data) and OutOfRadius —
        /// use DetermineLiftStatus for full status classification.
        /// </summary>
        public static double CraneCapacity(
            CraneSpecification craneSpec,
            double radius,
            double boomLength,
            GASettings settings)
        {
            if (craneSpec.LoadChart == null || craneSpec.LoadChart.Count == 0)
                return 0;

            double? capacity = InterpolateCapacityFromChart(craneSpec, radius, boomLength);
            if (capacity == null || capacity <= 0)
                return 0;

            return capacity.Value * settings.DeratingRatio * settings.SafetyFactor;
        }

        private static double? InterpolateCapacityFromChart(
            CraneSpecification craneSpec,
            double radius,
            double boomLength)
        {
            var matchingRows = craneSpec.LoadChart
                .Where(tp => tp.Radius >= radius && tp.BoomLength >= boomLength)
                .ToList();

            if (matchingRows.Count == 0)
                return null;

            var nearestRadius = matchingRows
                .Select(tp => tp.Radius)
                .Distinct()
                .OrderBy(r => r)
                .FirstOrDefault();

            var radiusRow = matchingRows
                .Where(tp => tp.Radius == nearestRadius)
                .OrderBy(tp => tp.BoomLength)
                .ToList();

            if (radiusRow.Count == 0)
                return null;

            return InterpolateCapacityAlongBoom(radiusRow, boomLength);
        }

        private static double? InterpolateCapacityAlongBoom(
            IReadOnlyList<TargetPoint> radiusRow,
            double boomLength)
        {
            var exactMatch = radiusRow.FirstOrDefault(tp => tp.BoomLength == boomLength);
            if (exactMatch != null)
                return exactMatch.Capacity;

            var lower = radiusRow.LastOrDefault(tp => tp.BoomLength < boomLength);
            var higher = radiusRow.FirstOrDefault(tp => tp.BoomLength > boomLength);

            if (higher == null)
                return null;

            if (lower == null)
                return higher.Capacity;

            return Interpolate(
                boomLength,
                lower.BoomLength,
                lower.Capacity,
                higher.BoomLength,
                higher.Capacity);
        }

        private static double Interpolate(
            double value,
            double x0,
            double y0,
            double x1,
            double y1)
        {
            if (Math.Abs(x1 - x0) < double.Epsilon)
                return y0;

            double fraction = (value - x0) / (x1 - x0);
            return y0 + fraction * (y1 - y0);
        }

        // ══════════════════════════════════════════════════════════════
        // LIFT STATUS DETERMINATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Determines the full lift status for an element
        /// given the computed physical parameters.
        ///
        /// Evaluation order (fail-fast — most obvious failures first):
        ///
        ///   1. OutOfRadius  → radius exceeds crane's maximum reach
        ///   2. HeightFail   → boom length exceeds crane's maximum boom
        ///   3. NoChartData  → crane spec has no load chart entry for this
        ///                     radius/boom combination (gap in manufacturer data)
        ///   4. Overweight   → element weight exceeds available chart capacity
        ///   5. Liftable     → all checks passed
        ///
        /// The distinction between OutOfRadius and NoChartData is intentional:
        ///   OutOfRadius → crane physically cannot reach (geometric limit)
        ///   NoChartData → crane might reach, but capacity is unknown (data gap)
        ///   These must be treated differently in reporting and fitness scoring.
        /// </summary>
        public static LiftStatus DetermineLiftStatus(
            double radius,
            double boomLength,
            double requiredCapacity,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            // ── 1. Geometric reach check ───────────────────────────
            if (radius > craneSpec.MaxRadius)
                return LiftStatus.OutOfRadius;

            // ── 2. Boom length check ───────────────────────────────
            if (boomLength > craneSpec.MaxBoomLength)
                return LiftStatus.HeightFail;

            // ── 3. Load chart data availability ───────────────────
            double available = CraneCapacity(craneSpec, radius, boomLength, settings);

            if (available <= 0)
                // Radius is within crane range, but no chart entry exists.
                // This is a data gap, not a geometric failure.
                return LiftStatus.SpecialCase;

            // ── 4. Capacity check ──────────────────────────────────
            if (requiredCapacity > available)
                return LiftStatus.Overweight;

            // ── 5. All checks passed ───────────────────────────────
            return LiftStatus.Liftable;
        }
    }
}
