namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class ElementReportRowDto
{
    public int No { get; init; }
    public string ElementId { get; init; } = string.Empty;
    public string ElementMark { get; init; } = string.Empty;
    public string SourceRevitId { get; init; } = string.Empty;
    public string StoreyLevel { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public double? Length { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
    public double? VolumeM3 { get; init; }
    public double? WeightTons { get; init; }
    public string WeightSource { get; init; } = string.Empty;
    public string WeightVerified { get; init; } = "No";
    public string WeightStatus { get; init; } = string.Empty;
    public string WeightWarning { get; init; } = "-";
    public string IncludedInCraneOptimization { get; init; } = "No";
}
