using System.Collections.Generic;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    public interface IReachabilityMapService
    {
        ReachabilityDiagnostics? LastDiagnostics { get; }
        IReadOnlyList<ScoredCoordinate> LastHotSpots { get; }
        IReadOnlyList<ZoneReachabilitySummary> LastZoneSummaries { get; }

        IReadOnlyList<ScoredCoordinate> ComputeHotSpots(
            IEnumerable<PrecastElement> elements,
            IEnumerable<GreenAreaZone> allowedZones,
            IEnumerable<GreenAreaZone> forbiddenZones,
            CraneSpecification craneSpec,
            GASettings settings,
            int topN);

        void RecordSelectedSeed(ScoredCoordinate coordinate);
        bool WasSelectedAsSeed(ScoredCoordinate coordinate);
    }
}
