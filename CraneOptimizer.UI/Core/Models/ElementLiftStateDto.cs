namespace CraneOptimizer.UI.Core.Models
{
    public sealed record ElementLiftStateDto(
        string ElementId,
        string State,
        double? CraneX,
        double? CraneY,
        double? Radius,
        double? RequiredCapacity);
}
