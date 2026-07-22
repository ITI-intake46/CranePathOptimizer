using CraneOptimizer.AI.Agents.Contracts;

namespace CraneOptimizer.AI.Agents.Runtime
{
    public sealed class AiToolRegistry : IAiToolRegistry
    {
        private readonly IReadOnlyDictionary<string, IAiProjectTool> _tools;

        public AiToolRegistry(IEnumerable<IAiProjectTool> tools)
        {
            ArgumentNullException.ThrowIfNull(tools);

            var materialized = tools.ToArray();
            var duplicates = materialized
                .GroupBy(tool => tool.Definition.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            if (duplicates.Length > 0)
                throw new InvalidOperationException("Duplicate AI tool names: " + string.Join(", ", duplicates));

            _tools = materialized.ToDictionary(
                tool => tool.Definition.Name,
                tool => tool,
                StringComparer.OrdinalIgnoreCase);

            Definitions = _tools.Values
                .Select(tool => tool.Definition)
                .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<AiToolDefinition> Definitions { get; }

        public IAiProjectTool GetRequired(string name)
        {
            if (TryGet(name, out var tool) && tool != null)
                return tool;

            throw new KeyNotFoundException($"AI tool '{name}' is not registered.");
        }

        public bool TryGet(string name, out IAiProjectTool? tool)
        {
            tool = null;
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return _tools.TryGetValue(name.Trim(), out tool);
        }
    }
}
