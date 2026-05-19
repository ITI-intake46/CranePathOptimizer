namespace CraneOptimizer.UI.Features.F5_ElementManagement.DTOs
{
    public sealed record ElementPanelDto(
        ElementPanelLiftStateDto LiftState,
        string Id,
        string Name,
        string Type,
        string Material,
        double? Length,
        double? Width,
        double? Height,
        string Weight,
        double? Volume,
        double CenterX,
        double CenterY,
        double CenterZ);

    public sealed record ElementPanelLiftStateDto(
        string State,
        string CranePosition,
        double? Radius,
        double? RequiredCapacity);
}
