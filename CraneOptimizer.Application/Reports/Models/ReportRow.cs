namespace CraneOptimizer.Application.Reports.Models;

public sealed class ReportRow
{
    public Dictionary<string, object?> Values { get; init; } = new(StringComparer.Ordinal);
}
