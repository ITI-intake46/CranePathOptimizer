namespace CraneOptimizer.Application.Reports.Models;

public sealed class ReportExportResult
{
    public bool Success { get; init; }
    public string PrimaryOutputPath { get; init; } = string.Empty;
    public List<string> OutputFiles { get; init; } = new();
    public string? ErrorMessage { get; init; }

    public static ReportExportResult Ok(string primaryPath, IEnumerable<string>? outputFiles = null)
    {
        var files = outputFiles?.ToList() ?? new List<string> { primaryPath };

        return new ReportExportResult
        {
            Success = true,
            PrimaryOutputPath = primaryPath,
            OutputFiles = files
        };
    }

    public static ReportExportResult Fail(string message)
    {
        return new ReportExportResult
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
