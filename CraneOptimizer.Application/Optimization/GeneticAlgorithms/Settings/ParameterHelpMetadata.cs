namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;

public sealed class ParameterHelpMetadata
{
    public string ParameterKey        { get; init; } = string.Empty;
    public string DisplayName         { get; init; } = string.Empty;
    public string Description         { get; init; } = string.Empty;
    public string IncreaseEffect      { get; init; } = string.Empty;
    public string DecreaseEffect      { get; init; } = string.Empty;
    public string ValidRange          { get; init; } = string.Empty;
    public string RecommendedRange    { get; init; } = string.Empty;
    public string PerformanceImpact   { get; init; } = string.Empty;
    public string QualityImpact       { get; init; } = string.Empty;
    public string? SpecialNote        { get; init; } // nullable — only when needed
}
