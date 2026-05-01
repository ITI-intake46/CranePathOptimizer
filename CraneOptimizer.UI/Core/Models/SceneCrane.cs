using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;

namespace CraneOptimizer.UI.Core.Models
{
    public class SceneCrane
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Crane CraneDefinition { get; set; } = null!;

        // Truck center in scene coordinates (meters)
        public DomainPoint3D Position { get; set; } = new();
        public double Rotation { get; set; } // truck heading deg

        // Kinematics
        public DomainPoint3D PivotOffset { get; set; } = new(0, 0, 1.5);
        public double BoomAngle { get; set; } = 45.0; // deg from horizontal
        public double BoomLength { get; set; } = 20.0; // m

        public CraneSpecification? ActiveSpecification { get; set; }

        public bool IsSelected { get; set; }
        public bool IsValidPlacement { get; set; } = true;

        public double? CurrentRadius { get; set; }
        public double? CurrentCapacity { get; set; }

        public DomainPoint3D GetBoomPivotWorld()
        {
            var a = Rotation * Math.PI / 180.0;
            var cos = Math.Cos(a);
            var sin = Math.Sin(a);

            var ox = PivotOffset.X * cos - PivotOffset.Y * sin;
            var oy = PivotOffset.X * sin + PivotOffset.Y * cos;

            return new DomainPoint3D(Position.X + ox, Position.Y + oy, Position.Z + PivotOffset.Z);
        }

        public DomainPoint3D GetBoomTipWorld()
        {
            var pivot = GetBoomPivotWorld();

            var heading = Rotation * Math.PI / 180.0;
            var elev = BoomAngle * Math.PI / 180.0;

            var horizontal = BoomLength * Math.Cos(elev);
            var dz = BoomLength * Math.Sin(elev);

            var dx = horizontal * Math.Cos(heading);
            var dy = horizontal * Math.Sin(heading);

            return new DomainPoint3D(pivot.X + dx, pivot.Y + dy, pivot.Z + dz);
        }
    }
}
