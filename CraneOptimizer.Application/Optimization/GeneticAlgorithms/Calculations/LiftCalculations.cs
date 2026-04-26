using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using System;

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
        /// For horizontal elements (slabs, beams laid flat):
        ///   radius = horizontal distance + halfElementLength
        ///   Reason: the crane must reach the far edge of a horizontal element,
        ///           not just its COG.
        ///
        /// For vertical elements (columns, walls standing upright):
        ///   radius = horizontal distance only
        ///   Reason: the element footprint is small in plan — the COG is the
        ///           correct reference point and no length offset is needed.
        ///
        /// Note: RiggingLength is NOT added here.
        ///       Rigging (slings/chains) runs vertically, so it affects
        ///       lift height only — see CalculateLiftHeight.
        /// </summary>
        public static double CalculateRadius(
            DomainPoint3D cranePosition,
            PrecastElement element)
        {
            double dx = cranePosition.X - element.ElementCOG.X;
            double dy = cranePosition.Y - element.ElementCOG.Y;

            double horizontalDistance = Math.Sqrt(dx * dx + dy * dy);

            // Only add half-length offset for horizontal elements.
            // An element is considered horizontal when its plan footprint
            // (length or width) is larger than its height.
            double lengthOffset = IsHorizontalElement(element)
                ? (element.ElementLength ?? 0) / 2.0
                : 0.0;

            return horizontalDistance + lengthOffset;
        }

        /// <summary>
        /// Determines whether an element is oriented horizontally in plan.
        ///
        /// Horizontal elements (slabs, beams) have their longest dimension
        /// in the X-Y plane → their plan length or width exceeds their height.
        ///
        /// Vertical elements (columns, walls) have height > plan dimensions.
        /// </summary>
        private static bool IsHorizontalElement(PrecastElement element)
        {
            double length = element.ElementLength ?? 0;
            double height = element.ElementHeight ?? 0;

            // If height data is missing, fall back to treating as horizontal
            // (conservative choice — adds the length offset).
            if (height <= 0)
                return true;

            return length > height;
        }

        // ══════════════════════════════════════════════════════════════
        // HEIGHT CALCULATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Calculates the required vertical lift height.
        ///
        /// Formula:
        ///   height = COG.Z + halfElementHeight + RiggingLength
        ///
        ///   COG.Z            → elevation of element center
        ///   halfElementHeight → from COG to the top of the element
        ///   RiggingLength    → vertical sling/chain length above the element
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
                   + settings.RiggingLength;
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
        ///   Find the nearest chart entry where BOTH:
        ///     tp.Radius    >= actual radius
        ///     tp.BoomLength >= actual boom length
        ///   Then take the entry with the SMALLEST values (closest to actual).
        ///
        /// This ensures we never use a capacity that was rated for a
        /// shorter radius or shorter boom than reality.
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
            var candidate = craneSpec.LoadChart
                .Where(tp =>
                    tp.Radius >= radius &&
                    tp.BoomLength >= boomLength)
                .OrderBy(tp => tp.Radius)
                .ThenBy(tp => tp.BoomLength)
                .FirstOrDefault();

            if (candidate == null)
                return 0;

            return candidate.Capacity * settings.SafetyFactor;
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