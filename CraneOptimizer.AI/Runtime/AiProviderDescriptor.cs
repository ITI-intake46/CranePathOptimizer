namespace CraneOptimizer.AI.Runtime
{
    public sealed record AiProviderDescriptor(
        string Id,
        string DisplayName,
        AiProviderCapabilities Capabilities,
        bool IsLocal,
        string? Endpoint = null);
}
