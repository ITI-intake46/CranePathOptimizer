namespace CraneOptimizer.Domain.Entities
{
    public class CraneSpecification : BaseEntity
    {
        public int Id { get; set; }

        public double MaxCapacity { get; set; }

        public double MaxRadius { get; set; }
        public double MinRadius { get; set; }

        public double MaxBoomLength { get; set; }
        public double MinBoomLength { get; set; }

        public double CounterWeight { get; set; }
        public double? MachineRotation { get; set; }

        public int CraneId { get; set; }
        public Crane Crane { get; set; } = null!;

        public ICollection<TargetPoint> LoadChart { get; set; } = new List<TargetPoint>();
    }

}
