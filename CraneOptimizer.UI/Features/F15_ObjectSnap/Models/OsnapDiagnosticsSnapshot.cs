namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Models
{
    public sealed record OsnapDiagnosticsSnapshot(
        long IndexBuildCount,
        long QueryCount,
        long CancelledQueryCount,
        long RejectedStaleQueryCount,
        int LastIfcBuildThreadId,
        int LastDxfBuildThreadId,
        int LastZoneBuildThreadId,
        int LastActiveZoneBuildThreadId,
        int LastQueryThreadId,
        int LastIntersectionThreadId,
        bool LastIfcBuildHadSynchronizationContext,
        bool LastDxfBuildHadSynchronizationContext,
        bool LastZoneBuildHadSynchronizationContext,
        bool LastActiveZoneBuildHadSynchronizationContext,
        bool LastQueryHadSynchronizationContext,
        int LastPointCandidateCount,
        int LastSegmentCandidateCount,
        double LastQueryMilliseconds,
        double MeanQueryMilliseconds,
        double P95QueryMilliseconds);
}
