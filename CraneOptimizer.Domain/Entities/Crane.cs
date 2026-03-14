using CraneOptimizer.Domain.Enums;

namespace CraneOptimizer.Domain.Entities
{
    public class Crane : BaseEntity
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;

        public CraneType CraneType { get; set; }

        public double VehicleLength { get; set; }
        public double VehicleWidth { get; set; }
        public double VehicleHeight { get; set; }

        public double? OutriggerLength { get; set; }
        public double? OutriggerWidth { get; set; }

        public ICollection<CraneSpecification> Specifications { get; set; } = new List<CraneSpecification>();
    }

}
