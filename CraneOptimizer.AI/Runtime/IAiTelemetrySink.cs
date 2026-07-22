namespace CraneOptimizer.AI.Runtime
{
    public interface IAiTelemetrySink
    {
        void Record(AiTelemetryEvent telemetryEvent);
    }
}
