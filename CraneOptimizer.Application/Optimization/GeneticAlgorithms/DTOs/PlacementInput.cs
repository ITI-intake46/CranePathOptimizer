using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
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
        public string? ProjectFolderPath { get; set; }
        public string? IfcFileName { get; set; }
        public int TotalElementsCount { get; set; }
    }
}
