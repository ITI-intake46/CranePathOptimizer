using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs
{
    public class PlacementInput
    {
        public List<GreenAreaZone> Zones { get; set; }
        public List<GreenAreaZone> ForbiddenZones { get; set; } = new();
        public List<PrecastElement> Elements { get; set; }
        public Dictionary<int, List<string>> Assignment { get; set; }
        public CraneSpecification CraneSpec { get; set; }
        public GASettings Settings { get; set; }
        public string? ProjectName { get; set; }
        public string? ProjectId { get; set; }
        public string? ProjectFolderPath { get; set; }
        public string? IfcFileName { get; set; }
        public string? ScenarioId { get; set; }
        public string? ProjectFeatureSnapshotId { get; set; }
        public string? ScopeDefinitionHash { get; set; }
        public CurrentCanonicalModelSnapshotDto CanonicalSnapshot { get; set; } = new();
        public IReadOnlyList<CanonicalLiftUnitSnapshotDto> SubmittedCanonicalLiftUnits { get; set; } = Array.Empty<CanonicalLiftUnitSnapshotDto>();
        public string? CanonicalSnapshotId { get; set; }
        public string? CanonicalPayloadHash { get; set; }
        public int RawElementCount { get; set; }
        public int OptimizationScopeElementCount { get; set; }
        public int RequestedMaxCranePositions { get; set; }
        public List<PrecastElement> FeatureScopeElements { get; set; } = new();
        public int TotalElementsCount { get; set; }
        public int RawGaEligibleElementCount { get; set; }
        public int GroupedAssemblyCount { get; set; }
        public int UngroupedElementCount { get; set; }
        public string? DiagnosticsReportsDirectory { get; set; }
    }
}
