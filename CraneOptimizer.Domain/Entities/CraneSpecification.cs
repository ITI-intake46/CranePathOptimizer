using System.ComponentModel.DataAnnotations.Schema;

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

        #region Configurable load-chart CSV
        [NotMapped]
        public double? DeratingRatio
        {
            get => CraneDerating;
            set => CraneDerating = value;
        }

        public double? CraneDerating { get; set; }

        public string? OutriggerConfig { get; set; }
        #endregion

        /// <summary>
        /// Describes how the load chart is stored.
        /// Standard2D keeps the legacy Radius + BoomLength -> Capacity grid.
        /// ExactPdfVisual preserves repeated manufacturer PDF visual columns as individual points.
        /// </summary>
        public string? LoadChartStorageMode { get; set; }

        /// <summary>
        /// Capacity policy used when duplicate visual columns share the same radius and boom length.
        /// The current safe default is ConservativeMinimumAcrossDuplicateVisualColumns.
        /// </summary>
        public string? LoadChartCapacityPolicy { get; set; }

        /// <summary>
        /// Original exact visual grid serialized as JSON for faithful reopen/audit display.
        /// </summary>
        public string? ExactLoadChartRawGridJson { get; set; }

        /// <summary>
        /// Source description such as PDF page/table used to create this chart.
        /// </summary>
        public string? LoadChartSourceDescription { get; set; }

        public ICollection<TargetPoint> LoadChart { get; set; } = new List<TargetPoint>();
    }
}
