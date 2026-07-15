namespace CraneOptimizer.Domain.Diagnostics
{
    public sealed record IfcLoadingProgressUpdate(
        double Progress,
        string Status,
        string Stage
    );
}
