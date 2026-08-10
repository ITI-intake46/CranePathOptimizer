using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Agents.Tools
{
    public abstract class AiProjectToolBase : IAiProjectTool
    {
        public abstract AiToolDefinition Definition { get; }

        public abstract Task<AiToolExecutionResult> ExecuteAsync(
            AiAgentToolContext context,
            CancellationToken cancellationToken = default);

        protected static IReadOnlyDictionary<string, string> MergeFacts(AiWorkflowContextSnapshot context)
        {
            var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var screen in context.ScreenContexts)
            {
                foreach (var fact in screen.Facts)
                    facts[fact.Key] = fact.Value;
            }

            return facts;
        }

        protected static int ReadInt(IReadOnlyDictionary<string, string> facts, string key, int fallback = 0)
            => facts.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

        protected AiToolExecutionResult Success(
            string summary,
            string evidence,
            IReadOnlyDictionary<string, string>? facts = null,
            IReadOnlyList<string>? actions = null)
            => new()
            {
                ToolName = Definition.Name,
                Succeeded = true,
                Summary = summary,
                Evidence = evidence,
                Facts = facts ?? new Dictionary<string, string>(),
                RecommendedActions = actions ?? Array.Empty<string>()
            };
    }
}
