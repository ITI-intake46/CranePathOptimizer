namespace CraneOptimizer.AI.Knowledge
{
    public interface IAiSoftwareInventoryService
    {
        string BuildXamlInventorySummary(int maxFiles = 25);
    }
}
