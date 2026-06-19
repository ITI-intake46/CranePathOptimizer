namespace CraneOptimizer.Application.Reports.Models;

public sealed class ReportValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Warnings { get; init; } = new();
    public List<string> Errors { get; init; } = new();

    public static ReportValidationResult Valid()
    {
        return new ReportValidationResult();
    }
}
