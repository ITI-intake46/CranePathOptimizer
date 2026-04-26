using CraneOptimizer.Domain.Common;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs
{
    public class PlacementInput
    {
        public List<GreenAreaZone> AllowedZones { get; set; }
        public List<GreenAreaZone> ForbiddenZones { get; set; }
        public List<PrecastElement> Elements { get; set; }
        public IReadOnlyList<DomainPoint3D> ElementCenters { get; set; }
        public IReadOnlyList<double> ElementWeights { get; set; }
        public Dictionary<int, List<string>> Assignment { get; set; }
        public CraneSpecification CraneSpec { get; set; }
        public GASettings Settings { get; set; }
    }
}
