namespace CraneOptimizer.AI.Runtime
{
    public interface IAiTelemetryStore : IAiTelemetrySink
    {
        IReadOnlyList<AiTelemetryEvent> Snapshot();

        void Clear();
    }
}
