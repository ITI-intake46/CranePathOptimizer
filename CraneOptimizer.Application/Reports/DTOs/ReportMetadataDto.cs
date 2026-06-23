namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class ReportMetadataDto
{
    public string ReportName { get; init; } = string.Empty;
    public string ItemsIncluded { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public DateTime ReportDate { get; init; } = DateTime.Now;
    public string? CompanyName { get; init; }
    public string? CompanyLogoPath { get; init; }
    public string? CreatedBy { get; init; }
    public string? IssuedTo { get; init; }
    public string? Revision { get; init; }
    public bool HasGaRun { get; init; } = true;
    public string? GaRunStatusText { get; init; }
    public string? GaWarningText { get; init; }
    public CraneReportInfoDto? CraneInfo { get; init; }
    public LiftingAccessoriesInfoDto? LiftingAccessories { get; init; }
    public IReadOnlyList<ReportSummaryItemDto> SummaryItems { get; init; } = Array.Empty<ReportSummaryItemDto>();
    public string? GeneralNotes { get; init; }
}
