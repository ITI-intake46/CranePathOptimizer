namespace CraneOptimizer.AI.DTOs
{
    /// <summary>
    /// Incremental response fragment emitted by local AI providers that support streaming.
    /// </summary>
    public sealed class AiStreamingUpdateDto
    {
        public string Delta { get; init; } = string.Empty;
        public string AccumulatedContent { get; init; } = string.Empty;
        public bool IsComplete { get; init; }
    }
}
