using CraneOptimizer.AI.DTOs;

namespace CraneOptimizer.AI.Interfaces
{
    /// <summary>
    /// Host adapter contract used by the isolated AI layer to read the current
    /// optimization result without referencing CraneOptimizer.UI view models.
    /// </summary>
    public interface IAiOptimizationContextProvider
    {
        AiOptimizationContextDto GetCurrentOptimizationContext();
    }
}
