namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class ReportDataRowDto
{
    public Dictionary<string, object?> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
