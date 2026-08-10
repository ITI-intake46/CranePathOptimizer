using CraneOptimizer.AI.Agents.Contracts;

namespace CraneOptimizer.AI.Agents.Runtime
{
    public sealed class InMemoryAiAgentActionAuditStore : IAiAgentActionAuditStore
    {
        private readonly object _sync = new();
        private readonly LinkedList<AiAgentActionExecutionSnapshot> _executions = new();
        private readonly int _capacity;

        public InMemoryAiAgentActionAuditStore()
            : this(100)
        {
        }

        public InMemoryAiAgentActionAuditStore(int capacity)
        {
            _capacity = Math.Clamp(capacity, 10, 1000);
        }

        public IReadOnlyList<AiAgentActionExecutionSnapshot> GetRecent(int maxCount = 50)
        {
            lock (_sync)
                return _executions.Take(Math.Clamp(maxCount, 1, _capacity)).ToArray();
        }

        public void Record(AiAgentActionExecutionSnapshot execution)
        {
            ArgumentNullException.ThrowIfNull(execution);

            lock (_sync)
            {
                _executions.AddFirst(execution);
                while (_executions.Count > _capacity)
                    _executions.RemoveLast();
            }
        }
    }
}
