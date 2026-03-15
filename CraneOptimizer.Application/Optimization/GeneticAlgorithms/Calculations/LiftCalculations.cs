using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.IfcEntities;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Calculations
{
    /// <summary>
    /// Contains engineering calculations used for crane lift evaluation.
    /// This class is stateless and provides only static helper methods.
    /// </summary>
    public static class LiftCalculation
    {
        /// <summary>
        /// Calculates working radius between crane position and element COG.
        /// </summary>
        public static double CalculateRadius(
            Point3D cranePosition,
            PrecastElement element,
            GASettings settings)
        {
            double dx = cranePosition.X - element.ElementCOG.X;
            double dy = cranePosition.Y - element.ElementCOG.Y;

            double horizontalDistance = Math.Sqrt(dx * dx + dy * dy);

            double halfElementLength = (element.ElementLength ?? 0) / 2.0;

            return horizontalDistance
                   + halfElementLength
                   + settings.RiggingLength;
        }

        /// <summary>
        /// Calculates vertical lift height.
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

        /// <summary>
        /// Calculates required boom length.
        /// </summary>
        public static double CalculateRequiredBoomLength(
            double radius,
            double height)
        {
            return Math.Sqrt(radius * radius + height * height);
        }

        /// <summary>
        /// Looks up crane capacity from load chart.
        /// </summary>
        public static double LookupCapacity(
            CraneSpecification craneSpec,
            double radius,
            double boomLength,
            GASettings settings)
        {
            var candidate = craneSpec.LoadChart
                .Where(tp =>
                    tp.Radius <= radius &&
                    tp.BoomLength <= boomLength)
                .OrderByDescending(tp => tp.Radius)
                .ThenByDescending(tp => tp.BoomLength)
                .FirstOrDefault();

            if (candidate == null)
                return 0;

            return candidate.Capacity * settings.SafetyFactor;
        }
    }
}