using CraneOptimizer.AI.Configuration;

namespace CraneOptimizer.AI.Runtime
{
    public sealed class InMemoryAiTelemetrySink : IAiTelemetryStore
    {
        private readonly object _gate = new();
        private readonly Queue<AiTelemetryEvent> _events = new();
        private readonly IAiCopilotSettingsService _settingsService;

        public InMemoryAiTelemetrySink(IAiCopilotSettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public void Record(AiTelemetryEvent telemetryEvent)
        {
            ArgumentNullException.ThrowIfNull(telemetryEvent);

            lock (_gate)
            {
                _events.Enqueue(telemetryEvent);
                var capacity = Math.Max(10, _settingsService.Current.TelemetryBufferCapacity);
                while (_events.Count > capacity)
                    _events.Dequeue();
            }
        }

        public IReadOnlyList<AiTelemetryEvent> Snapshot()
        {
            lock (_gate)
                return _events.ToArray();
        }

        public void Clear()
        {
            lock (_gate)
                _events.Clear();
        }
    }
}
