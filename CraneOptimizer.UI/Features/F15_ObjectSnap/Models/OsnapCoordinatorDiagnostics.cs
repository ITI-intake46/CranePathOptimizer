namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Models
{
    public sealed record OsnapCoordinatorDiagnostics(
        long SubmittedHoverCount,
        long CompletedHoverCount,
        long CancelledHoverCount,
        long DroppedPendingHoverCount,
        int ActiveHoverCount,
        int PendingHoverCount,
        int MaximumConcurrentHoverCount);
}
