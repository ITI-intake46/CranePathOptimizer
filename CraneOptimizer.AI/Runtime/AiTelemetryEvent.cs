namespace CraneOptimizer.AI.Runtime
{
    public sealed record AiTelemetryEvent(
        string CorrelationId,
        string ProviderId,
        string Model,
        DateTimeOffset StartedAt,
        long ElapsedMilliseconds,
        bool Success,
        bool Streaming,
        int SystemPromptCharacters,
        int UserPromptCharacters,
        int ContextCharacters,
        int ResponseCharacters,
        string ErrorCategory);
}
