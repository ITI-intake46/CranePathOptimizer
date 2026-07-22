namespace CraneOptimizer.AI.Runtime
{
    public interface IAiProviderRegistry
    {
        IReadOnlyList<AiProviderDescriptor> Providers { get; }

        bool TryResolve(string? providerId, out IAiChatProvider provider);
    }
}
