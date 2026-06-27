namespace CraneOptimizer.Application.AI.DTOs
{
    public sealed class AiRecommendationDto
    {
        public AiRecommendationSeverity Severity { get; init; } = AiRecommendationSeverity.Info;
        public string Title { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public string SuggestedAction { get; init; } = string.Empty;
    }
}
