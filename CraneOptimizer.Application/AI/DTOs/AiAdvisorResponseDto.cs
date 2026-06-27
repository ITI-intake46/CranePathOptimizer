namespace CraneOptimizer.Application.AI.DTOs
{
    public sealed class AiAdvisorResponseDto
    {
        public string HealthLabel { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public IReadOnlyList<AiRecommendationDto> Recommendations { get; init; } = Array.Empty<AiRecommendationDto>();
        public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();
    }
}
