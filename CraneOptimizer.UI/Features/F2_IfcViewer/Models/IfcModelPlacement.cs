using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Models
{
    public sealed class IfcModelPlacement
    {
        public IfcModelPlacement(Guid modelInstanceId, IReadOnlyList<PrecastElement> elements)
        {
            ModelInstanceId = modelInstanceId;
            AnchorPoint = CalculateBottomCenter(elements);
        }

        public Guid ModelInstanceId { get; }
        public DomainPoint3D AnchorPoint { get; }
        public DomainPoint3D Translation { get; private set; } = new();

        public void PlaceAt(DomainPoint3D clickedPoint)
        {
            Translation = new DomainPoint3D(
                clickedPoint.X - AnchorPoint.X,
                clickedPoint.Y - AnchorPoint.Y,
                clickedPoint.Z - AnchorPoint.Z);
        }

        private static DomainPoint3D CalculateBottomCenter(IReadOnlyList<PrecastElement> elements)
        {
            if (elements.Count == 0)
                return new DomainPoint3D();

            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var minZ = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;

            foreach (var element in elements)
            {
                var length = element.BBoxLength ?? 0d;
                var width = element.BBoxWidth ?? 0d;
                var height = element.BBoxHeight ?? 0d;
                var cog = element.OriginalElementCOG ?? element.ElementCOG;

                minX = Math.Min(minX, cog.X - length / 2d);
                maxX = Math.Max(maxX, cog.X + length / 2d);
                minY = Math.Min(minY, cog.Y - width / 2d);
                maxY = Math.Max(maxY, cog.Y + width / 2d);
                minZ = Math.Min(minZ, cog.Z - height / 2d);
            }

            if (minX == double.MaxValue)
                return new DomainPoint3D();

            return new DomainPoint3D(
                (minX + maxX) / 2d,
                (minY + maxY) / 2d,
                minZ);
        }
    }
}
