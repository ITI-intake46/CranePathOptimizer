namespace CraneOptimizer.AI.Knowledge.Rag
{
    /// <summary>
    /// Provider-neutral embedding contract. The deterministic local implementation is
    /// always available; Ollama or hosted embedding providers can be added later
    /// without changing retrieval or UI contracts.
    /// </summary>
    public interface IAiTextEmbeddingService
    {
        string ProviderId { get; }
        int Dimensions { get; }
        IReadOnlyList<double> Embed(string text);
    }
}
