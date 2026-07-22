namespace CraneOptimizer.AI.Runtime
{
    [Flags]
    public enum AiProviderCapabilities
    {
        None = 0,
        Chat = 1 << 0,
        Streaming = 1 << 1,
        StructuredOutput = 1 << 2,
        ToolCalling = 1 << 3,
        Embeddings = 1 << 4,
        Vision = 1 << 5
    }
}
