using CraneOptimizer.AI.Host;
using CraneOptimizer.UI.Shell.ViewModels;

namespace CraneOptimizer.UI.AIIntegration
{
    /// <summary>
    /// Builds a deterministic, read-only AI context pack for one UI feature area.
    /// Contributors must not mutate project state or invoke dialogs.
    /// </summary>
    public interface IAiScreenContextContributor
    {
        string ScreenName { get; }
        AiScreenContextSnapshot BuildSnapshot(MainViewModel shell);
    }
}
