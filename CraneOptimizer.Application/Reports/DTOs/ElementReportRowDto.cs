namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class ElementReportRowDto
{
    public int No { get; init; }
    public string ElementId { get; init; } = string.Empty;
    public string ElementMark { get; init; } = string.Empty;

    // Phase 5: Canonical Display Identity
    public string CanonicalMark { get; init; } = string.Empty;
    public string MarkSource { get; init; } = string.Empty;
    public string MarkConfidence { get; init; } = "None";
    public bool IsExplicitMark { get; init; }

    public string SourceRevitId { get; init; } = string.Empty;
    public string StoreyLevel { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public double? Length { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
    public double? VolumeM3 { get; init; }
    public string VolumeSource { get; init; } = "Missing";
    public string TopLevelElevation { get; init; } = "\u2014";
    public string BottomLevelElevation { get; init; } = "\u2014";
    public double? WeightTons { get; init; }
    public string WeightSource { get; init; } = string.Empty;
    public string WeightVerified { get; init; } = "No";
    public string WeightStatus { get; init; } = string.Empty;
    public string WeightWarning { get; init; } = "-";
    public string IncludedInCraneOptimization { get; init; } = "No";
    public string ReadyForGa { get; init; } = "No";
    public string MissingReadinessReason { get; init; } = "\u2014";
}
