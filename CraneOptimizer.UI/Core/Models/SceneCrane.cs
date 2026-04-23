using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;

namespace CraneOptimizer.UI.Core.Models
{
    /// <summary>
    /// Represents a runtime instance of a Crane inside the Scene (Project-specific)
    /// </summary>
    public class SceneCrane
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // =========================
        // Reference (DB Definition)
        // =========================
        public Crane CraneDefinition { get; set; } = null!;

        // =========================
        // Placement (Scene-specific)
        // =========================
        public DomainPoint3D Position { get; set; } = new();
        public double Rotation { get; set; } // degrees

        // =========================
        // Selected Configuration
        // =========================
        public CraneSpecification? ActiveSpecification { get; set; }

        // =========================
        // Runtime State (for UI / GA)
        // =========================
        public bool IsSelected { get; set; }
        public bool IsValidPlacement { get; set; } = true;

        // Optional (for GA / evaluation)
        public double? CurrentRadius { get; set; }
        public double? CurrentCapacity { get; set; }
    }
}
