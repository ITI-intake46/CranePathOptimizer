using System.Text;
using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Knowledge.Rag
{
    /// <summary>
    /// Creates a bounded, citation-addressable grounding packet for one user question.
    /// The packet can be consumed by an LLM or appended to deterministic answers.
    /// </summary>
    public sealed class AiProjectGroundingService : IAiProjectGroundingService
    {
        private readonly IAiWorkflowKnowledgeProjector _projector;
        private readonly IAiHybridKnowledgeRetriever _retriever;

        public AiProjectGroundingService(
            IAiWorkflowKnowledgeProjector projector,
            IAiHybridKnowledgeRetriever retriever)
        {
            _projector = projector ?? throw new ArgumentNullException(nameof(projector));
            _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        }

        public AiGroundingPacket Ground(AiWorkflowContextSnapshot? snapshot, string? query, int maxEvidence = 6)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(query))
                return AiGroundingPacket.Empty;

            var documents = _projector.Project(snapshot);
            var chunks = AiKnowledgeTextChunker.Chunk(documents);
            var citations = _retriever.Retrieve(documents, query.Trim(), maxEvidence);

            return new AiGroundingPacket
            {
                Query = query.Trim(),
                ProjectKey = snapshot.ProjectKey,
                ModelFingerprint = snapshot.ModelFingerprint,
                ModelRevision = snapshot.ModelRevision,
                IndexedDocumentCount = documents.Count,
                IndexedChunkCount = chunks.Count,
                Citations = citations,
                PromptContext = BuildPromptContext(snapshot, query.Trim(), citations, documents.Count, chunks.Count)
            };
        }

        public string AppendCitationTrace(string answer, AiGroundingPacket packet)
        {
            var normalized = string.IsNullOrWhiteSpace(answer)
                ? "No answer was generated."
                : answer.Trim();

            if (!packet.HasEvidence)
                return normalized;

            if (packet.Citations.Any(citation => normalized.Contains($"[{citation.Id}]", StringComparison.OrdinalIgnoreCase)))
                return normalized;

            var builder = new StringBuilder(normalized);
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("## Grounding evidence");
            foreach (var citation in packet.Citations)
            {
                builder.AppendLine($"- [{citation.Id}] **{citation.SourceLabel}** — {citation.Snippet}");
                builder.AppendLine($"  Source: `{citation.SourceLocator}` · {citation.ScoreLabel} · model revision {citation.ModelRevision:N0}");
            }
            builder.AppendLine();
            builder.AppendLine($"Grounding coverage: {packet.CoverageLabel}. Generated claims remain advisory; deterministic project evidence takes precedence.");
            return builder.ToString().Trim();
        }

        private static string BuildPromptContext(
            AiWorkflowContextSnapshot snapshot,
            string query,
            IReadOnlyList<AiKnowledgeCitation> citations,
            int documentCount,
            int chunkCount)
        {
            if (citations.Count == 0)
                return "PROJECT RAG GROUNDING: No relevant live-project evidence passed the retrieval threshold. Do not invent project facts; state what evidence is missing.";

            var builder = new StringBuilder();
            builder.AppendLine("PROJECT RAG GROUNDING PACKET — deterministic live-project evidence");
            builder.AppendLine($"Question: {query}");
            builder.AppendLine($"Project: {snapshot.ProjectDisplayName}; model revision {snapshot.ModelRevision}; fingerprint {snapshot.ModelFingerprint}");
            builder.AppendLine($"Indexed corpus: {documentCount:N0} documents / {chunkCount:N0} chunks");
            builder.AppendLine("Citation contract:");
            builder.AppendLine("- Ground every project-specific factual claim in one or more evidence IDs such as [E1].");
            builder.AppendLine("- Do not treat low-confidence evidence as verified engineering truth.");
            builder.AppendLine("- When evidence conflicts or is incomplete, say so explicitly.");
            builder.AppendLine("- Never recommend bypassing deterministic safety, weight, load-chart, or GA-readiness gates.");
            builder.AppendLine();

            foreach (var citation in citations)
            {
                builder.AppendLine($"[{citation.Id}] {citation.SourceLabel}");
                builder.AppendLine($"Source type: {citation.SourceType}");
                builder.AppendLine($"Source locator: {citation.SourceLocator}");
                builder.AppendLine($"Trust: {citation.TrustLevel}; retrieval confidence: {citation.Confidence}; relevance: {citation.Score:P0}");
                builder.AppendLine($"Evidence: {citation.Snippet}");
                builder.AppendLine();
            }

            return builder.ToString().Trim();
        }
    }
}
