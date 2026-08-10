using CraneOptimizer.Application.Reports.Definitions;

namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class ReportDocumentDto
{
    public string DocumentId { get; init; } = Guid.NewGuid().ToString("N");
    public string ReportBuildSessionId { get; init; } = Guid.NewGuid().ToString("N");
    public string ReportReference { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string InputSignature { get; init; } = string.Empty;
    public string SourceSignature { get; init; } = string.Empty;
    public ReportFamily InternalFamily { get; init; }
    public ReportMetadataDto Metadata { get; init; } = new();
    public IReadOnlyList<ReportColumnDefinition> Columns { get; init; } = Array.Empty<ReportColumnDefinition>();
    public IReadOnlyList<ReportDataRowDto> Rows { get; init; } = Array.Empty<ReportDataRowDto>();
    public IReadOnlyList<CraneSetupReportRowDto> CraneSetups { get; init; } = Array.Empty<CraneSetupReportRowDto>();
    public bool PreferA3Landscape { get; init; }
    public string TextAlignment { get; init; } = "Left";
    public double TextSizePt { get; init; } = 10;
}
