using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.Domain.GreenAreaEntities
{
    public class CranePlacementZone
    {
        public Guid ZoneId { get; set; } = Guid.NewGuid();

        public List<DomainPoint3D> Vertices { get; set; } = new();

        public double Area { get; set; }

        public string Name { get; set; }
    }
}
