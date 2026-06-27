namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Knowledge
{
    public interface IAiSoftwareInventoryService
    {
        string BuildXamlInventorySummary(int maxFiles = 25);
    }
}
