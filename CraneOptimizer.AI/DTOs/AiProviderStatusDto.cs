namespace CraneOptimizer.AI.DTOs
{
    public sealed class AiProviderStatusDto
    {
        public string ProviderId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public bool IsAvailable { get; init; }
        public bool IsLocal { get; init; }
        public bool RequiresCredential { get; init; }
        public bool HasCredential { get; init; }
        public string Endpoint { get; init; } = string.Empty;
        public IReadOnlyList<string> Models { get; init; } = Array.Empty<string>();
        public string? ErrorMessage { get; init; }
    }
}
