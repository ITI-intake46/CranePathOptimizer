namespace CraneOptimizer.AI.Workflow
{
    public sealed class StructuredRecommendationCard
    {
        public string Status { get; init; } = string.Empty;
        public string Finding { get; init; } = string.Empty;
        public string Evidence { get; init; } = string.Empty;
        public string Recommendation { get; init; } = string.Empty;
        public string NextClick { get; init; } = string.Empty;
        public string Risk { get; init; } = string.Empty;
        public string Confidence { get; init; } = "Medium";
    }
}
