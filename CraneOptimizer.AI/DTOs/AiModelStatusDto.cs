namespace CraneOptimizer.AI.DTOs
{
    public sealed class AiModelStatusDto
    {
        public bool IsAvailable { get; init; }
        public string Endpoint { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public IReadOnlyList<string> InstalledModels { get; init; } = Array.Empty<string>();
        public string? ErrorMessage { get; init; }
    }
}
