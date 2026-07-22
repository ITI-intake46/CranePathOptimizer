using CraneOptimizer.AI.Agents.Contracts;

namespace CraneOptimizer.AI.Agents.Runtime
{
    public sealed class InMemoryAiAgentAuditStore : IAiAgentAuditStore
    {
        private readonly object _gate = new();
        private readonly LinkedList<AiAgentRunSnapshot> _runs = new();
        private readonly int _capacity;

        public InMemoryAiAgentAuditStore()
            : this(100)
        {
        }

        public InMemoryAiAgentAuditStore(int capacity)
        {
            _capacity = Math.Clamp(capacity, 10, 1000);
        }

        public IReadOnlyList<AiAgentRunSnapshot> GetRecent(int maxCount = 20)
        {
            var bounded = Math.Clamp(maxCount, 1, _capacity);
            lock (_gate)
                return _runs.Take(bounded).ToArray();
        }

        public void Record(AiAgentRunSnapshot run)
        {
            ArgumentNullException.ThrowIfNull(run);

            lock (_gate)
            {
                _runs.AddFirst(run);
                while (_runs.Count > _capacity)
                    _runs.RemoveLast();
            }
        }
    }
}
