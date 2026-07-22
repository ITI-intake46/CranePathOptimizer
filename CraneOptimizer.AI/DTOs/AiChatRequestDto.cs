namespace CraneOptimizer.AI.DTOs
{
    public sealed class AiChatRequestDto
    {
        public string ProviderId { get; init; } = string.Empty;
        public string CorrelationId { get; init; } = string.Empty;
        public string Model { get; init; } = string.Empty;
        public string SystemPrompt { get; init; } = string.Empty;
        public string UserPrompt { get; init; } = string.Empty;
        public string ContextJson { get; init; } = string.Empty;
        public double? Temperature { get; init; }
        public int? TimeoutSeconds { get; init; }
        public bool PreferStreaming { get; init; } = true;
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
