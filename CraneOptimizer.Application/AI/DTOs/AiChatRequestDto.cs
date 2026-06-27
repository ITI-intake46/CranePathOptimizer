namespace CraneOptimizer.Application.AI.DTOs
{
    public sealed class AiChatRequestDto
    {
        public string Model { get; init; } = string.Empty;
        public string SystemPrompt { get; init; } = string.Empty;
        public string UserPrompt { get; init; } = string.Empty;
        public string ContextJson { get; init; } = string.Empty;
        public double? Temperature { get; init; }
        public int? TimeoutSeconds { get; init; }
        public bool PreferStreaming { get; init; } = true;
    }
}
