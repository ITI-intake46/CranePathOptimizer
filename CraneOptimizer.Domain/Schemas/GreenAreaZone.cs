using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas.Enums;

namespace CraneOptimizer.Domain.Schemas
{
    public class GreenAreaZone
    {
        public Guid ZoneId { get; set; } = Guid.NewGuid();

        public List<DomainPoint3D> Vertices { get; set; } = new();

        public double Area { get; set; }

        public string Name { get; set; }

        public ZoneType Type { get; set; }
    }
}
