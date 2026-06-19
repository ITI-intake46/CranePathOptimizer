namespace CraneOptimizer.Application.Reports.Models;

public sealed class CsvExportOptions
{
    public bool IncludeMetadataFile { get; init; } = true;
    public bool IncludeManifestFile { get; init; } = true;
    public bool OneFilePerTable { get; init; } = true;
}
