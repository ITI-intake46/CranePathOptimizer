namespace CraneOptimizer.UI.Features.F11_DXF.DTOs
{
    public sealed record DxfSourceState(
        Guid Id,
        string FilePath,
        double[] TransformValues,
        int LoadedOrder,
        bool IsActive,
        bool IsSelected,
        bool HasSavedTransform,
        bool UseDxfColors,
        string? DisplayName = null);
}
