using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Knowledge.Rag;

namespace CraneOptimizer.AI.Agents.Tools
{
    public sealed class SearchProjectEvidenceTool : AiProjectToolBase
    {
        private readonly IAiProjectGroundingService _groundingService;

        public SearchProjectEvidenceTool(IAiProjectGroundingService groundingService)
        {
            _groundingService = groundingService ?? throw new ArgumentNullException(nameof(groundingService));
        }

        public override AiToolDefinition Definition { get; } = new()
        {
            Name = "search_project_evidence",
            Description = "Runs hybrid lexical and semantic retrieval over the current project evidence snapshot.",
            InputSchema = "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}}}",
            OutputSchema = "{\"citations\":\"array\",\"coverage\":\"string\"}",
            AccessMode = AiToolAccessMode.ReadOnly,
            PermissionLevel = "ProjectRead"
        };

        public override Task<AiToolExecutionResult> ExecuteAsync(AiAgentToolContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var packet = _groundingService.Ground(context.Workflow, context.Question, 6);
            var evidence = packet.HasEvidence
                ? string.Join(Environment.NewLine, packet.Citations.Select(citation =>
                    $"[{citation.Id}] {citation.SourceLabel}: {citation.Snippet} ({citation.ScoreLabel})"))
                : "No project RAG evidence matched the requested goal.";

            return Task.FromResult(new AiToolExecutionResult
            {
                ToolName = Definition.Name,
                Succeeded = true,
                Summary = packet.HasEvidence
                    ? $"Retrieved {packet.Citations.Count:N0} grounded project source(s)."
                    : "No grounded project source matched the query.",
                Evidence = evidence,
                Facts = new Dictionary<string, string>
                {
                    ["CitationCount"] = packet.Citations.Count.ToString(),
                    ["IndexedDocumentCount"] = packet.IndexedDocumentCount.ToString(),
                    ["IndexedChunkCount"] = packet.IndexedChunkCount.ToString(),
                    ["ModelRevision"] = packet.ModelRevision.ToString()
                },
                Citations = packet.Citations
            });
        }
    }
}
