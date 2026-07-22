namespace CraneOptimizer.AI.DTOs
{
    public sealed class AiChatResponseDto
    {
        public bool Success { get; init; }
        public string Content { get; init; } = string.Empty;
        public string? ErrorMessage { get; init; }
        public string ProviderId { get; init; } = string.Empty;
        public string CorrelationId { get; init; } = string.Empty;
        public long ElapsedMilliseconds { get; init; }
    }
}
