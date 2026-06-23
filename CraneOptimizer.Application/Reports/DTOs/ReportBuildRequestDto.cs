using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class ReportBuildRequestDto
{
    public ReportFamily Family { get; init; }
    public string TemplateName { get; init; } = string.Empty;
    public string ItemsIncluded { get; init; } = string.Empty;
    public ReportMetadataDto Metadata { get; init; } = new();
    public IReadOnlyList<ReportColumnDefinition> SelectedColumns { get; init; } = Array.Empty<ReportColumnDefinition>();
    public IReadOnlyList<PrecastElement> SourceElements { get; init; } = Array.Empty<PrecastElement>();
    public IReadOnlyList<PrecastElement> SelectedElements { get; init; } = Array.Empty<PrecastElement>();
    public IReadOnlyList<LiftingResultSourceRowDto> LiftingResults { get; init; } = Array.Empty<LiftingResultSourceRowDto>();
    public IReadOnlyList<CraneSetupReportRowDto> CraneSetups { get; init; } = Array.Empty<CraneSetupReportRowDto>();
}
