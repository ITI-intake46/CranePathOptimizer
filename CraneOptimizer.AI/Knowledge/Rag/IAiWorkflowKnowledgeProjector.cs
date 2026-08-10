using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Knowledge.Rag
{
    public interface IAiWorkflowKnowledgeProjector
    {
        IReadOnlyList<AiKnowledgeDocument> Project(AiWorkflowContextSnapshot snapshot);
    }
}
