namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Models
{
    /// <summary>
    /// Canonical immutable Object Snap request used for both hover and click.
    /// </summary>
    public sealed record OsnapQuery(
        long RequestId,
        bool IsClick,
        OsnapViewportSnapshot Viewport);

    /// <summary>
    /// A completed request together with the exact metadata used to calculate it.
    /// </summary>
    public sealed record OsnapQueryCompletion(
        OsnapQuery Query,
        OsnapResult? SnapResult,
        double ElapsedMilliseconds);
}
