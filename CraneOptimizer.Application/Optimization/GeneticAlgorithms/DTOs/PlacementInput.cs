using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs
{
    public class PlacementInput
    {
        public List<CranePlacementZone> Zones { get; set; }
        public List<PrecastElement> Elements { get; set; }
        public Dictionary<int, List<string>> Assignment { get; set; }
        public CraneSpecification CraneSpec { get; set; }
        public GASettings Settings { get; set; }
    }
}
