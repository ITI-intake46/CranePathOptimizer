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
        // CONSTANTS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Engineering tolerance (tons) applied to the capacity comparison.
        /// Prevents false Overweight results caused by floating-point rounding
        /// when an element weight is extremely close to the chart capacity.
        /// 0.001 t = 1 kg — well below measurement precision.
        /// </summary>
        private const double CapacityToleranceTons = 0.001;

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
        /// Strategy — bilinear interpolation (Issue 5 + 6 fix):
        ///   1. Find the two radius rows that bracket the required radius.
        ///   2. For each row, interpolate capacity along the boom dimension.
        ///   3. Linearly interpolate the two capacity values across the radius gap.
        ///
        ///   If only one side of the radius bracket exists, use that side
        ///   conservatively (no extrapolation beyond the outer radius row).
        ///
        ///   If the required radius exceeds the largest chart radius, the
        ///   caller's OutOfRadius check will already have fired first —
        ///   this method returns 0 to signal no-data only for genuine gaps.
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

        /// <summary>
        /// Performs bilinear interpolation over the load chart.
        ///
        /// Step 1 — Radius bracketing (Issue 5 fix):
        ///   Select chart rows independently of boom length.
        ///   Find the largest radius row ≤ required (lowerRadius) and
        ///   the smallest radius row ≥ required (upperRadius).
        ///   This avoids rejecting valid radius rows simply because
        ///   their boom entries don't satisfy boom constraints.
        ///
        /// Step 2 — Boom interpolation per radius row (Issue 6 fix):
        ///   For each bracketing radius row, interpolate capacity along
        ///   the boom dimension. When the boom request exceeds all chart
        ///   entries for that row, use the maximum entry (conservative)
        ///   rather than returning null (which would produce SpecialCase).
        ///
        /// Step 3 — Radius interpolation (Issue 6 enhancement):
        ///   Linearly interpolate the two boom-interpolated capacities
        ///   across the radius gap.
        /// </summary>
        private static double? InterpolateCapacityFromChart(
            CraneSpecification craneSpec,
            double radius,
            double boomLength)
        {
            // Collect all distinct radius values present in the chart
            var distinctRadii = craneSpec.LoadChart
                .Select(tp => tp.Radius)
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            if (distinctRadii.Count == 0)
                return null;

            // ── Step 1: Find radius brackets ──────────────────────────
            double? lowerRadius = null;
            double? upperRadius = null;

            foreach (var r in distinctRadii)
            {
                if (r <= radius)
                    lowerRadius = r;
                else if (upperRadius == null)
                    upperRadius = r;
            }

            // No chart data covers this radius range at all
            if (lowerRadius == null && upperRadius == null)
                return null;

            // ── Step 2: Interpolate boom for each bracketing row ──────

            double? lowerCapacity = null;
            double? upperCapacity = null;

            if (lowerRadius.HasValue)
            {
                var lowerRow = craneSpec.LoadChart
                    .Where(tp => tp.Radius == lowerRadius.Value)
                    .OrderBy(tp => tp.BoomLength)
                    .ToList();
                lowerCapacity = InterpolateCapacityAlongBoom(lowerRow, boomLength);
            }

            if (upperRadius.HasValue)
            {
                var upperRow = craneSpec.LoadChart
                    .Where(tp => tp.Radius == upperRadius.Value)
                    .OrderBy(tp => tp.BoomLength)
                    .ToList();
                upperCapacity = InterpolateCapacityAlongBoom(upperRow, boomLength);
            }

            // ── Step 3: Interpolate across radius ────────────────────

            // Both sides available → linear interpolation
            if (lowerRadius.HasValue && upperRadius.HasValue
                && lowerCapacity.HasValue && upperCapacity.HasValue)
            {
                double fraction = (radius - lowerRadius.Value)
                                / (upperRadius.Value - lowerRadius.Value);

                // Capacity decreases with radius, so interpolate conservatively
                return lowerCapacity.Value + fraction * (upperCapacity.Value - lowerCapacity.Value);
            }

            // Only upper bracket — radius is below the smallest chart radius.
            // Use upper capacity (we are closer than the smallest documented radius,
            // which is typically safe but use the nearest documented value).
            if (upperCapacity.HasValue)
                return upperCapacity.Value;

            // Only lower bracket — radius is beyond all chart rows.
            // The OutOfRadius check in DetermineLiftStatus fires first for
            // radii beyond craneSpec.MaxRadius, so reaching here means the
            // chart is just sparse at the outer boundary.
            if (lowerCapacity.HasValue)
                return lowerCapacity.Value;

            return null;
        }

        /// <summary>
        /// Interpolates crane capacity along the boom length dimension
        /// within a single radius row.
        ///
        /// Issue 6 fix — upper-bound behaviour:
        ///   When the requested boom length exceeds all entries in the row,
        ///   return the capacity at the maximum boom entry rather than null.
        ///   Returning null would cause SpecialCase for valid (if marginal)
        ///   configurations. The MaxBoomLength geometric check in
        ///   DetermineLiftStatus already guards against over-length booms.
        ///
        ///   When the requested boom is below the minimum chart entry,
        ///   return the minimum entry (conservative — shorter boom at same
        ///   radius typically has equal or better capacity).
        /// </summary>
        private static double? InterpolateCapacityAlongBoom(
            IReadOnlyList<TargetPoint> radiusRow,
            double boomLength)
        {
            if (radiusRow.Count == 0)
                return null;

            // Exact match
            var exactMatch = radiusRow.FirstOrDefault(tp => tp.BoomLength == boomLength);
            if (exactMatch != null)
                return exactMatch.Capacity;

            var lower = radiusRow.LastOrDefault(tp => tp.BoomLength < boomLength);
            var higher = radiusRow.FirstOrDefault(tp => tp.BoomLength > boomLength);

            // Boom exceeds all chart entries — use maximum entry (Issue 6 fix)
            if (higher == null)
                return radiusRow[^1].Capacity;

            // Boom is below all chart entries — use minimum entry (conservative)
            if (lower == null)
                return radiusRow[0].Capacity;

            // Standard linear interpolation
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
        ///                     (with CapacityToleranceTons tolerance for FP rounding)
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

            // ── 4. Capacity check (with FP tolerance) ─────────────
            // Issue 4 fix: use a small tolerance to avoid false Overweight
            // results when element weight equals chart capacity to within
            // floating-point precision.
            if (requiredCapacity > available + CapacityToleranceTons)
                return LiftStatus.Overweight;

            // ── 5. All checks passed ───────────────────────────────
            return LiftStatus.Liftable;
        }
    }
}
