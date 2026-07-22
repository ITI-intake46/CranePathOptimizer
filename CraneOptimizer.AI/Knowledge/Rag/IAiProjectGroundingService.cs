using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Knowledge.Rag
{
    public interface IAiProjectGroundingService
    {
        AiGroundingPacket Ground(AiWorkflowContextSnapshot? snapshot, string? query, int maxEvidence = 6);
        string AppendCitationTrace(string answer, AiGroundingPacket packet);
    }
}
