using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    public interface IConstraintEngine
    {
        bool IsValidSetupPoint(
            DomainPoint3D point,
            IReadOnlyList<GreenAreaZone> allowedZones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings);

        bool IsMeaningfullySeparated(
            DomainPoint3D candidate,
            IReadOnlyList<DomainPoint3D> existingPoints,
            GASettings settings);

        List<DomainPoint3D> FilterDistinctValidPoints(
            IEnumerable<DomainPoint3D> points,
            IReadOnlyList<GreenAreaZone> allowedZones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings);

        double CalculateSimilarityPenalty(
            IReadOnlyList<DomainPoint3D> points,
            GASettings settings);

        double CalculateNormalizedRelocationDistance(
            IReadOnlyList<DomainPoint3D> points,
            GASettings settings);
    }
}
