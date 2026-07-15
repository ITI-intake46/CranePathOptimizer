using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Interfaces
{
    /// <summary>
    /// Host adapter contract used by the isolated AI layer to read live workflow
    /// readiness context without referencing CraneOptimizer.UI shell classes.
    /// </summary>
    public interface IAiWorkflowContextProvider
    {
        event EventHandler? ContextChanged;

        AiWorkflowContextSnapshot GetSnapshot();
    }
}
