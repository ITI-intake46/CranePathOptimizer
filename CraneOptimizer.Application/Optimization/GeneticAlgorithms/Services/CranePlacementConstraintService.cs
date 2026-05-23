using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Temporary static compatibility wrapper delegating to ConstraintEngine and SpatialMath.
    /// </summary>
    public static class CranePlacementConstraintService
    {
        private static readonly IConstraintEngine _engine = new ConstraintEngine();

        public static bool IsValidSetupPoint(
            DomainPoint3D point,
            IReadOnlyList<GreenAreaZone> allowedZones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            return _engine.IsValidSetupPoint(point, allowedZones, forbiddenZones, settings);
        }

        public static bool IsMeaningfullySeparated(
            DomainPoint3D candidate,
            IReadOnlyList<DomainPoint3D> existingPoints,
            GASettings settings)
        {
            return _engine.IsMeaningfullySeparated(candidate, existingPoints, settings);
        }

        public static List<DomainPoint3D> FilterDistinctValidPoints(
            IEnumerable<DomainPoint3D> points,
            IReadOnlyList<GreenAreaZone> allowedZones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            return _engine.FilterDistinctValidPoints(points, allowedZones, forbiddenZones, settings);
        }

        public static double CalculateSimilarityPenalty(
            IReadOnlyList<DomainPoint3D> points,
            GASettings settings)
        {
            return _engine.CalculateSimilarityPenalty(points, settings);
        }

        public static double CalculateNormalizedRelocationDistance(
            IReadOnlyList<DomainPoint3D> points,
            GASettings settings)
        {
            return _engine.CalculateNormalizedRelocationDistance(points, settings);
        }

        public static double Distance2D(DomainPoint3D a, DomainPoint3D b)
        {
            return SpatialMath.Distance2D(a, b);
        }
    }
}
