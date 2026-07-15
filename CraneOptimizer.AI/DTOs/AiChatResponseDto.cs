namespace CraneOptimizer.AI.DTOs
{
    public sealed class AiChatResponseDto
    {
        public bool Success { get; init; }
        public string Content { get; init; } = string.Empty;
        public string? ErrorMessage { get; init; }
    }
}
